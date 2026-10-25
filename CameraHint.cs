using Godot;

public partial class CameraHint : Area3D
{
    // Camera settings used while the player is inside this area.
    [Export] public Vector3 CameraOffset = new Vector3(0, 2, 7);
    [Export] public float FieldOfView = 65.0f;
    [Export] public Vector3 CameraRotation = Vector3.Zero;

    private CameraRig cameraRig;

    // Instantialize everything
    public override void _Ready()
    {
        cameraRig = GetTree().Root.FindChild("CameraRig", true, false) as CameraRig;
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    // Tell the camera rig to use this hint when player enters
    private void OnBodyEntered(Node3D body)
    {
        if (body is CharacterBody3D && cameraRig != null)
        {
            cameraRig.SetHint(this);
        }
    }

    // Return to the normal camera when the character leaves this hint.
    private void OnBodyExited(Node3D body)
    {
        if (body is CharacterBody3D && cameraRig != null)
        {
            cameraRig.ClearHint(this);
        }
    }
}