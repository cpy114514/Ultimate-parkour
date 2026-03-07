using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public enum ControlType
    {
        WASD,
        ArrowKeys,
        IJKL
    }

    [Header("Control")]
    public ControlType controlType;

    [Header("Movement")]
    public float moveSpeed = 8f;
    public float airControl = 5f;

    [Header("Jump")]
    public float jumpForce = 12f;
    public float fallMultiplier = 3f;
    public float lowJumpMultiplier = 2f;

    [Header("Wall Jump")]
    public float wallJumpForceX = 10f;
    public float wallJumpForceY = 12f;

    [Header("Wall Slide")]
    public float wallSlideSpeed = 2f;

    [Header("Coyote Time")]
    public float coyoteTime = 0.1f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckDistance = 0.2f;

    [Header("Wall Check")]
    public Transform wallCheckLeft;
    public Transform wallCheckRight;
    public float wallCheckDistance = 0.2f;

    public LayerMask groundLayer;

    Rigidbody2D rb;
    SpriteRenderer sr;

    float horizontal;
    bool jumpPressed;
    bool jumpHeld;

    bool isGrounded;
    bool touchingWallLeft;
    bool touchingWallRight;

    float coyoteTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        HandleInput();
        DetectGround();
        DetectWalls();
        HandleCoyoteTime();
        HandleJump();
        HandleFlip();
    }

    void FixedUpdate()
    {
        HandleMovement();
        HandleWallSlide();
        BetterJump();
    }

    void HandleInput()
    {
        horizontal = 0;
        jumpPressed = false;

        switch (controlType)
        {
            case ControlType.WASD:
                if (Input.GetKey(KeyCode.A)) horizontal = -1;
                if (Input.GetKey(KeyCode.D)) horizontal = 1;
                jumpHeld = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Space);
                jumpPressed = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space);
                break;

            case ControlType.ArrowKeys:
                if (Input.GetKey(KeyCode.LeftArrow)) horizontal = -1;
                if (Input.GetKey(KeyCode.RightArrow)) horizontal = 1;
                jumpHeld = Input.GetKey(KeyCode.UpArrow);
                jumpPressed = Input.GetKeyDown(KeyCode.UpArrow);
                break;

            case ControlType.IJKL:
                if (Input.GetKey(KeyCode.J)) horizontal = -1;
                if (Input.GetKey(KeyCode.L)) horizontal = 1;
                jumpHeld = Input.GetKey(KeyCode.I);
                jumpPressed = Input.GetKeyDown(KeyCode.I);
                break;
        }
    }

    void DetectGround()
    {
        isGrounded = Physics2D.Raycast(
            groundCheck.position,
            Vector2.down,
            groundCheckDistance,
            groundLayer
        );
    }

    void DetectWalls()
    {
        touchingWallLeft = Physics2D.Raycast(
            wallCheckLeft.position,
            Vector2.left,
            wallCheckDistance,
            groundLayer
        );

        touchingWallRight = Physics2D.Raycast(
            wallCheckRight.position,
            Vector2.right,
            wallCheckDistance,
            groundLayer
        );
    }

    void HandleCoyoteTime()
    {
        if (isGrounded)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer -= Time.deltaTime;
    }

    void HandleJump()
    {
        if (!jumpPressed) return;

        // 普通跳
        if (coyoteTimer > 0)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            coyoteTimer = 0;
            return;
        }

        // 左墙跳
        if (touchingWallLeft)
        {
            rb.velocity = new Vector2(wallJumpForceX, wallJumpForceY);
            return;
        }

        // 右墙跳
        if (touchingWallRight)
        {
            rb.velocity = new Vector2(-wallJumpForceX, wallJumpForceY);
            return;
        }
    }

    void HandleMovement()
    {
        if (isGrounded)
        {
            rb.velocity = new Vector2(horizontal * moveSpeed, rb.velocity.y);
        }
        else
        {
            float targetSpeed = horizontal * moveSpeed;
            float newX = Mathf.Lerp(rb.velocity.x, targetSpeed, airControl * Time.fixedDeltaTime);
            rb.velocity = new Vector2(newX, rb.velocity.y);
        }
    }

    void HandleWallSlide()
    {
        if (!isGrounded && (touchingWallLeft || touchingWallRight) && rb.velocity.y < 0)
        {
            rb.velocity = new Vector2(rb.velocity.x, -wallSlideSpeed);
        }
    }

    void BetterJump()
    {
        if (rb.velocity.y < 0)
        {
            rb.velocity += Vector2.up * Physics2D.gravity.y *
                           (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.velocity.y > 0 && !jumpHeld)
        {
            rb.velocity += Vector2.up * Physics2D.gravity.y *
                           (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    void HandleFlip()
    {
        if (horizontal > 0.1f)
            sr.flipX = true;
        else if (horizontal < -0.1f)
            sr.flipX = false;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(groundCheck.position,
                groundCheck.position + Vector3.down * groundCheckDistance);
        }

        if (wallCheckLeft != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(wallCheckLeft.position,
                wallCheckLeft.position + Vector3.left * wallCheckDistance);
        }

        if (wallCheckRight != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(wallCheckRight.position,
                wallCheckRight.position + Vector3.right * wallCheckDistance);
        }
    }
}