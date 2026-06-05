using Godot;

/// <summary>
/// A diegetic inventory slot mounted on the player's body.
/// The player retrieves items by looking down and freelooking to aim at the slot,
/// then pressing the grab button — identical flow to picking up a world item.
///
/// Add as a child of the player mesh/body node with a CollisionShape3D sized to
/// cover the slot's visual footprint. The item inside the slot does NOT need its
/// own CollisionShape while slotted.
///
/// Scene setup:
///   BodySlot (BodySlot)
///     ├─ CollisionShape3D      ← hitbox for the camera raycast
///     └─ [MyItem] (optional)   ← pre-place an item here in the editor; _Ready() auto-registers it
///
/// Override OnItemStored / OnItemTaken / OnTargetedChanged for audio and visual feedback.
/// </summary>
public partial class BodySlot : Area3D
{
	[Export] public string SlotName = "Slot";

	public InventoryItemBase HeldItem { get; private set; }
	public bool HasItem => HeldItem != null;
	public bool IsTargeted { get; private set; }

	public override void _Ready()
	{
		// Auto-register any InventoryItemBase child placed in the scene editor
		// so designers can pre-fill slots without extra script calls.
		foreach (Node child in GetChildren())
		{
			if (child is InventoryItemBase item)
			{
				StoreItem(item);
				break; // slots hold only one item
			}
		}
	}

	/// <summary>
	/// Stores an item in this slot. Reparents it here, zeroes its local transform,
	/// and registers this as the item's home so it can notify on grab.
	/// </summary>
	public void StoreItem(InventoryItemBase item)
	{
		if (HasItem)
		{
			GD.PushWarning($"BodySlot '{SlotName}' already occupied — cannot store {item.ItemName}.");
			return;
		}
		HeldItem = item;
		item.Reparent(this, false);
		item.Position = Vector3.Zero;
		item.Rotation = Vector3.Zero;
		item.SetHomeSlot(this);
		OnItemStored(item);
	}

	/// <summary>
	/// Called by the item's OnGrabbed when it leaves this slot.
	/// Clears the internal reference; the item handles its own reparenting.
	/// </summary>
	public void ClearSlot()
	{
		InventoryItemBase leaving = HeldItem;
		HeldItem = null;
		if (leaving != null) OnItemTaken(leaving);
	}

	/// <summary>
	/// Marks this slot as aimed-at by the player so subclasses can show a highlight.
	/// Called each physics frame by the player controller via UpdateSlotTargeting().
	/// </summary>
	public void SetTargeted(bool targeted)
	{
		if (IsTargeted == targeted) return;
		IsTargeted = targeted;
		OnTargetedChanged(targeted);
	}

	/// <summary>Override to play a store sound or show an item-seated indicator.</summary>
	protected virtual void OnItemStored(InventoryItemBase item) { }

	/// <summary>Override to play a take sound or clear the item-seated indicator.</summary>
	protected virtual void OnItemTaken(InventoryItemBase item) { }

	/// <summary>Override to show/hide a reticle ring, glow, or highlight when aimed at.</summary>
	protected virtual void OnTargetedChanged(bool targeted) { }
}
