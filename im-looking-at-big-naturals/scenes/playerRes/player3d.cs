using Godot;
using System;

public partial class player3d : CharacterBody3D
{
	public const float Speed = 8.0f;
	public const float RunSpeed = 7.0f;
	private const float groundAccel = 40f;
	private const float AirWishSpeed = 0.6f; // Small cap on "desired" air speed — keeping this well below
	private const float AirAccelerate = 150f; // what creates strafe arc angle
	private const float JumpVelocity = 4.5f;
	public const float MouseSensitivity = 0.003f;
	private const float MaxPitchAngle = 85.0f;
	private const float MaxRollAngle = 120.0f;
	private const float Friction = 35.0f;
	private const float FreelookReturnSpeed = 16.0f;
	private const float JumpBufferTime = 0.2f; // How long (seconds) a jump press is remembered before the player lands.
	// Camera inertia
	private const float CamPosStiffness    = 100f;
	private const float CamPosDamping      = 22f;
	private const float CamRotStiffness    = 100f;
	private const float CamRotDamping      = 16f;
	private const float LandingBobStrength = 2f; // Scaled by fall speed (units/s). Tune if bobs feel too strong.
	private const float JumpKickStrength   = 0.3f;  // Fixed downward offset impulse on jump.
	private const float MaxBobOffset       = 0.5f;   // Position clamp (metres) — safety net for extreme falls.
	private const float LateralLeanDeg     = 1f;   // Max camera roll from strafing (degrees).
	private const float ForwardTiltDeg     = 1f;   // Max camera pitch from forward/back movement (degrees).
	private Marker3D _twistPivot;
    private Marker3D _pitchPivot;
	private RayCast3D _camPicker;
	private bool _running = false;
	private bool _crouching = false;
	private bool _freelook = false;
	private bool _unfree = false;
	private bool _jumpBuffered = false;
	private float _jumpBufferTimer = 0f;
	// Camera inertia state
	private Camera3D _camera;
	private Vector3 _camRestPos = Vector3.Zero;
	private Vector3 _camRestRot = Vector3.Zero;
	private Vector3 _camPosOffset = Vector3.Zero;
	private Vector3 _camPosSpringVel = Vector3.Zero;
	private Vector3 _camRotOffset = Vector3.Zero;
	private Vector3 _camRotSpringVel = Vector3.Zero;
	private bool _wasOnFloor = false;
	private Vector3 _prevVelocity = Vector3.Zero;
	// Hand system
	private PlayerHand _leftHand;
	private PlayerHand _rightHand;
	private BodySlot _lastTargetedSlot;
	// Accumulated mouse movement since the last physics frame — consumed by hand inertia each tick.
	private Vector2 _mouseDeltaAccum = Vector2.Zero;

	public override void _Ready()
    {
        // Get references to our structural nodes
        _twistPivot = GetNode<Marker3D>("CamPivot");
        _pitchPivot = GetNode<Marker3D>("CamPivot/NeckPivot");
		_camPicker = GetNode<RayCast3D>("CamPivot/NeckPivot/RayCast3D");
		_camera = GetNode<Camera3D>("CamPivot/NeckPivot/Camera3D");
		// Store the camera's rest transform so inertia offsets are always additive,
		// not destructive — the scene's existing tilt/position is preserved.
		_camRestPos = _camera.Position;
		_camRestRot = _camera.Rotation;
		// Hand references — expects LeftHand and RightHand child nodes on this node.
		_leftHand  = GetNode<PlayerHand>("CamPivot/NeckPivot/Camera3D/HandPosL/Hand");
		_rightHand = GetNode<PlayerHand>("CamPivot/NeckPivot/Camera3D/HandPosR/Hand");
		_camPicker.Enabled = true; // keep hot every frame for grab detection and slot highlighting

        // Lock and hide the cursor inside the window bounds
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }
	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
		{
			velocity += GetGravity() * (float)delta;
		}

		// Tick down the jump buffer. The press itself is recorded in _UnhandledInput so we never miss it between physics ticks.
		_jumpBufferTimer = Mathf.Max(0f, _jumpBufferTimer - (float)delta);
		if (_jumpBufferTimer <= 0f) _jumpBuffered = false;

		// Handle Jump. `jumped` gates friction below — if we leave the ground this frame, ground friction must not run or it will kill player horizontal momentum.
		bool jumped = false;
		if (_jumpBuffered && IsOnFloor())
		{
			velocity.Y = JumpVelocity;
			jumped = true;
			_jumpBuffered = false;
			_jumpBufferTimer = 0f;
		}

