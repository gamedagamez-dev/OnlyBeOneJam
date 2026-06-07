using Godot;

public enum HandSide  { Left, Right }
public enum HandState { Empty, HoldingItem, GrabbingHold }

/// <summary>
/// State machine and inertia layer for one of the player's hands.
///
/// Handles grab input, sprite-based visuals, a reach animation, and three
/// independent spring-damper channels that mirror the camera inertia system:
///   Y position — landing bob, jump kick, vertical look drag
///   X position — lateral strafe sway, horizontal look drag
///   Z position — forward/back sway
///   Z rotation — sprite roll in the strafe direction
///
/// Scene setup (hand.tscn):
///   Hand (PlayerHand)              ← this script; Side export set per-instance
///     ├─ Hand (Sprite3D)           ← the hand sprite; auto-found by name "Hand" or via _handSpritePath
///     └─ GripMarker (Marker3D)     ← held items reparent here; set _gripMarkerPath
///
/// The parent node (HandPosL / HandPosR Marker3D) defines the rest position anchor.
/// All spring offsets are additive around that anchor's local origin.
/// </summary>
public partial class PlayerHand : Node3D
{
	[Export] public HandSide Side = HandSide.Left;
	[Export] private NodePath _gripMarkerPath;
	/// <summary>Optional override for the hand Sprite3D path; falls back to child named "Hand".</summary>
	[Export] private NodePath _handSpritePath;

	public Marker3D GripMarker { get; private set; }
	public HandState State { get; private set; } = HandState.Empty;
	public GrabbableBaseItem HeldGrabbable { get; private set; }
	public bool IsEmpty => State == HandState.Empty;

	// ── Inertia tuning ───────────────────────────────────────────────────────
	private const float HandPosStiffness = 120f;
	private const float HandPosDamping   = 18f;
	private const float HandRotStiffness = 80f;
	private const float HandRotDamping   = 16f;
	private const float HandLandingBob   = 0.2f;   // scales with fall speed (units/s)
	private const float HandJumpKick     = 0.15f;  // fixed downward impulse on jump
	private const float HandMaxBobOffset = 0.1f;  // Y position clamp (metres)
	private const float HandLateralSway  = 0.03f;  // max X offset at full strafe speed
	private const float HandForwardSway  = 0.015f; // max Z offset at full run speed
	private const float HandMouseLagX    = 0.00025f; // X impulse per mouse pixel (horizontal look)
	private const float HandMouseLagY    = 0.00025f; // Y impulse per mouse pixel (vertical look)
	private const float HandRollDeg      = 2f;     // max sprite Z rotation from strafing (degrees)
	private const float HandReachDist    = 0.06f;  // how far the hand extends when reaching

	// ── Sprite state ─────────────────────────────────────────────────────────
	private Sprite3D  _handSprite;
	private Texture2D _defaultHandTexture;

	// ── Spring-damper inertia state ───────────────────────────────────────────
	private Vector3 _posOffset    = Vector3.Zero; // additive position offset from anchor rest
	private Vector3 _posSpringVel = Vector3.Zero;
	private Vector3 _rotOffset    = Vector3.Zero; // additive rotation offset
	private Vector3 _rotSpringVel = Vector3.Zero;

	// ── Reach state ───────────────────────────────────────────────────────────
	// Kept separate from _posOffset so it retracts cleanly without fighting inertia.
	private Vector3 _reachOffset = Vector3.Zero;
	private Vector3 _reachVel   = Vector3.Zero;

	// ── Hold pose override ────────────────────────────────────────────────────
	// When set by a ClimbingHoldBase, the hand springs to this world-space position
	// each frame instead of the reach/rest offset.
	private Vector3? _poseWorldTarget = null;

	// True while the grab button is physically held — drives WhileHeld and RequiresHeldButton release.
	private bool _grabButtonHeld = false;

	public override void _Ready()
	{
		GripMarker = GetNode<Marker3D>(_gripMarkerPath);

		// Resolve the hand sprite — exported path takes priority; "Hand" child is the fallback.
		Sprite3D sprite = null;
		if (_handSpritePath != null && !_handSpritePath.IsEmpty)
			sprite = GetNodeOrNull<Sprite3D>(_handSpritePath);
		if (sprite == null)
			sprite = GetNodeOrNull<Sprite3D>("Hand");
		_handSprite = sprite;
		if (_handSprite != null) _defaultHandTexture = _handSprite.Texture;
	}

