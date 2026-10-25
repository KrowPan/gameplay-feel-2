using Godot;

public partial class CameraRig : Node3D
{
    [Export] public Node3D FacingTarget;
    [Export] public CharacterBody3D Target;
    [Export] public float FollowSpeed = 5.0f;
    [Export] public Vector3 TargetOffset = new Vector3(0, 1.5f, 0);
    [Export] public float CameraDistance = 7.0f;
    [Export] public float OrbitSpeed = 2.5f;
    [Export] public float MinPitch = -20.0f;
    [Export] public float MaxPitch = 60.0f;
    [Export] public float AlignSpeed = 1.0f;
    [Export] public float InputDeadzone = 0.1f;
    [Export] public float CameraCollisionPadding = 0.3f;
    [Export] public float CollisionInSpeed = 20.0f;
    [Export] public float CollisionOutSpeed = 5.0f;
    [Export] public float SidewaysAvoidanceDistance = 1.0f;
    [Export] public float NormalFieldOfView = 75.0f;
    [Export] public float CameraTransitionDuration = 0.8f;

    private Camera3D camera;
    private RayCast3D cameraRay;
    private RayCast3D whiskerLeft;
    private RayCast3D whiskerRight;
    private float yaw = 0.0f;
    private float pitch = Mathf.DegToRad(15.0f);
    private float currentFieldOfView;
    private Vector3 currentCameraPosition;
    private CameraHint activeHint;
    private Vector3 transitionStartPosition;
    private Quaternion transitionStartRotation;
    private float transitionStartFov;
    private float transitionTimer = 0.0f;
    private bool isTransitioning = false;

    // Instantializing camera nodes
    public override void _Ready()
    {
        camera = GetNode<Camera3D>("Camera3D");
        cameraRay = GetNode<RayCast3D>("RayCast3D");
        whiskerLeft = GetNode<RayCast3D>("WhiskerLeft");
        whiskerRight = GetNode<RayCast3D>("WhiskerRight");
        currentCameraPosition = camera.GlobalPosition;
        currentFieldOfView = camera.Fov;
    }

    // Activate camerahint settings
    public void SetHint(CameraHint hint)
    {
        if (activeHint == hint)
        {
            return;
        }
        activeHint = hint;
        StartCameraTransition();
    }

    // Deactivate
    public void ClearHint(CameraHint hint)
    {
        if (activeHint != hint)
            return;

        activeHint = null;
        StartCameraTransition();
    }

    // Save the current camera state so the transition starts smoothly
    private void StartCameraTransition()
    {
        transitionStartPosition = camera.GlobalPosition;
        transitionStartRotation = camera.GlobalBasis.GetRotationQuaternion();
        transitionStartFov = camera.Fov;
        transitionTimer = 0.0f;
        isTransitioning = true;
    }

    // Camera smoothness, raycasts/whiskers, joystick adjustable, transitions 
    public override void _PhysicsProcess(double delta)
    {
        if (Target == null)
        {
            return;
        }

        float dt = (float)delta;

        // Read camera orbit from the right stick.
        Vector2 lookInput = new Vector2(Input.GetJoyAxis(0, JoyAxis.RightX), Input.GetJoyAxis(0, JoyAxis.RightY));

        // Deadzone check
        if (lookInput.Length() < InputDeadzone)lookInput = Vector2.Zero;

        // Camera self adjusts to face player direction when not used
        Vector3 movement = Target.Velocity;
        movement.Y = 0;

        if (lookInput == Vector2.Zero && movement.Length() > 0.1f && FacingTarget != null)
        {
            float targetYaw = FacingTarget.GlobalRotation.Y - Mathf.Pi;

            yaw = Mathf.LerpAngle(yaw, targetYaw, 1.0f - Mathf.Exp(-AlignSpeed * dt));
        }

        yaw += lookInput.X * OrbitSpeed * dt;
        pitch -= lookInput.Y * OrbitSpeed * dt;

        // Pitch limits the camera up and down angle
        pitch = Mathf.Clamp(pitch, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));

        // Smoothly follow a point above the player.
        Vector3 targetPosition = Target.GlobalPosition + TargetOffset;

        GlobalPosition = GlobalPosition.Lerp(targetPosition, 1.0f - Mathf.Exp(-FollowSpeed * dt));