		// Get the input direction and handle the movement/deceleration.
		Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
		Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
		// Skip the entire ground block when we just jumped. we're leaving the floor
		// this frame, so applying friction would kill the horizontal momentum we need.
		if (IsOnFloor() && !jumped)
        {
            if (direction != Vector3.Zero)
            {
                velocity.X = Mathf.MoveToward(velocity.X, direction.X * (Speed + (RunSpeed * Convert.ToInt32(_running))), groundAccel * (float)delta);
                velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * (Speed + (RunSpeed * Convert.ToInt32(_running))), groundAccel * (float)delta);
            }
            else
            {
                velocity.X = Mathf.MoveToward(velocity.X, 0, Friction * (float)delta);
                velocity.Z = Mathf.MoveToward(velocity.Z, 0, Friction * (float)delta);
            }
        }
        else
        {
            // Holding a strafe key while turning the mouse steers the arc and
            // allows gradual speed gain. this is the intended mechanic.
            if (direction != Vector3.Zero)
            {
                AirStrafe(ref velocity, direction, AirWishSpeed, AirAccelerate, (float)delta);
            }
        }
		

		Velocity = velocity;
		MoveAndSlide();
		if (!_freelook && !Mathf.IsZeroApprox(_twistPivot.Rotation.Y))
   		{
       		Vector3 twistRot = _twistPivot.Rotation;
      		twistRot.Y = Mathf.Lerp(twistRot.Y, 0f, 1f - Mathf.Exp(-FreelookReturnSpeed * (float)delta));
			if (Mathf.Abs(twistRot.Y) < 0.001f){twistRot.Y = 0f;}
      		_twistPivot.Rotation = twistRot;
   		}

		// MoveAndSlide has run — IsOnFloor() is current for this frame.
		bool justLanded = !_wasOnFloor && IsOnFloor();
		// Capture impact speed NOW — _prevVelocity still holds last frame's post-slide velocity,
		// matching the same proxy used by UpdateCameraInertia's landing bob.
		float impactSpeed = justLanded ? Mathf.Abs(_prevVelocity.Y) : 0f;
		UpdateCameraInertia((float)delta, jumped, justLanded);
		_prevVelocity = Velocity; // save post-slide velocity; used next frame as impact-speed proxy
		_wasOnFloor = IsOnFloor();

		// Shared inputs for hand inertia (avoid re-computing localVel twice).
		Vector3 localVel = Transform.Basis.Inverse() * new Vector3(Velocity.X, 0f, Velocity.Z);
		float maxSpeed = Speed + RunSpeed;

		// Compute the physics-frame aim target once for both hands.
		// Uses the automatic per-frame raycast update (no ForceRaycastUpdate needed here).
		GrabbableBaseItem aimTarget = null;
		if (_camPicker.IsColliding())
		{
			GodotObject col = _camPicker.GetCollider();
			if (col is GrabbableBaseItem g) aimTarget = g;
			else if (col is BodySlot s && s.HasItem) aimTarget = s.HeldItem;
		}

		// Drive hand springs and callbacks. _mouseDeltaAccum holds all mouse movement since
		// the last physics tick (accumulated in _UnhandledInput) and resets after each tick.
		_leftHand.Tick((float)delta, jumped, justLanded, impactSpeed, _mouseDeltaAccum, aimTarget, localVel, maxSpeed);
		_rightHand.Tick((float)delta, jumped, justLanded, impactSpeed, _mouseDeltaAccum, aimTarget, localVel, maxSpeed);
		_mouseDeltaAccum = Vector2.Zero;
		UpdateSlotTargeting();
	}

	public override void _UnhandledInput(InputEvent @event)
    {
        // Process mouse movement if the cursor is locked
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
			// 1. Check whether the player is in freelook or not
            if(_freelook)
			{
				// 1a. Rotate the character neck pivot left and right (Y axis)
            	_twistPivot.RotateY(-mouseMotion.Relative.X * MouseSensitivity);

				// 1b. Clamp the horizontal looking angle to prevent looking directly behind the player
				Vector3 currentRotationHor = _twistPivot.Rotation;
           		currentRotationHor.Y = Mathf.Clamp(
            		currentRotationHor.Y, 
                	Mathf.DegToRad(-MaxRollAngle), 
                	Mathf.DegToRad(MaxRollAngle)
				);
				_twistPivot.Rotation = currentRotationHor; 
			}
			else
			{
				// 1A. Rotate the character body pivot left and right (Y axis)
				RotateY(-mouseMotion.Relative.X * MouseSensitivity);
			}
            // 2. Rotate the camera pitch up and down (X axis)
            _pitchPivot.RotateX(-mouseMotion.Relative.Y * MouseSensitivity);
			
            // 3. Clamp the vertical looking angle to prevent flipping completely upside down
            Vector3 currentRotation = _pitchPivot.Rotation;
            currentRotation.X = Mathf.Clamp(
                currentRotation.X, 
                Mathf.DegToRad(-MaxPitchAngle), 
                Mathf.DegToRad(MaxPitchAngle)
            );
            _pitchPivot.Rotation = currentRotation;

			// Accumulate mouse movement for hand inertia. Multiple motion events can fire
			// between physics ticks; the sum is consumed once per physics frame then reset.
			_mouseDeltaAccum += mouseMotion.Relative;
        }
		
		// handle sprint action being held 
		if (@event.IsActionPressed("sprint")){_running = true;}
		// handle sprint action being unheld 
		if (@event.IsActionReleased("sprint")){_running = false;}

		// Buffer the jump press so _PhysicsProcess can't miss it between ticks.
		if (@event.IsActionPressed("jump"))
		{
			_jumpBuffered = true;
			_jumpBufferTimer = JumpBufferTime;
		}

		// handle freelook action being held
		if (@event.IsActionPressed("free_look")){_freelook = true;}
		// handle freelook action being unheld
		if (@event.IsActionReleased("free_look")){_freelook = false;}

		// Grab and drop — routed through the hand state machines.
		// GetCurrentGrabbable() calls ForceRaycastUpdate() for a fresh result at the moment of the press.
		if (@event.IsActionPressed("left_hand_grab"))   { _leftHand.OnGrabPressed(GetCurrentGrabbable()); }
		if (@event.IsActionReleased("left_hand_grab"))  { _leftHand.OnGrabReleased(); }
		if (@event.IsActionPressed("right_hand_grab"))  { _rightHand.OnGrabPressed(GetCurrentGrabbable()); }
		if (@event.IsActionReleased("right_hand_grab")) { _rightHand.OnGrabReleased(); }
		if (@event.IsActionPressed("left_hand_drop"))   { _leftHand.Drop(); }
		if (@event.IsActionPressed("right_hand_drop"))  { _rightHand.Drop(); }

        // toggles mouse capture off and on when pressing the Escape key
		if (@event.IsActionPressed("ui_cancel"))
		{
			if (Input.MouseMode == Input.MouseModeEnum.Captured)
			{
				Input.MouseMode = Input.MouseModeEnum.Visible;
			}
			else
			{
				Input.MouseMode = Input.MouseModeEnum.Captured;
			}
		}
    }

	/// <summary>
    /// Air accelerate formula.
	/// 
	/// When wishDir is roughly perpendicular to current velocity (i.e. strafing
	/// sideways while turning), currentSpeed ≈ 0, so addSpeed ≈ wishSpeed and
	/// we push the full accelSpeed. this is what lets the player curve and build
	/// speed through coordinated mouse + key movement.
    /// </summary>
    /// <param name="currentSpeed">
    /// how fast we're already moving in the wish direction
    /// </param>
	/// <param name="addSpeed">
    /// how much headroom is left before we'd exceed wishSpeed
    /// </param>
	/// <param name="accelSpeed">
    /// how fast we're already moving in the wish direction
    /// </param>
	/// <remarks>
	/// 
	/// </remarks
	private static void AirStrafe(ref Vector3 velocity, Vector3 wishDir, float wishSpeed, float accel, float delta)
	{
		float currentSpeed = new Vector3(velocity.X, 0f, velocity.Z).Dot(wishDir);
		float addSpeed = wishSpeed - currentSpeed;

		if (addSpeed <= 0f) return;

		float accelSpeed = Mathf.Min(accel * wishSpeed * delta, addSpeed);

		velocity.X += accelSpeed * wishDir.X;
		velocity.Z += accelSpeed * wishDir.Z;
	}

	/// <summary>
	/// Procedural camera inertia. Runs every physics frame, always after MoveAndSlide.
	///
	/// Three independent spring-damper channels:
	///   Z roll  — camera banks into the current strafe direction (target-driven spring).
	///   X pitch — camera tilts with forward/back momentum (target-driven spring).
	///   Y pos   — camera bobs on landing and on jump takeoff (impulse-driven spring).
	///
	/// All offsets are added on top of _camRestRot/_camRestPos, which hold the camera's
	/// original scene transform, so the scene's built-in tilt and position are preserved.
	/// </summary>
	private void UpdateCameraInertia(float delta, bool jumped, bool justLanded)
	{
		// Project world-space horizontal velocity into character-local space.
		// Dividing by (Speed + RunSpeed) normalises to [0..1] at max sprint so the
		// lean/tilt constants are expressed as "degrees at full run speed".
		Vector3 localVel = Transform.Basis.Inverse() * new Vector3(Velocity.X, 0f, Velocity.Z);
		float maxSpeed = Speed + RunSpeed;

		// ── Lateral lean (Z roll): camera banks into the direction of the strafe ──
		// Moving right (+local X) → negative Z rotation → right side of frame tilts down.
		float targetRoll = Mathf.DegToRad(-localVel.X * (LateralLeanDeg / maxSpeed));
		targetRoll = Mathf.Clamp(targetRoll, Mathf.DegToRad(-LateralLeanDeg), Mathf.DegToRad(LateralLeanDeg));
		_camRotSpringVel.Z += (targetRoll - _camRotOffset.Z) * CamRotStiffness * delta;
		_camRotSpringVel.Z -= _camRotSpringVel.Z * CamRotDamping * delta;
		_camRotOffset.Z += _camRotSpringVel.Z * delta;

		// ── Forward tilt (X pitch): camera pitches slightly with forward/back speed ──
		// Forward is -Z in Godot, so localVel.Z < 0 when moving forward.
		// Negating gives positive pitch (nose down), which reads as a forward lean.
		float targetPitch = Mathf.DegToRad(-localVel.Z * (ForwardTiltDeg / maxSpeed));
		targetPitch = Mathf.Clamp(targetPitch, Mathf.DegToRad(-ForwardTiltDeg), Mathf.DegToRad(ForwardTiltDeg));
		_camRotSpringVel.X += (targetPitch - _camRotOffset.X) * CamRotStiffness * delta;
		_camRotSpringVel.X -= _camRotSpringVel.X * CamRotDamping * delta;
		_camRotOffset.X += _camRotSpringVel.X * delta;

		// ── Vertical bob (Y position): impulse-driven spring ─────────────────────
		// Both impulses push the offset downward. The spring then pulls it back to zero,
		// producing the characteristic bob-down-then-return feel.
		if (justLanded)
		{
			// _prevVelocity.Y is the downward speed accumulated by gravity last frame —
			// a good proxy for impact force without needing a separate collision callback.
			_camPosSpringVel.Y -= Mathf.Abs(_prevVelocity.Y) * LandingBobStrength;
		}
		if (jumped)
		{
			// Camera lags behind the sudden upward launch — offset is pushed downward.
			_camPosSpringVel.Y -= JumpKickStrength;
		}
		_camPosSpringVel.Y -= _camPosOffset.Y * CamPosStiffness * delta; // spring toward zero
		_camPosSpringVel.Y -= _camPosSpringVel.Y * CamPosDamping * delta; // velocity damping
		_camPosOffset.Y += _camPosSpringVel.Y * delta;
		_camPosOffset.Y = Mathf.Clamp(_camPosOffset.Y, -MaxBobOffset, MaxBobOffset);

		// Apply both offsets additively on top of the camera's original rest transform.
		_camera.Position = _camRestPos + new Vector3(0f, _camPosOffset.Y, 0f);
		_camera.Rotation = _camRestRot + new Vector3(_camRotOffset.X, 0f, _camRotOffset.Z);
	}

	/// <summary>
	/// Reads the camera raycast to find the nearest GrabbableBaseItem or BodySlot item.
	/// Called on demand from input events. ForceRaycastUpdate() guarantees the result
	/// is fresh at the exact moment the button was pressed, not from the last physics tick.
	/// Returns null if nothing grabbable is in range or in the crosshair.
	/// </summary>
	private GrabbableBaseItem GetCurrentGrabbable()
	{
		_camPicker.ForceRaycastUpdate();
		if (!_camPicker.IsColliding()) return null;

		GodotObject collider = _camPicker.GetCollider();

		// Direct hit on a world-placed grabbable (loose items, climbing holds, etc.)
		if (collider is GrabbableBaseItem grabbable) return grabbable;

		// Hit a body slot — hand over the item it contains, not the slot node itself.
		if (collider is BodySlot slot && slot.HasItem) return slot.HeldItem;

		return null;
	}

	/// <summary>
	/// Runs every physics frame. Tracks which BodySlot is currently under the crosshair
	/// and calls SetTargeted() so slots can show or hide their highlight indicator.
	/// Uses the physics-updated raycast result — no ForceRaycastUpdate() needed here.
	/// </summary>
	private void UpdateSlotTargeting()
	{
		BodySlot current = null;
		if (_camPicker.IsColliding() && _camPicker.GetCollider() is BodySlot slot)
			current = slot;

		if (current != _lastTargetedSlot)
		{
			_lastTargetedSlot?.SetTargeted(false);
			current?.SetTargeted(true);
			_lastTargetedSlot = current;
		}
	}
}
