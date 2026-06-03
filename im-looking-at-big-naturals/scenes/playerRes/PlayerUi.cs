using Godot;
using System;

public partial class PlayerUi : Control
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		// Get the full 3D velocity vector
        Vector3 fullVelocity = GetParent<player3d>().Velocity;

        // Extract only the horizontal (X and Z) components
        float xzSpeed = new Vector2(fullVelocity.X, fullVelocity.Z).Length();

        // Update the UI label
        GetNode<Label>("VelBox").Text = xzSpeed.ToString();
	}
}