	/// <summary>
	/// Called by the player every physics frame.
	/// Drives WhileHeld callbacks, the reach animation, and all spring-damper channels.
	/// </summary>
	public void Tick(float delta, bool jumped, bool justLanded, float impactSpeed,
	                 Vector2 mouseDelta, GrabbableBaseItem aimTarget,
	                 Vector3 playerLocalVel, float maxSpeed)
	{
		if (HeldGrabbable != null && _grabButtonHeld)
			HeldGrabbable.WhileHeld(this, delta);

		UpdateReach(delta, aimTarget);
		UpdatePositionInertia(delta, jumped, justLanded, impactSpeed, mouseDelta, playerLocalVel, maxSpeed);
		UpdateRotationInertia(delta, playerLocalVel, maxSpeed);

		// Apply all spring offsets. The parent anchor (HandPosL/R Marker3D) holds the true
		// rest position; this node's local position offsets around that anchor.
		if (_poseWorldTarget.HasValue)
		{
			// Hold mode: spring toward the hold's world position in anchor-local space.
			// _posOffset adds elasticity so the hand stretches naturally as the body moves.
			Node3D anchor = GetParentOrNull<Node3D>();
			Vector3 localTarget = anchor != null ? anchor.ToLocal(_poseWorldTarget.Value) : Vector3.Zero;
			Position = localTarget + _posOffset;
		}
		else
		{
			Position = _reachOffset + _posOffset;
		}
		Rotation = _rotOffset;
	}

	/// <summary>
	/// Called on grab button press.
	/// Empty hand: tries to grab the target. Holding something: activates it.
	/// </summary>
	public void OnGrabPressed(GrabbableBaseItem target)
	{
		_grabButtonHeld = true;

		if (HeldGrabbable != null)
		{
			HeldGrabbable.OnActivated(this);
			return;
		}

		if (target == null || !target.CanBeGrabbed(this)) return;
		Grab(target);
	}

	/// <summary>
	/// Called on grab button release.
	/// Only releases the held object if it requires the button to remain held (climbing holds).
	/// Items stay in hand regardless.
	/// </summary>
	public void OnGrabReleased()
	{
		_grabButtonHeld = false;

		if (HeldGrabbable != null && HeldGrabbable.RequiresHeldButton)
			Release();
	}

	/// <summary>Drops whatever the hand holds (Q / E keys). Unconditional.</summary>
	public void Drop()
	{
		if (HeldGrabbable != null)
			Release();
	}

	/// <summary>
	/// Switches the hand sprite to an item-specific texture.
	/// Called by InventoryItemBase.OnGrabbed — the item's 3D mesh hides itself separately.
	/// If tex is null the default sprite is kept, so items without a hand sprite still work.
	/// </summary>
	public void SetItemSprite(Texture2D tex)
	{
		if (_handSprite == null || tex == null) return;
		_handSprite.Texture = tex;
	}

	/// <summary>
	/// Restores the hand's bare-hand sprite. Called by InventoryItemBase.OnDropped.
	/// </summary>
	public void ClearItemSprite()
	{
		if (_handSprite == null) return;
		_handSprite.Texture = _defaultHandTexture;
	}

	/// <summary>
	/// Sets (or clears) a world-space position the hand will spring toward each frame.
	/// Called by ClimbingHoldBase: pass the hold's GlobalPosition in OnGrabbed / WhileHeld,
	/// and null in OnDropped to restore normal reach / rest behaviour.
	/// </summary>
	public void SetPoseOverride(Vector3? worldTarget) => _poseWorldTarget = worldTarget;

	// ── Private helpers ───────────────────────────────────────────────────────

	private void Grab(GrabbableBaseItem target)
	{
		HeldGrabbable = target;
		State = target.RequiresHeldButton ? HandState.GrabbingHold : HandState.HoldingItem;
		target.OnGrabbed(this);
	}

	private void Release()
	{
		GrabbableBaseItem released = HeldGrabbable;
		HeldGrabbable = null;
		State = HandState.Empty;
		released.OnDropped(this);
	}

