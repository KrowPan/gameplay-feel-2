using Godot;

public partial class CameraHint : Area3D
{
    [Export] public Vector3 CameraOffset = new Vector3(0, 2, 7);
    [Export] public float FieldOfView = 65.0f;
    [Export] public float TransitionSpeed = 2.0f;
    [Export] public Vector3 CameraRotation = Vector3.Zero;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is CharacterBody3D)
        {
            CameraRig cameraRig = GetTree().Root.FindChild("CameraRig", true, false) as CameraRig;

            if (cameraRig != null)
            {
                cameraRig.SetHint(this);
            }
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is CharacterBody3D)
        {
            CameraRig cameraRig = GetTree().Root.FindChild("CameraRig", true, false) as CameraRig;

            if (cameraRig != null)
            {
                cameraRig.ClearHint(this);
            }
        }
    }
}
