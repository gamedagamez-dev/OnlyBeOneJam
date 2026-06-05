using Godot;

public enum HandSide  { Left, Right }
public enum HandState { Empty, HoldingItem, GrabbingHold }

/// <summary>
/// State machine for one of the player's hands.
/// Add two of these as children of the player — one per hand.
/// Position each under the camera rig (CamPivot/NeckPivot or similar) so held
/// items follow the camera correctly in first-person.
///
/// Scene setup:
///   LeftHand (PlayerHand)         ← attach script, set Side = Left
///     └─ GripMarker (Marker3D)    ← set _gripMarkerPath to this node; held items parent here
/// </summary>
public partial class PlayerHand : Node3D
{
	[Export] public HandSide Side = HandSide.Left;

	[Export] private NodePath _gripMarkerPath;
	/// <summary>The Marker3D that held items reparent themselves to.</summary>
	public Marker3D GripMarker { get; private set; }
	public HandState State      { get; private set; } = HandState.Empty;
	public GrabbableBaseItem HeldGrabbable { get; private set; }
	public bool IsEmpty => State == HandState.Empty;

	// True while the physical grab button is held — drives WhileHeld and RequiresHeldButton release.
	private bool _grabButtonHeld = false;

	public override void _Ready()
	{
		GripMarker = GetNode<Marker3D>(_gripMarkerPath);
	}

	/// <summary>
	/// Called by the player every physics frame.
	/// Drives continuous held-button callbacks on the current grabbable.
	/// </summary>
	public void Tick(float delta)
	{
		if (HeldGrabbable == null || !_grabButtonHeld) return;
		HeldGrabbable.WhileHeld(this, delta);
	}

	/// <summary>
	/// Called on grab button press.
	/// Empty hand: attempts to grab the supplied target.
	/// Hand already holding something: activates it instead (press-to-use).
	/// </summary>
	public void OnGrabPressed(GrabbableBaseItem target)
	{
		_grabButtonHeld = true;

		if (HeldGrabbable != null)
		{
			// Already holding — press activates the item rather than grabbing again.
			HeldGrabbable.OnActivated(this);
			return;
		}

		if (target == null || !target.CanBeGrabbed(this)) return;
		Grab(target);
	}

	/// <summary>
	/// Called on grab button release.
	/// Only releases the held object if it requires the button to remain held (e.g. climbing holds).
	/// Items stay in hand regardless.
	/// </summary>
	public void OnGrabReleased()
	{
		_grabButtonHeld = false;

		if (HeldGrabbable != null && HeldGrabbable.RequiresHeldButton)
			Release();
	}

	/// <summary>
	/// Drops whatever the hand is holding (Q / E keys).
	/// Unconditional — releases both items and holds.
	/// </summary>
	public void Drop()
	{
		if (HeldGrabbable != null)
			Release();
	}

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
}
