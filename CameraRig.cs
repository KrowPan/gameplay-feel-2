
using Godot;

public partial class CameraRig : Node3D
{
    [Export] public Node3D FacingTarget;
    [Export] public CharacterBody3D Target;
    [Export] public float FollowSpeed = 5.0f;
    [Export] public Vector3 TargetOffset = new Vector3(0, 1.5f, 0);

    [Export] public float OrbitSpeed = 2.5f;
    [Export] public float MinPitch = -20.0f;
    [Export] public float MaxPitch = 60.0f;
    [Export] public float CameraDistance = 5.0f;

    [Export] public float AlignSpeed = 1.0f;
    [Export] public float InputDeadzone = 0.1f;
    [Export] public float CameraCollisionPadding = 0.3f;
    [Export] public float CollisionInSpeed = 20.0f;
    [Export] public float CollisionOutSpeed = 5.0f;
    [Export] public float SidewaysAvoidanceDistance = 1.0f;
    [Export] public float NormalFieldOfView = 75.0f;

    private float currentFieldOfView;
    private float yaw = 0.0f;
    private float pitch = 15.0f;

    private RayCast3D cameraRay;
    private RayCast3D whiskerLeft;
    private RayCast3D whiskerRight;

    private Vector3 currentCameraPosition;
    private CameraHint activeHint;
    private Vector3 normalCameraOffset;
    private Camera3D camera;

    public override void _Ready()
    {
        camera = GetNode<Camera3D>("Camera3D");

        cameraRay = GetNode<RayCast3D>("RayCast3D");
        whiskerLeft = GetNode<RayCast3D>("WhiskerLeft");
        whiskerRight = GetNode<RayCast3D>("WhiskerRight");

        currentCameraPosition = camera.GlobalPosition;
        normalCameraOffset = new Vector3(0, 2, 7);
        currentFieldOfView = camera.Fov;
    }

    public void SetHint(CameraHint hint)
    {
        activeHint = hint;
    }

    public void ClearHint(CameraHint hint)
    {
        if (activeHint == hint)
        {
            activeHint = null;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Target == null)
        {
            return;
        }

        Vector2 lookInput = Input.GetVector( "look_left", "look_right", "look_up", "look_down");

        Vector3 movement = Target.Velocity;
        movement.Y = 0;

        if (lookInput.Length() < InputDeadzone && movement.Length() > 0.1f && FacingTarget != null)
        {
            float targetYaw = FacingTarget.GlobalRotation.Y - Mathf.Pi;
            yaw = Mathf.LerpAngle(yaw, targetYaw, 1.0f - Mathf.Exp(-AlignSpeed * (float)delta));
        }

        yaw -= lookInput.X * OrbitSpeed * (float)delta;
        pitch -= lookInput.Y * OrbitSpeed * (float)delta;

        pitch = Mathf.Clamp( pitch, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));

        Vector3 targetPosition = Target.GlobalPosition + TargetOffset;

        GlobalPosition = GlobalPosition.Lerp( targetPosition, 1.0f - Mathf.Exp(-FollowSpeed * (float)delta));

        Vector3 cameraOffset = new Vector3( Mathf.Sin(yaw) * Mathf.Cos(pitch) * 
        CameraDistance, Mathf.Sin(pitch) * CameraDistance, Mathf.Cos(yaw) * Mathf.Cos(pitch) * CameraDistance);

        Vector3 desiredOffset = activeHint != null ? activeHint.CameraOffset : normalCameraOffset;

        float desiredFov = activeHint != null ? activeHint.FieldOfView : NormalFieldOfView;

        Vector3 desiredCameraPosition = activeHint != null ? GlobalPosition + desiredOffset : GlobalPosition + cameraOffset;

        currentFieldOfView = Mathf.Lerp(currentFieldOfView, desiredFov, 1.0f - 
        Mathf.Exp((activeHint != null ? -activeHint.TransitionSpeed : -2.0f) * (float)delta));

        camera.Fov = currentFieldOfView;

        // Set up the three rays from the camera rig.
        Vector3 side = camera.GlobalBasis.X * 0.5f;

        cameraRay.GlobalPosition = GlobalPosition;
        whiskerLeft.GlobalPosition = GlobalPosition;
        whiskerRight.GlobalPosition = GlobalPosition;

        // Keep the rays aligned with world coordinates.
        cameraRay.GlobalRotation = Vector3.Zero;
        whiskerLeft.GlobalRotation = Vector3.Zero;
        whiskerRight.GlobalRotation = Vector3.Zero;

        cameraRay.TargetPosition = cameraRay.ToLocal(desiredCameraPosition);
        whiskerLeft.TargetPosition = whiskerLeft.ToLocal( desiredCameraPosition - side);
        whiskerRight.TargetPosition = whiskerRight.ToLocal(desiredCameraPosition + side);

        cameraRay.ForceRaycastUpdate();
        whiskerLeft.ForceRaycastUpdate();
        whiskerRight.ForceRaycastUpdate();

        // Find the closest obstacle detected by any ray.
        float safeDistance = GlobalPosition.DistanceTo(desiredCameraPosition);

        Vector3 direction = (desiredCameraPosition - GlobalPosition).Normalized();

        if (cameraRay.IsColliding())
        {
            safeDistance = Mathf.Min(safeDistance, GlobalPosition.DistanceTo(cameraRay.GetCollisionPoint()));
        }

        if (whiskerLeft.IsColliding())
        {
            safeDistance = Mathf.Min(safeDistance, GlobalPosition.DistanceTo(whiskerLeft.GetCollisionPoint()));
        }

        if (whiskerRight.IsColliding())
        {
            safeDistance = Mathf.Min(safeDistance, GlobalPosition.DistanceTo(whiskerRight.GetCollisionPoint()));
        }

        if (cameraRay.IsColliding())
        {
            desiredCameraPosition = GlobalPosition + direction * Mathf.Max(0.5f, safeDistance - CameraCollisionPadding);
        }
        else if (whiskerLeft.IsColliding() || whiskerRight.IsColliding())
        {
            Vector3 sideDirection = camera.GlobalBasis.X;

            if (whiskerLeft.IsColliding())
            {
                desiredCameraPosition += sideDirection * SidewaysAvoidanceDistance;
            }
            else
            {
                desiredCameraPosition -= sideDirection * SidewaysAvoidanceDistance;
            }
        }

        // Smooth the camera's movement toward or away from obstacles.
        bool hitObstacle = cameraRay.IsColliding() || whiskerLeft.IsColliding() || whiskerRight.IsColliding();

        float collisionSpeed = hitObstacle ? CollisionInSpeed : CollisionOutSpeed;

        currentCameraPosition = currentCameraPosition.Lerp(desiredCameraPosition, 1.0f - Mathf.Exp(-collisionSpeed * (float)delta));

        camera.GlobalPosition = currentCameraPosition;

        // Apply hint rotation, or aim at the player normally.
        if (activeHint != null)
        {
            Vector3 desiredRotation = new Vector3( Mathf.DegToRad(activeHint.CameraRotation.X), Mathf.DegToRad(activeHint.CameraRotation.Y), Mathf.DegToRad(activeHint.CameraRotation.Z));

            float rotationWeight = 1.0f - Mathf.Exp(-activeHint.TransitionSpeed * (float)delta);

            camera.GlobalRotation = camera.GlobalRotation.Lerp( desiredRotation, rotationWeight);
        }
        else
        {
            camera.LookAt(GlobalPosition, Vector3.Up);
        }
    }
}
