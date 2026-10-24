using Godot;

public partial class Player : CharacterBody3D
{
    [Export] public float MoveSpeed = 5.0f;
    [Export] public float Acceleration = 8.0f;
    [Export] public float Deceleration = 25.0f;
    [Export] public float RotationSpeed = 10.0f;
    [Export] public float JumpForce = 7.0f;
    [Export] public float JumpDelay = 0.15f;
    [Export] public float Gravity = 20.0f;
    [Export] public float HangTime = 0.3f;
    [Export] public float CoyoteTime = 0.15f;
    [Export] public float DuckSpeed = 2.5f;

    private float jumpTimer = 0.0f;
    private float coyoteTimer = 0.0f;
    private float hangTimer = 0.0f;
    private bool jumpInProgress = false;
    private bool isHovering = false;
    private bool isDucking = false;
    private bool preparingJump = false;

    private CollisionShape3D upperSphere;
    private CollisionShape3D lowerSphere;
    private Node3D graphics;
    private Camera3D camera;

    private int movementMode = 1;


    public override void _Ready()
    {
        upperSphere = GetNode<CollisionShape3D>("UpperSphere");
        lowerSphere = GetNode<CollisionShape3D>("LowerSphere");
        camera = GetViewport().GetCamera3D();
        graphics = GetNode<Node3D>("Graphics");
    }

    // Includes movement and what functions should be called in what order
    public override void _PhysicsProcess(double delta)
    {
        // Each mode determines what movement style the player will have
        if (Input.IsActionJustPressed("modulation_1"))
        {
            movementMode = 1;
        }

        if (Input.IsActionJustPressed("modulation_2"))
        {
            movementMode = 2;
        }

        if (Input.IsActionJustPressed("modulation_3"))
        {
            movementMode = 3;
        }

        Vector2 input = Input.GetVector("move_left","move_right","move_forward","move_backward");

        // Calculate the camera's direction
        Vector3 forward = -camera.GlobalBasis.Z;
        Vector3 right = camera.GlobalBasis.X;

        // Ensures camera angle change doesn't affect movement speed
        forward.Y = 0;
        right.Y = 0;

        // Keeps movement relative to the camera
        forward = forward.Normalized();
        right = right.Normalized();
        Vector3 direction = (right * input.X - forward * input.Y).Normalized();

        float currentSpeed = isDucking ? DuckSpeed : MoveSpeed;
        Vector3 targetVelocity = direction * currentSpeed;
        Vector3 horizontalVelocity = new Vector3(Velocity.X, 0, Velocity.Z);


        // Snappy instant acceleration/deceleration
        if (movementMode == 1)
        {
            horizontalVelocity = targetVelocity;
        }

        // Constant slow acceleration/deceleration
        else if (movementMode == 2)
        {
            if (direction != Vector3.Zero)
            {
                horizontalVelocity = horizontalVelocity.MoveToward(targetVelocity, Acceleration * (float)delta);
            }
            else
            {
                horizontalVelocity = horizontalVelocity.MoveToward(Vector3.Zero, Deceleration * (float)delta);
            }
        }

        // Ease acceleration/deceleration with Lerp instead of MoveToward which provides smoother easing
        else if (movementMode == 3)
        {
            if (direction != Vector3.Zero)
            {
                horizontalVelocity = horizontalVelocity.Lerp(targetVelocity, Acceleration * (float)delta);
            }
            // Deceleration still uses a constant-rate reduction in speed.
            else
            {
                horizontalVelocity = horizontalVelocity.MoveToward(Vector3.Zero, Deceleration * (float)delta);
            }
        }

        // Prevents movement input from overwriting jump or gravity.
        Velocity = new Vector3(horizontalVelocity.X, Velocity.Y, horizontalVelocity.Z);

        // Proper order for movement
        RotateGraphics((float)delta);
        HandleHangTime((float)delta);
        ApplyGravity((float)delta);
        UpdateCoyoteTime((float)delta);
        PrepareJump((float)delta);
        HandleDucking();
        MoveAndSlide();
    }

    // Function makes it so rotation/movement is tied to camera angle
    private void RotateGraphics(float delta)
    {
        Vector3 direction = new Vector3(Velocity.X, 0, Velocity.Z);

        // If the player barely moves then it will face the same direction
        if (direction.LengthSquared() < 0.01f)
        {
            return;
        }

        // Atan2 converts the X and Z direction into a rotation angle.
        float targetAngle = Mathf.Atan2(direction.X, direction.Z);

        Vector3 rotation = GetNode<Node3D>("Graphics").Rotation;

        // LerpAngle chooses the appropriate direction around the circle.
        rotation.Y = Mathf.LerpAngle(rotation.Y, targetAngle, RotationSpeed * delta);

        GetNode<Node3D>("Graphics").Rotation = rotation;
    }

    // Jump anticipation with timers
    private void PrepareJump(float delta)
    {
        if (Input.IsActionJustPressed("jump") && coyoteTimer > 0 && !preparingJump && !isDucking)
        {
            preparingJump = true;
            jumpTimer = JumpDelay;
        }

        // Continue the anticipation 
        if (preparingJump)
        {
            jumpTimer -= delta;

            // Hard coded "animation" for anticiption
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
    }

    // Gravity
    private void ApplyGravity(float delta)
    {
        if (!IsOnFloor())
        {
            Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * delta, Velocity.Z);
        }
    }

    // Hang time checks for when the player stops gaining/losing vertical velocity from gravity
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

    // Coyote time was surprisingly easy to implement, but I know it can end up looking janky
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

    // Crouching
    private void HandleDucking()
    {
        isDucking = Input.IsActionPressed("duck");

        // Ducking "animation"
        if (isDucking)
        {
            graphics.Scale = new Vector3(1, -0.4f, 1);
            graphics.Position = new Vector3(0, -0.4f, 0);
            upperSphere.Position = new Vector3(0, -0.4f, 0);
        }

        // Prevents jump anticipation from being overriden by Vector3.One;
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