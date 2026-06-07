using Godot;
using System;

public partial class SpearGround : InventoryItemBase
{
    private PackedScene SpearInstancer = GD.Load<PackedScene>("res://scenes/playerRes/Items/spearGen.tscn");
    public override void OnActivated(PlayerHand hand) {
        RigidBody3D temPrj = SpearInstancer.Instantiate<RigidBody3D>();
        AddSibling(temPrj);
        temPrj.Rotation = Rotation;
        temPrj.Position = new Vector3(Position.X,Position.Y,Position.Z-1.2f);
        temPrj.Reparent(temPrj.GetParent().GetParent().GetParent().GetParent().GetParent().GetParent().GetParent().GetParent());
        hand.Drop();
        QueueFree();
    }
}
