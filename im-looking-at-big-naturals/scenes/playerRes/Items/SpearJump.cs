using Godot;
using System;

public partial class SpearJump : RigidBody3D
{
    private float _speed = 250;
    public override void _Ready()
    {
        base._Ready();
        ApplyCentralImpulse(-GlobalTransform.Basis.Z.Normalized() * _speed);

    }
    public override void _PhysicsProcess(double delta)
    {
        // Only look toward velocity if the object is actively moving
        if (LinearVelocity.LengthSquared() > 0.001f && !Freeze)
        {
            LookAt(GlobalTransform.Origin + -LinearVelocity.Normalized());
        }
    }
    private void _on_body_entered(Node3D body){
        Freeze = true ;
    }
}