	/// <summary>
	/// Reach spring — extends the hand when the grab button is held with nothing in it.
	/// When an aim target exists the reach steers toward it (in the anchor's local space),
	/// giving the impression the hand is reaching for that specific object.
	/// Retracts to zero once an item is grabbed, producing a natural snap-back.
	/// </summary>
	private void UpdateReach(float delta, GrabbableBaseItem aimTarget)
	{
		Vector3 reachTarget = Vector3.Zero;

		if (_grabButtonHeld && HeldGrabbable == null)
		{
			// Base reach: forward and slightly upward
			reachTarget = new Vector3(0f, HandReachDist * 0.3f, -HandReachDist);

			// Steer toward the aim target in the anchor parent's local space.
			// This biases the reach in the screen direction of the targeted object.
			Node3D anchor = GetParentOrNull<Node3D>();
			if (anchor != null && aimTarget != null && aimTarget.IsInsideTree())
			{
				Vector3 toTarget = anchor.ToLocal(aimTarget.GlobalPosition).Normalized();
				reachTarget.X += toTarget.X * HandReachDist;
				reachTarget.Y += toTarget.Y * HandReachDist;
			}
		}

		_reachVel += (reachTarget - _reachOffset) * HandPosStiffness * delta;
		_reachVel -= _reachVel * HandPosDamping * delta;
		_reachOffset += _reachVel * delta;
	}

	/// <summary>
	/// Position spring — landing bob, jump kick, lateral and forward sway, and camera look drag.
	///
	/// Look drag impulses: the player's mouse input is passed in as mouseDelta (accumulated
	/// since the last physics tick). Looking right pushes the hand left; looking down pushes
	/// the hand up — mimicking the lag of a weighted object held loosely in the hand.
	/// </summary>
	private void UpdatePositionInertia(float delta, bool jumped, bool justLanded, float impactSpeed,
	                                   Vector2 mouseDelta, Vector3 localVel, float maxSpeed)
	{
		// ── Y: landing bob, jump kick, vertical look drag ─────────────────────
		if (justLanded) _posSpringVel.Y -= impactSpeed * HandLandingBob;
		if (jumped)     _posSpringVel.Y -= HandJumpKick;
		// Looking down (positive mouseDelta.Y) pushes hand upward — it lags behind camera pitch.
		_posSpringVel.Y += mouseDelta.Y * HandMouseLagY;
		_posSpringVel.Y += (0f - _posOffset.Y) * HandPosStiffness * delta; // spring toward zero
		_posSpringVel.Y -= _posSpringVel.Y * HandPosDamping * delta;
		_posOffset.Y    += _posSpringVel.Y * delta;
		_posOffset.Y     = Mathf.Clamp(_posOffset.Y, -HandMaxBobOffset, HandMaxBobOffset);

		// ── X: lateral strafe sway + horizontal look drag ─────────────────────
		// Moving right (+local X) swings hand left; spring target is opposite strafe.
		float targetSwayX = -localVel.X * (HandLateralSway / maxSpeed);
		// Looking right (positive mouseDelta.X) pushes hand left — it lags behind camera yaw.
		_posSpringVel.X -= mouseDelta.X * HandMouseLagX;
		_posSpringVel.X += (targetSwayX - _posOffset.X) * HandPosStiffness * delta;
		_posSpringVel.X -= _posSpringVel.X * HandPosDamping * delta;
		_posOffset.X    += _posSpringVel.X * delta;

		// ── Z: forward/back sway ─────────────────────────────────────────────
		// Forward is -Z; moving forward (localVel.Z < 0) drifts hand slightly rearward.
		float targetSwayZ = localVel.Z * (HandForwardSway / maxSpeed);
		_posSpringVel.Z += (targetSwayZ - _posOffset.Z) * HandPosStiffness * delta;
		_posSpringVel.Z -= _posSpringVel.Z * HandPosDamping * delta;
		_posOffset.Z    += _posSpringVel.Z * delta;
	}

	/// <summary>
	/// Rotation spring — hand sprite rolls gently in the direction of the current strafe.
	/// </summary>
	private void UpdateRotationInertia(float delta, Vector3 localVel, float maxSpeed)
	{
		float targetRoll = Mathf.DegToRad(-localVel.X * (HandRollDeg / maxSpeed));
		_rotSpringVel.Z += (targetRoll - _rotOffset.Z) * HandRotStiffness * delta;
		_rotSpringVel.Z -= _rotSpringVel.Z * HandRotDamping * delta;
		_rotOffset.Z    += _rotSpringVel.Z * delta;
	}
}