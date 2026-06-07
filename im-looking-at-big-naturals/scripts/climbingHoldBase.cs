using Godot;
using System;

public abstract partial class ClimbingHoldBase : GrabbableBaseItem
{
    public override bool RequiresHeldButton => true;

    [Export] private NodePath _grabPointPath;
    private Marker3D _grabPoint;

    public override void _Ready()
    {
        if (_grabPointPath != null && !_grabPointPath.IsEmpty)
            _grabPoint = GetNodeOrNull<Marker3D>(_grabPointPath);
    }

    public override void OnGrabbed(PlayerHand hand)
    {
        // Lock the hand to this hold's world position immediately.
        Vector3 grabPos = _grabPoint != null ? _grabPoint.GlobalPosition : GlobalPosition;
        hand.SetPoseOverride(grabPos);
    }

    public override void WhileHeld(PlayerHand hand, float delta)
    {
        // Re-set every frame in case the hold itself moves (swinging holds, etc.).
        Vector3 grabPos = _grabPoint != null ? _grabPoint.GlobalPosition : GlobalPosition;
        hand.SetPoseOverride(grabPos);

        // ── Your climbing physics here ──────────────────────────────
        // Query both hands' states from the player to determine how many
        // holds are active and compute the resultant body force.
        // See "Player physics" notes below.
    }

    public override void OnDropped(PlayerHand hand)
    {
        hand.SetPoseOverride(null); // release pose, hand returns to rest
    }
}