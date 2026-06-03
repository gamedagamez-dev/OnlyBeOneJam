using Godot;
using System;

public partial class player3d : CharacterBody3D
{
	public const float Speed = 5.0f;
	public const float RunSpeed = 10f;
	private const float groundAccel = 40f;
	private const float airAccel = 10f;
	public const float JumpVelocity = 4.5f;
	public const float MouseSensitivity = 0.003f;
	public const float MaxPitchAngle = 85.0f;
	private const float Friction = 35.0f;
	private const float FreelookReturnSpeed = 16.0f;
	private Marker3D _twistPivot;
    private Camera3D _pitchPivot;
	private bool _running = false;
	private bool _crouching = false;
	private bool _freelook = false;
	private bool _unfree = false;

	public override void _Ready()
    {
        // Get references to our structural nodes
        _twistPivot = GetNode<Marker3D>("Marker3D");
        _pitchPivot = GetNode<Camera3D>("Marker3D/Camera3D");

        // Lock and hide the cursor inside the window bounds
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }
	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
		{
			velocity += GetGravity() * (float)delta;
		}

		// Handle Jump.
		if (Input.IsActionJustPressed("jump") && IsOnFloor())
		{
			velocity.Y = JumpVelocity;
		}

		// Get the input direction and handle the movement/deceleration.
		Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
		Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
		if (IsOnFloor())
        {
            if (direction != Vector3.Zero)
            {
                velocity.X = Mathf.MoveToward(velocity.X, direction.X * (Speed + (RunSpeed * Convert.ToInt32(_running))), groundAccel * (float)delta);
                velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * (Speed + (RunSpeed * Convert.ToInt32(_running))), groundAccel * (float)delta);
            }
            else
            {
                velocity.X = Mathf.MoveToward(velocity.X, 0, Friction * (float)delta);
                velocity.Z = Mathf.MoveToward(velocity.Z, 0, Friction * (float)delta);
            }
        }
        else
        {
            // Air momentum: Maintain current XZ velocity, but allow slight steering if input is held
            if (direction != Vector3.Zero)
            {
                velocity.X = Mathf.MoveToward(velocity.X, direction.X * (Speed + (RunSpeed * Convert.ToInt32(_running))), airAccel * (float)delta);
                velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * (Speed + (RunSpeed * Convert.ToInt32(_running))), airAccel * (float)delta);
            }
            // If no input is given in the air, X and Z are untouched to preserve momentum
        }
		

		Velocity = velocity;
		MoveAndSlide();
		if (!_freelook && !Mathf.IsZeroApprox(_twistPivot.Rotation.Y))
   		{
       		Vector3 twistRot = _twistPivot.Rotation;
      		twistRot.Y = Mathf.Lerp(twistRot.Y, 0f, 1f - Mathf.Exp(-FreelookReturnSpeed * (float)delta));
			if (Mathf.Abs(twistRot.Y) < 0.001f){twistRot.Y = 0f;}
      		_twistPivot.Rotation = twistRot;
   		}
	}

	public override void _UnhandledInput(InputEvent @event)
    {
        // Process mouse movement if the cursor is locked
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
			// 1. Check whether the player is in freelook or not
            if(_freelook)
			{
				// 1a. Rotate the character neck pivot left and right (Y axis)
            	_twistPivot.RotateY(-mouseMotion.Relative.X * MouseSensitivity);
			}
			else
			{
				// 1b. Rotate the character body pivot left and right (Y axis)
				RotateY(-mouseMotion.Relative.X * MouseSensitivity);
			}
            // 2. Rotate the camera pitch up and down (X axis)
            _pitchPivot.RotateX(-mouseMotion.Relative.Y * MouseSensitivity);
			
            // 3. Clamp the vertical looking angle to prevent flipping completely upside down
            Vector3 currentRotation = _pitchPivot.Rotation;
            currentRotation.X = Mathf.Clamp(
                currentRotation.X, 
                Mathf.DegToRad(-MaxPitchAngle), 
                Mathf.DegToRad(MaxPitchAngle)
            );
            _pitchPivot.Rotation = currentRotation;
        }
		
		// handle sprint action being held 
		if (@event.IsActionPressed("sprint")){_running = true;}
		// handle sprint action being unheld 
		if (@event.IsActionReleased("sprint")){_running = false;}

		// handle freelook action being held
		if (@event.IsActionPressed("free_look")){_freelook = true;}
		// handle freelook action being unheld
		if (@event.IsActionReleased("free_look")){_freelook = false;}

        // toggles mouse capture off and on when pressing the Escape key
		if (@event.IsActionPressed("ui_cancel"))
		{
			if (Input.MouseMode == Input.MouseModeEnum.Captured)
			{
				Input.MouseMode = Input.MouseModeEnum.Visible;
			}
			else
			{
				Input.MouseMode = Input.MouseModeEnum.Captured;
			}
		}
    }
}
