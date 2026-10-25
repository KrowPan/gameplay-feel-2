using Godot;

public partial class Player : CharacterBody3D
{
    // Movement settings
    [Export] public float MoveSpeed = 5.0f;
    [Export] public float RotationSpeed = 10.0f;
    [Export] public float JumpForce = 7.0f;
    [Export] public float JumpDelay = 0.15f;
    [Export] public float Gravity = 20.0f;
    [Export] public float HangTime = 0.3f;
    [Export] public float CoyoteTime = 0.15f;
    [Export] public float DuckSpeed = 2.5f;
    [Export] public float StickDeadzone = 0.2f;

    private float jumpTimer = 0.0f;
    private float coyoteTimer = 0.0f;
    private float hangTimer = 0.0f;

    private bool jumpInProgress = false;
    private bool isHovering = false;
    private bool isDucking = false;
    private bool preparingJump = false;
    private bool jumpWasPressed = false;

    private CollisionShape3D upperSphere;
    private Node3D graphics;
    private Camera3D camera;
    
    // Lowersphere isn't ever modified so it isn't called upon
    public override void _Ready()
    {
        upperSphere = GetNode<CollisionShape3D>("UpperSphere");
        graphics = GetNode<Node3D>("Graphics");
        camera = GetViewport().GetCamera3D();
    }

    // Handles movement, movement relative to camera and deadzone. Also used to call upon everything in order
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        Vector2 input = new Vector2(Input.GetJoyAxis(0, JoyAxis.LeftX), Input.GetJoyAxis(0, JoyAxis.LeftY));

        // Deadzone check necessary
        if (input.Length() < StickDeadzone)
        {
            input = Vector2.Zero;
        }

        else
        {
            input = input.LimitLength(1.0f);
        }

        // Limit diagonal movement so it is not faster than straight movement
        input = input.LimitLength(1.0f);

        // Used to keep movement on the horizontal plane so camera doesn't affect Y velocity
        Vector3 forward = -camera.GlobalBasis.Z;
        Vector3 right = camera.GlobalBasis.X;
        forward.Y = 0;
        right.Y = 0;
        forward = forward.Normalized();
        right = right.Normalized();

        // Convert movement to be relative to camera
        Vector3 direction = (right * input.X - forward * input.Y).Normalized();

        // Crouch speed
        float speed = isDucking ? DuckSpeed : MoveSpeed;
        Vector3 horizontalVelocity = direction * speed;

        // Update horizontal movement without changing vertical velocity.
        Velocity = new Vector3(horizontalVelocity.X, Velocity.Y, horizontalVelocity.Z);

        // Proper function order, with deltaTime 
        RotateGraphics(dt);
        HandleHangTime(dt);
        ApplyGravity(dt);
        UpdateCoyoteTime(dt);
        PrepareJump(dt);
        HandleDucking();
        MoveAndSlide();
    }

    // Turn the visible model toward its movement direction.
    private void RotateGraphics(float delta)
    {
        Vector3 direction = new Vector3(Velocity.X, 0, Velocity.Z);

        if (direction.LengthSquared() < 0.01f)
        {
            return;
        }

        float targetAngle = Mathf.Atan2(direction.X, direction.Z);
        Vector3 rotation = graphics.Rotation;
        rotation.Y = Mathf.LerpAngle(rotation.Y, targetAngle, RotationSpeed * delta);
        graphics.Rotation = rotation;
    }

    // Jump anticipation "animation" and delaying jump for it
    private void PrepareJump(float delta)
    {
        bool jumpPressed = Input.IsJoyButtonPressed(0, JoyButton.A);

        // Start a jump only when the button is newly pressed (prevents holding button to jump)
        if (jumpPressed && !jumpWasPressed && coyoteTimer > 0 && !preparingJump && !isDucking)
        {
            preparingJump = true;
            jumpTimer = JumpDelay;
        }

        jumpWasPressed = jumpPressed;

        if (!preparingJump)
        {
            return;
        }

        jumpTimer -= delta;

        // "Animation"
        Vector3 scale = graphics.Scale;
        scale.Y = 0.7f;
        graphics.Scale = scale;

        if (jumpTimer <= 0.0f)
        {
            Velocity = new Vector3(Velocity.X, JumpForce, Velocity.Z);

            jumpInProgress = true;
            preparingJump = false;
            graphics.Scale = Vector3.One;
        }
    }

    // Simple gravity
    private void ApplyGravity(float delta)
    {
        if (!IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * delta, Velocity.Z);
        }
    }

    // Apex of a jump is calculated by checking if the player stops gaining or losing vertical velocity
    private void HandleHangTime(float delta)
    {
        if (jumpInProgress && !isHovering && Velocity.Y <= 0)
        {
            isHovering = true;
            hangTimer = HangTime;
        }

        if (isHovering)
        {
            hangTimer -= delta;
            Velocity = new Vector3(Velocity.X, 0, Velocity.Z);

            if (hangTimer <= 0)
            {
                isHovering = false;
                jumpInProgress = false;
            }
        }
    }

    // Simple coyote implementation
    private void UpdateCoyoteTime(float delta)
    {
        if (IsOnFloor())
        {
            coyoteTimer = CoyoteTime;
        }

        else
        {
            coyoteTimer -= delta;
        }
    }

    // Also known as crouching, scales the player with an "animation" too and slows speed
    private void HandleDucking()
    {
        isDucking = Input.IsJoyButtonPressed(0, JoyButton.B);

        if (isDucking)
        {
            graphics.Scale = new Vector3(1, -0.4f, 1);
            graphics.Position = new Vector3(0, -0.4f, 0);
            upperSphere.Position = new Vector3(0, -0.4f, 0);
        }

        else
        {
            if (!preparingJump)
            {
                graphics.Scale = Vector3.One;
                graphics.Position = Vector3.Zero;
            }

            upperSphere.Position = new Vector3(0, 0.48f, 0);
        }
    }
}