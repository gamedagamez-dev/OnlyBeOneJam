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
	private const float JumpBufferTime = 0.1f; // How long (seconds) a jump press is remembered before the player lands.
	private Marker3D _twistPivot;
    private Marker3D _pitchPivot;
	private RayCast3D _camPicker;
	private bool _running = false;
	private bool _crouching = false;
	private bool _freelook = false;
	private bool _unfree = false;
	private bool _jumpBuffered = false;
	private float _jumpBufferTimer = 0f;

	public override void _Ready()
    {
        // Get references to our structural nodes
        _twistPivot = GetNode<Marker3D>("CamPivot");
        _pitchPivot = GetNode<Marker3D>("CamPivot/NeckPivot");
		_camPicker = GetNode<RayCast3D>("CamPivot/NeckPivot/RayCast3D");

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

		// handle left hand interact action
		if (@event.IsActionPressed("left_hand_grab")){GetPickerSlot();}

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
	/// we push the full accelSpeed — this is what lets the player curve and build
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

	private void GetPickerSlot(){
		_camPicker.Enabled = true;
		if(_camPicker.IsColliding())
		{
			Area3D tempArea = (Area3D)_camPicker.GetCollider();
			if(tempArea.IsInGroup("PlayerSlot"))
			{
				GD.Print("Collided with slot");
			}
			else
			{
				GD.Print("Collided with nothing");
			}
		}
	}
}