        // Calculate the camera position from the orbit angles.
        Vector3 cameraOffset = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch) * CameraDistance, Mathf.Sin(pitch)
        * CameraDistance, Mathf.Cos(yaw) * Mathf.Cos(pitch) * CameraDistance);

        Vector3 desiredCameraPosition = activeHint != null ? GlobalPosition + activeHint.CameraOffset : GlobalPosition + cameraOffset;

        float desiredFov = activeHint != null ? activeHint.FieldOfView : NormalFieldOfView;

        // Calculate ease start and end transition 
        float progress = 1.0f;

        if (isTransitioning)
        {
            transitionTimer += dt;
            progress = Mathf.Clamp(transitionTimer / Mathf.Max(CameraTransitionDuration, 0.01f), 0.0f, 1.0f);
        }

        float easedProgress = progress * progress * (3.0f - 2.0f * progress);

        if (isTransitioning)
        {
            desiredCameraPosition = transitionStartPosition.Lerp(desiredCameraPosition, easedProgress);
            currentFieldOfView = Mathf.Lerp(transitionStartFov, desiredFov, easedProgress);
        }
        else
        {
            currentFieldOfView = desiredFov;
        }

        camera.Fov = currentFieldOfView;

        // Work out the sideways direction for the whisker rays
        Vector3 side = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));
        Vector3 leftEnd = desiredCameraPosition - side * 0.5f;
        Vector3 rightEnd = desiredCameraPosition + side * 0.5f;

        // All rays start at the rig and point toward the desired camera position.
        cameraRay.GlobalPosition = GlobalPosition;
        whiskerLeft.GlobalPosition = GlobalPosition;
        whiskerRight.GlobalPosition = GlobalPosition;

        cameraRay.GlobalRotation = Vector3.Zero;
        whiskerLeft.GlobalRotation = Vector3.Zero;
        whiskerRight.GlobalRotation = Vector3.Zero;

        cameraRay.TargetPosition = cameraRay.ToLocal(desiredCameraPosition);
        whiskerLeft.TargetPosition = whiskerLeft.ToLocal(leftEnd);
        whiskerRight.TargetPosition = whiskerRight.ToLocal(rightEnd);

        // Update the rays immediately
        cameraRay.ForceRaycastUpdate();
        whiskerLeft.ForceRaycastUpdate();
        whiskerRight.ForceRaycastUpdate();

        bool mainHit = cameraRay.IsColliding();
        bool leftHit = whiskerLeft.IsColliding();
        bool rightHit = whiskerRight.IsColliding();

        Vector3 safePosition = desiredCameraPosition;

        // Pull the camera inward if the main ray hits an obstacle
        if (mainHit)
        {
            float hitDistance = GlobalPosition.DistanceTo(cameraRay.GetCollisionPoint());

            float safeDistance = Mathf.Max(0.5f, hitDistance - CameraCollisionPadding);

            Vector3 direction = (desiredCameraPosition - GlobalPosition).Normalized();

            safePosition = GlobalPosition + direction * safeDistance;
        }

        // Shift away from a side obstacle if only one whisker hits
        if (leftHit && !rightHit)
        {
            safePosition += side * SidewaysAvoidanceDistance;
        }
        else if (rightHit && !leftHit)
        {
            safePosition -= side * SidewaysAvoidanceDistance;
        }

        // If a whisker hits the ground, raise the camera and pull it inward
        bool groundHit = (leftHit && whiskerLeft.GetCollisionNormal().Y > 0.5f)
        || (rightHit && whiskerRight.GetCollisionNormal().Y > 0.5f);

        if (groundHit)
        {
            safePosition.Y += SidewaysAvoidanceDistance;
            safePosition = GlobalPosition + (safePosition - GlobalPosition) * 0.8f;
        }

        // Move inward quickly when blocked and return slowly when clear
        bool hitObstacle = mainHit || leftHit || rightHit;
        float collisionSpeed = hitObstacle ? CollisionInSpeed : CollisionOutSpeed;

        currentCameraPosition = currentCameraPosition.Lerp(safePosition, 1.0f - Mathf.Exp(-collisionSpeed * dt));

        camera.GlobalPosition = currentCameraPosition;

        // Hints will use a fixed rotation
        Quaternion desiredRotation;

        if (activeHint != null)
        {
            Vector3 rotationInRadians = new Vector3(Mathf.DegToRad(activeHint.CameraRotation.X),
            Mathf.DegToRad(activeHint.CameraRotation.Y),  Mathf.DegToRad(activeHint.CameraRotation.Z));

            desiredRotation = Basis.FromEuler(rotationInRadians).GetRotationQuaternion();
        }
        else
        {
            camera.LookAt(GlobalPosition, Vector3.Up);
            desiredRotation = camera.GlobalBasis.GetRotationQuaternion();
        }

        // Blend the rotation during hint transitions.
        Quaternion finalRotation = isTransitioning ? transitionStartRotation.Slerp(desiredRotation, easedProgress) : desiredRotation;

        camera.GlobalBasis = new Basis(finalRotation);

        if (isTransitioning && progress >= 1.0f)isTransitioning = false;
    }
}