using Godot;

/// <summary>
/// Abstract base for all carriable inventory items.
/// Items persist in the hand after the grab button is released (RequiresHeldButton = false).
/// Override OnActivated() for item-specific use: swing, aim, throw, shoot, etc.
///
/// World scene setup (loose item):
///   MyItem (MyClass : InventoryItemBase)
///     ├─ CollisionShape3D    ← required for raycast detection when in the world
///     ├─ MeshInstance3D      ← hidden automatically when held; shown on drop
///     └─ GripPoint (Marker3D)  ← optional; the item's "handle", aligns to the hand's GripMarker
///
/// Items inside a BodySlot are detected through the slot's own collision shape —
/// the item's CollisionShape can be disabled while slotted, or simply not present if
/// the item will never be placed loose in the world.
///
/// Visuals:
///   Set HandSprite to the Texture2D that the hand sprite should display while this item is held.
///   If left null, the hand keeps its default bare-hand sprite.
///   All VisualInstance3D children (meshes, sprites) are hidden automatically when grabbed and
///   restored when dropped. Set _worldMeshPath to target a specific node if needed.
/// </summary>
public abstract partial class InventoryItemBase : GrabbableBaseItem
{
	public override bool RequiresHeldButton => false;

	[Export] public string ItemName { get; protected set; } = "Item";

	/// <summary>
	/// Texture shown on the hand sprite while this item is held.
	/// The hand keeps its default sprite if this is left null.
	/// </summary>
	[Export] public Texture2D HandSprite;

	/// <summary>
	/// Optional: path to a specific Node3D to hide while held (e.g. a single MeshInstance3D).
	/// If empty, ALL VisualInstance3D children are hidden automatically.
	/// CollisionShape3D children are never touched.
	/// </summary>
	[Export] private NodePath _worldMeshPath;

	/// <summary>
	/// Path to a Marker3D on this item that marks its grip point (handle).
	/// The item is positioned so this point sits exactly at the hand's GripMarker.
	/// Leave empty to snap the item's own origin to the grip marker instead.
	/// </summary>
	[Export] private NodePath _gripPointPath;

	private Marker3D _gripPoint;
	private Node3D   _worldMesh; // specific node to hide, resolved from _worldMeshPath if set
	private BodySlot _homeSlot;
	protected PlayerHand _heldBy;

	public override void _Ready()
	{
		if (_gripPointPath != null && !_gripPointPath.IsEmpty)
			_gripPoint = GetNodeOrNull<Marker3D>(_gripPointPath);
		if (_worldMeshPath != null && !_worldMeshPath.IsEmpty)
			_worldMesh = GetNodeOrNull<Node3D>(_worldMeshPath);
	}

	/// <summary>Block grabbing while already held — prevents the other hand from stealing it.</summary>
	public override bool CanBeGrabbed(PlayerHand hand) => _heldBy == null;

	public override void OnGrabbed(PlayerHand hand)
	{
		if (_homeSlot != null)
		{
			_homeSlot.ClearSlot();
			_homeSlot = null;
		}

		_heldBy = hand;

		// Hide the 3D world visuals — the hand sprite takes over while held.
		SetWorldVisualsVisible(false);
		hand.SetItemSprite(HandSprite);

		// Reparent to the grip marker so the item follows the hand in world space.
		// keepGlobalTransform = false — we set the local offset explicitly below.
		Reparent(hand.GripMarker, false);

		// Align the item's grip point to the marker origin, or snap origin-to-origin.
		Position = _gripPoint != null ? -_gripPoint.Position : Vector3.Zero;
		Rotation = _gripPoint != null ? -_gripPoint.Rotation : Vector3.Zero;
	}

	public override void OnDropped(PlayerHand hand)
	{
		_heldBy = null;

		// Restore 3D world visuals and clear the hand sprite.
		hand.ClearItemSprite();
		SetWorldVisualsVisible(true);

		// Return the item to the scene root; it stays at its current world-space position.
		Node scene = GetTree().CurrentScene;
		if (scene != null) Reparent(scene);
	}

	/// <summary>Override to implement item-specific use (swing, aim, etc.).</summary>
	public override void OnActivated(PlayerHand hand) { }

	/// <summary>Called by BodySlot.StoreItem() to register this item's home slot.</summary>
	public void SetHomeSlot(BodySlot slot) => _homeSlot = slot;

	/// <summary>
	/// Shows or hides this item's world-space visuals.
	/// Uses _worldMesh if set; otherwise toggles all VisualInstance3D children.
	/// CollisionShape3D nodes are intentionally unaffected.
	/// </summary>
	private void SetWorldVisualsVisible(bool visible)
	{
		if (_worldMesh != null)
		{
			_worldMesh.Visible = visible;
			return;
		}
		foreach (Node child in GetChildren())
			if (child is VisualInstance3D vis) vis.Visible = visible;
	}
}
