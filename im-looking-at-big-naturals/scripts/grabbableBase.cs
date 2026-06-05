using Godot;

/// <summary>
/// Abstract base for anything a PlayerHand can grab — items and climbing holds alike.
/// Subclass this for world items. For climbing holds, also subclass this (see outline).
///
/// This node IS the physics presence of the grabbable object. Give it a CollisionShape3D
/// child when it needs to be detected by the camera raycast in the world.
/// Items inside a BodySlot are detected through the slot — no CollisionShape needed
/// while slotted, but having a disabled one ready is fine.
///
/// Minimum scene setup:
///   MyGrabbable (MyClass : GrabbableBaseItem)
///     └─ CollisionShape3D   ← required for world-placement raycast detection
/// </summary>
public abstract partial class GrabbableBaseItem : Area3D
{
	/// <summary>
	/// When true the hand releases this on grab button-up.
	/// Items leave this false — they persist in the hand after the button is released.
	/// Climbing holds set this true — the player must hold the button to stay on.
	/// </summary>
	public virtual bool RequiresHeldButton => false;

	/// <summary>Called once when a PlayerHand first grabs this.</summary>
	public abstract void OnGrabbed(PlayerHand hand);

	/// <summary>Called once when the hand releases this — from button-up or a drop command.</summary>
	public abstract void OnDropped(PlayerHand hand);

	/// <summary>
	/// Called every physics frame while this is held AND the grab button is physically held down.
	/// Useful for hold maintenance, item charging, or other continuous actions.
	/// </summary>
	public virtual void WhileHeld(PlayerHand hand, float delta) { }

	/// <summary>
	/// Called on the press of the grab button while this is already held in a hand.
	/// Override for item activation: swinging, aiming, throwing, shooting, etc.
	/// </summary>
	public virtual void OnActivated(PlayerHand hand) { }

	/// <summary>
	/// Return false to block grabbing — e.g. hand-side restrictions, cooldowns,
	/// or "already held by the other hand" guards.
	/// </summary>
	public virtual bool CanBeGrabbed(PlayerHand hand) => true;
}
