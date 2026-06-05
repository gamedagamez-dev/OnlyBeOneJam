using Godot;

/// <summary>
/// Abstract base for all carriable inventory items.
/// Items persist in the hand after the grab button is released (RequiresHeldButton = false).
/// Override OnActivated() for item-specific use: swing, aim, throw, shoot, etc.
///
/// World scene setup (loose item):
///   MyItem (MyClass : InventoryItemBase)
///     ├─ CollisionShape3D    ← required for raycast detection when in the world
///     ├─ MeshInstance3D
///     └─ GripPoint (Marker3D)  ← optional; the item's "handle", aligns to the hand's GripMarker
///
/// Items inside a BodySlot are detected through the slot's own collision shape —
/// the item's CollisionShape can be disabled while slotted, or simply not present if
/// the item will never be placed loose in the world.
/// </summary>
public abstract partial class InventoryItemBase : GrabbableBaseItem
{
	public override bool RequiresHeldButton => false;

	[Export] public string ItemName { get; protected set; } = "Item";

	/// <summary>
	/// Path to a Marker3D on this item that marks its grip point (handle).
	/// The item is positioned so this point sits exactly at the hand's GripMarker.
	/// Leave empty to snap the item's own origin to the grip marker instead.
	/// </summary>
	[Export] private NodePath _gripPointPath;

	private Marker3D _gripPoint;
	private BodySlot _homeSlot;     // set when this item lives in a BodySlot
	protected PlayerHand _heldBy;

	public override void _Ready()
	{
		if (_gripPointPath != null && !_gripPointPath.IsEmpty)
			_gripPoint = GetNodeOrNull<Marker3D>(_gripPointPath);
	}

	/// <summary>Block grabbing while already held — prevents the other hand from stealing it.</summary>
	public override bool CanBeGrabbed(PlayerHand hand) => _heldBy == null;

	public override void OnGrabbed(PlayerHand hand)
	{
		// Notify the body slot this item is leaving it.
		if (_homeSlot != null)
		{
			_homeSlot.ClearSlot();
			_homeSlot = null;
		}

		_heldBy = hand;

		// Reparent to the grip marker so the item follows the hand in world space.
		// keepGlobalTransform = false — we immediately set the local offset ourselves below.
		Reparent(hand.GripMarker, false);

		// Align the item's grip point to the marker origin, or snap origin-to-origin.
		Position = _gripPoint != null ? -_gripPoint.Position : Vector3.Zero;
		Rotation = _gripPoint != null ? -_gripPoint.Rotation : Vector3.Zero;
	}

	public override void OnDropped(PlayerHand hand)
	{
		_heldBy = null;
		// Return the item to the scene root; it stays at its current world-space position.
		Node scene = GetTree().CurrentScene;
		if (scene != null) Reparent(scene);
	}

	/// <summary>Override to implement item-specific use (swing, aim, etc.).</summary>
	public override void OnActivated(PlayerHand hand) { }

	/// <summary>Called by BodySlot.StoreItem() to register this item's home slot.</summary>
	public void SetHomeSlot(BodySlot slot) => _homeSlot = slot;
}
