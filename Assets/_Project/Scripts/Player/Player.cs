using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Underbrew.Core;

public class Player : MonoBehaviour
{
    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }
    public PlayerInputSet input { get; private set; }
    public string CurrentStateName => stateMachine?.currentState?.GetType().Name ?? "None";
    public float CurrentCoyoteTime => coyoteTimeCounter;
    public float CurrentJumpBuffer => jumpBufferCounter;
    public float CurrentWallJumpLock => wallJumpControlLockCounter;
    public float CurrentDashCooldown => dashCooldownTimer;
    public bool IsJumpHeld => jumpHeld;

    private StateMachine stateMachine;
    private System.Action<InputAction.CallbackContext> movementPerformedHandler;
    private System.Action<InputAction.CallbackContext> movementCanceledHandler;



    public Player_IdleState idleState { get; private set; }
    public Player_MoveState moveState { get; private set; }
    public Player_JumpState jumpState { get; private set; }
    public Player_FallState fallState { get; private set; }
    public Player_WallSlideState wallSlideState { get; private set; }
    public Player_WallJumpState wallJumpState { get; private set; }
    public Player_DashState dashState { get; private set; }
    public Player_BasicAttackState basicAttackState { get; private set; }
    public Player_JumpAttackState jumpAttackState { get; private set; }
    public Player_InteractState interactState { get; private set; }

    [Header("Attack details")]
    public bool allowAttackInput = false;
    public Vector2[] attackVelocity;
    public Vector2 jumpAttackVelocity;
    public float attackVelocityDuration = .1f;
    public float comboResetTime = 1;
    private Coroutine queuedAttackCo;



    [Header("Movement details")]
    [Min(0f)] public float moveSpeed;
    [Min(0f)] public float jumpForce = 5;
    public Vector2 wallJumpForce;

    [Range(0,1)]
    public float inAirMoveMultiplier = .7f; // Should be from 0 to 1;
    [Range(0,1)]
    public float wallSlideSlowMultiplier = .7f;
    [Space]
    [Min(0f)] public float dashDuration = .25f;
    [Min(0f)] public float dashSpeed = 20;
    [Min(0f)] public float dashCooldown = 0.4f;

    [Header("Movement Feel")]
    [Min(0f)] public float groundAcceleration = 90f;
    [Min(0f)] public float groundDeceleration = 110f;
    [Min(0f)] public float airAcceleration = 55f;
    [Min(0f)] public float airDeceleration = 45f;
    [Min(0f)] public float coyoteTime = 0.12f;
    [Min(0f)] public float jumpBufferTime = 0.12f;
    [Min(0f)] public float jumpCutGravityMultiplier = 2f;
    [Min(0f)] public float fallGravityMultiplier = 2.2f;
    [Min(0f)] public float jumpHangGravityMultiplier = 0.8f;
    [Min(0f)] public float jumpHangVelocityThreshold = 1.5f;
    [Min(0f)] public float maxFallSpeed = 18f;
    [Min(0f)] public float maxFastFallSpeed = 24f;
    [Min(0f)] public float wallSlideSpeed = 3f;
    [Min(0f)] public float wallJumpControlLockTime = 0.12f;

    [Header("Interaction details")]
    [Min(0f)] public float interactHoldDuration = .5f;
    [Range(0,1)]
    public float interactMoveSlowMultiplier = .2f;



    private bool facingRight = true;
    public int facingDir { get; private set; } = 1;
    public Vector2 moveInput { get; private set; }

    [Header("Collision detection")]
    [SerializeField] private float groundCheckDistance;
    [SerializeField] private float wallCheckDistance;
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] private Transform primaryWallCheck;
    [SerializeField] private Transform secondaryWallCheck;
    public bool groundDetected { get; private set; }
    public bool wallDetected { get; private set; }
    public IInteractable currentInteractable { get; private set; }

    private static Player instance;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private float defaultGravityScale;
    private bool jumpHeld;
    private float wallJumpControlLockCounter;
    private float dashCooldownTimer;
    private bool gameplayInputSuppressedByDialogue;

    private void Awake()
    {
        
        
        // ----- DUPLICATE CHECK + PERSISTENCE -----
        if (instance != null && instance != this)
        {
        
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        
        DontDestroyOnLoad(gameObject);

        if (GetComponent<PlayerFootstepAudio>() == null)
            gameObject.AddComponent<PlayerFootstepAudio>();

        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        defaultGravityScale = rb.gravityScale;
        

        stateMachine = new StateMachine();
        input = new PlayerInputSet();

        movementPerformedHandler = OnMovementPerformed;
        movementCanceledHandler = OnMovementCanceled;
        

        idleState = new Player_IdleState(this, stateMachine, "idle");
        moveState = new Player_MoveState(this, stateMachine, "move");
        jumpState = new Player_JumpState(this, stateMachine, "jumpFall");
        fallState = new Player_FallState(this, stateMachine, "jumpFall");
        wallSlideState = new Player_WallSlideState(this, stateMachine, "wallSlide");
        wallJumpState = new Player_WallJumpState(this, stateMachine, "jumpFall");
        dashState = new Player_DashState(this, stateMachine, "dash");
        basicAttackState = new Player_BasicAttackState(this, stateMachine, "basicAttack");
        jumpAttackState = new Player_JumpAttackState(this, stateMachine, "jumpAttack");
        interactState = new Player_InteractState(this, stateMachine, "idle");
        
    }

    private void OnEnable()
    {
        if (input != null)
        {
            input.Enable();
            input.Player.Movement.performed += movementPerformedHandler;
            input.Player.Movement.canceled += movementCanceledHandler;
            RefreshHeldInput();
        }
    }

    private void OnDisable()
    {
        if (input != null)
        {
            input.Player.Movement.performed -= movementPerformedHandler;
            input.Player.Movement.canceled -= movementCanceledHandler;
            input.Disable();
            moveInput = Vector2.zero;
        }
    }

    private void Start()
    {
        stateMachine.Initialize(idleState);
    }

    private void Update()
    {
        UpdateDialogueInputSuppression();

        HandleCollisionDetection();

        if (gameplayInputSuppressedByDialogue)
        {
            moveInput = Vector2.zero;
            jumpBufferCounter = 0f;
            jumpHeld = false;

            if (rb != null)
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            return;
        }

        UpdateJumpInput();
        UpdateJumpTimers();
        UpdateDashCooldown();
        HandleInteractionInput();
        stateMachine.UpdateActiveState();
        ApplyBetterJumpPhysics();
    }

    private void HandleInteractionInput()
    {
        if (currentInteractable == null)
            return;

        if (stateMachine.currentState == interactState)
            return;

        if (input.Player.Interact.WasPressedThisFrame())
        {
            interactState.Setup(currentInteractable, stateMachine.currentState);
            stateMachine.ChangeState(interactState);
        }
    }

    public void SetCurrentInteractable(IInteractable interactable)
    {
        currentInteractable = interactable;
    }

    public void RefreshHeldInput()
    {
        if (input == null)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = input.Player.Movement.ReadValue<Vector2>();
    }

    public void ApplyHorizontalMovement(float inputX, bool grounded)
    {
        if (grounded == false && wallJumpControlLockCounter > 0f)
        {
            return;
        }

        float targetSpeed = inputX * moveSpeed;
        if (grounded == false && IsPressingIntoWall(inputX))
        {
            targetSpeed = 0f;
        }

        bool hasMoveInput = Mathf.Abs(inputX) > 0.01f;
        float acceleration = grounded
            ? (hasMoveInput ? groundAcceleration : groundDeceleration)
            : (hasMoveInput ? airAcceleration : airDeceleration);

        float newXVelocity = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, acceleration * Time.deltaTime);
        SetVelocity(newXVelocity, rb.linearVelocity.y);
    }

    public bool ConsumeBufferedGroundJump()
    {
        if (jumpBufferCounter <= 0f || CanGroundJump() == false)
        {
            return false;
        }

        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
        return true;
    }

    public void PerformJump()
    {
        SetVelocity(rb.linearVelocity.x, jumpForce);
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
    }

    public void ConsumeJumpBuffer()
    {
        jumpBufferCounter = 0f;
    }

    public void BeginWallJumpLock()
    {
        wallJumpControlLockCounter = wallJumpControlLockTime;
    }

    public bool IsDashReady()
    {
        return dashCooldownTimer <= 0f;
    }

    public bool CanUseAttackInput()
    {
        return allowAttackInput;
    }

    public void ConsumeDash()
    {
        dashCooldownTimer = dashCooldown;
    }

    public void ApplyWallSlideMovement()
    {
        float clampedFallSpeed = Mathf.Max(rb.linearVelocity.y, -wallSlideSpeed);
        rb.linearVelocity = new Vector2(0f, clampedFallSpeed);
    }

    public bool CanWallSlide()
    {
        return groundDetected == false
            && wallDetected
            && Mathf.Abs(moveInput.x) > 0.01f
            && Mathf.Sign(moveInput.x) == facingDir;
    }

    public bool IsPressingIntoWall(float inputX)
    {
        return wallDetected
            && Mathf.Abs(inputX) > 0.01f
            && Mathf.Sign(inputX) == facingDir;
    }

    public void EnterAttackStateWithDelay()
    {
        if (!CanUseAttackInput())
            return;

        if (queuedAttackCo != null)
            StopCoroutine(queuedAttackCo);

        queuedAttackCo = StartCoroutine(EnterAttackStateWithDelayCo());
    }

    private IEnumerator EnterAttackStateWithDelayCo()
    {
        yield return new WaitForEndOfFrame();
        stateMachine.ChangeState(basicAttackState);
    }

    public void CallAnimationTrigger()
    {
        stateMachine.currentState.CallAnimationTrigger();
    }

    public void SetVelocity(float xVelocity, float yVelocity)
    {
        rb.linearVelocity = new Vector2(xVelocity, yVelocity);
        HandleFlip(xVelocity);
    }

    private void HandleFlip(float xVelcoity)
    {
        if (xVelcoity > 0 && facingRight == false)
            Flip();
        else if (xVelcoity < 0 && facingRight)
            Flip();
    }

    public void Flip()
    {
        transform.Rotate(0, 180, 0);
        facingRight = !facingRight;
        facingDir = facingDir * -1;
    }

    private void HandleCollisionDetection()
    {
        groundDetected = Physics2D.Raycast(transform.position, Vector2.down, groundCheckDistance, whatIsGround);
        wallDetected = Physics2D.Raycast(primaryWallCheck.position, Vector2.right * facingDir, wallCheckDistance, whatIsGround)
                    && Physics2D.Raycast(secondaryWallCheck.position, Vector2.right * facingDir, wallCheckDistance, whatIsGround);
    }

    private void UpdateJumpInput()
    {
        if (input == null)
        {
            jumpHeld = false;
            return;
        }

        if (input.Player.Jump.WasPressedThisFrame())
        {
            jumpBufferCounter = jumpBufferTime;
        }

        jumpHeld = input.Player.Jump.IsPressed();
    }

    private void UpdateJumpTimers()
    {
        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        if (wallJumpControlLockCounter > 0f)
        {
            wallJumpControlLockCounter -= Time.deltaTime;
        }

        if (groundDetected)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else if (coyoteTimeCounter > 0f)
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
    }

    private void UpdateDashCooldown()
    {
        if (dashCooldownTimer <= 0f)
            return;

        dashCooldownTimer -= Time.deltaTime;
    }

    private bool CanGroundJump()
    {
        return groundDetected || coyoteTimeCounter > 0f;
    }

    private void ApplyBetterJumpPhysics()
    {
        if (rb == null || stateMachine.currentState == dashState)
        {
            return;
        }

        rb.gravityScale = defaultGravityScale;

        Vector2 velocity = rb.linearVelocity;
        bool isFastFalling = moveInput.y < -0.5f;

        if (velocity.y > 0f && jumpHeld == false)
        {
            velocity.y += Physics2D.gravity.y * defaultGravityScale * (jumpCutGravityMultiplier - 1f) * Time.deltaTime;
        }
        else if (groundDetected == false && Mathf.Abs(velocity.y) < jumpHangVelocityThreshold && jumpHeld)
        {
            velocity.y += Physics2D.gravity.y * defaultGravityScale * (jumpHangGravityMultiplier - 1f) * Time.deltaTime;
        }
        else if (velocity.y < 0f)
        {
            float fallMultiplier = isFastFalling ? fallGravityMultiplier + 0.35f : fallGravityMultiplier;
            velocity.y += Physics2D.gravity.y * defaultGravityScale * (fallMultiplier - 1f) * Time.deltaTime;
        }

        float fallSpeedLimit = isFastFalling ? maxFastFallSpeed : maxFallSpeed;
        velocity.y = Mathf.Max(velocity.y, -fallSpeedLimit);
        rb.linearVelocity = velocity;
    }

    private void OnMovementPerformed(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnMovementCanceled(InputAction.CallbackContext context)
    {
        moveInput = Vector2.zero;
    }

    private void UpdateDialogueInputSuppression()
    {
        var shouldSuppressGameplayInput = DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive;

        if (shouldSuppressGameplayInput == gameplayInputSuppressedByDialogue)
            return;

        gameplayInputSuppressedByDialogue = shouldSuppressGameplayInput;

        if (gameplayInputSuppressedByDialogue)
        {
            SetGameplayInputEnabled(false);
            moveInput = Vector2.zero;
            jumpBufferCounter = 0f;
            jumpHeld = false;
            ForceIdleStateDuringDialogueSuppression();
            return;
        }

        SetGameplayInputEnabled(true);
        RefreshHeldInput();
    }

    private void ForceIdleStateDuringDialogueSuppression()
    {
        if (stateMachine == null || idleState == null)
            return;

        if (stateMachine.currentState != idleState)
            stateMachine.ChangeState(idleState);

        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void SetGameplayInputEnabled(bool enabled)
    {
        if (input == null)
            return;

        input.Player.Movement.performed -= movementPerformedHandler;
        input.Player.Movement.canceled -= movementCanceledHandler;

        if (enabled)
        {
            input.Enable();
            input.Player.Movement.performed += movementPerformedHandler;
            input.Player.Movement.canceled += movementCanceledHandler;
        }
        else
        {
            input.Disable();
        }
    }


    private void OnDrawGizmos()
    {
        Gizmos.DrawLine(transform.position, transform.position + new Vector3(0, -groundCheckDistance));
        Gizmos.DrawLine(primaryWallCheck.position, primaryWallCheck.position + new Vector3(wallCheckDistance * facingDir, 0));
        Gizmos.DrawLine(secondaryWallCheck.position, secondaryWallCheck.position + new Vector3(wallCheckDistance * facingDir, 0));
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        jumpForce = Mathf.Max(0f, jumpForce);
        dashDuration = Mathf.Max(0f, dashDuration);
        dashSpeed = Mathf.Max(0f, dashSpeed);
        dashCooldown = Mathf.Max(0f, dashCooldown);

        groundAcceleration = Mathf.Max(0f, groundAcceleration);
        groundDeceleration = Mathf.Max(0f, groundDeceleration);
        airAcceleration = Mathf.Max(0f, airAcceleration);
        airDeceleration = Mathf.Max(0f, airDeceleration);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
        jumpCutGravityMultiplier = Mathf.Max(0f, jumpCutGravityMultiplier);
        fallGravityMultiplier = Mathf.Max(0f, fallGravityMultiplier);
        jumpHangGravityMultiplier = Mathf.Max(0f, jumpHangGravityMultiplier);
        jumpHangVelocityThreshold = Mathf.Max(0f, jumpHangVelocityThreshold);
        maxFallSpeed = Mathf.Max(0f, maxFallSpeed);
        maxFastFallSpeed = Mathf.Max(maxFallSpeed, maxFastFallSpeed);
        wallSlideSpeed = Mathf.Max(0f, wallSlideSpeed);
        wallJumpControlLockTime = Mathf.Max(0f, wallJumpControlLockTime);
        interactHoldDuration = Mathf.Max(0f, interactHoldDuration);
        interactMoveSlowMultiplier = Mathf.Clamp01(interactMoveSlowMultiplier);
        inAirMoveMultiplier = Mathf.Clamp01(inAirMoveMultiplier);
        wallSlideSlowMultiplier = Mathf.Clamp01(wallSlideSlowMultiplier);
    }
}
