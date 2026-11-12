using UnityEngine;
using UnityStandardAssets.CrossPlatformInput;

[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    // Keep existing serialized names and animation bindings.
    public float Speed = 5f;
    public float MaxSpeed = 10f;
    public bool Grounded;
    public float JumpPower = 5f;
    public float jumpCooldown = 0.5f;
    public LayerMask collisionLayer;

    [Header("Movement tuning (zero keeps immediate legacy response)")]
    [Min(0f)] public float acceleration;
    [Min(0f)] public float deceleration;
    [Range(0f, 1f)] public float airControl = 1f;
    [Range(0f, 1f)] public float minimumGroundNormalY = 0.5f;
    [Min(0f)] public float jumpBufferTime = 0.1f;

    private readonly ContactPoint2D[] contacts = new ContactPoint2D[32];
    private Rigidbody2D rb2d;
    private SpriteRenderer spr;
    private Animator Animar;
    private bool DoubleJump;
    private bool Movement = true;
    private float horizontalInput;
    private float jumpRequestedUntil = float.NegativeInfinity;
    private bool jumpQueued;
    private float lastJumpTime = float.NegativeInfinity;
    private Vector3 initialScale;

    void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        Animar = GetComponent<Animator>();
        spr = GetComponent<SpriteRenderer>();
        initialScale = transform.localScale;
    }

    void Update()
    {
        // The existing manager selects hardware or mobile virtual controls.
        horizontalInput = CrossPlatformInputManager.GetAxis("Horizontal");
        if (CrossPlatformInputManager.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W))
        {
            jumpRequestedUntil = Time.time + jumpBufferTime;
            jumpQueued = true;
        }

        if (Animar != null)
        {
            Animar.SetFloat("Speed", Mathf.Abs(rb2d.linearVelocity.x));
            Animar.SetBool("Grounded", Grounded);
        }
    }

    void FixedUpdate()
    {
        RefreshGroundContact();
        if (Grounded) DoubleJump = true;
        bool freshJump = jumpQueued;
        jumpQueued = false;
        if (!Movement) return;

        float target = Mathf.Clamp(horizontalInput * Speed, -MaxSpeed, MaxSpeed);
        float rate = Mathf.Abs(target) > 0.001f ? acceleration : deceleration;
        float control = Grounded ? 1f : airControl;
        float x = rate <= 0f
            ? Mathf.Lerp(rb2d.linearVelocity.x, target, control)
            : Mathf.MoveTowards(rb2d.linearVelocity.x, target, rate * control * Time.fixedDeltaTime);
        rb2d.linearVelocity = new Vector2(x, rb2d.linearVelocity.y);

        if ((freshJump || Time.time <= jumpRequestedUntil) && Time.time - lastJumpTime >= jumpCooldown && (Grounded || DoubleJump))
        {
            bool fromGround = Grounded;
            rb2d.AddForce(Vector2.up * JumpPower, ForceMode2D.Impulse);
            lastJumpTime = Time.time;
            jumpRequestedUntil = float.NegativeInfinity;
            DoubleJump = fromGround;
            Grounded = false;
        }

        if (Mathf.Abs(horizontalInput) > 0.001f)
            transform.localScale = new Vector3(Mathf.Abs(initialScale.x) * Mathf.Sign(horizontalInput), initialScale.y, initialScale.z);
    }

    private void RefreshGroundContact()
    {
        Grounded = false;
        if (rb2d.linearVelocity.y > 0.1f) return;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(collisionLayer);
        int count = rb2d.GetContacts(filter, contacts);
        for (int i = 0; i < count; i++)
        {
            if (contacts[i].normal.y >= minimumGroundNormalY)
            {
                Grounded = true;
                return;
            }
        }
    }

    private void OnBecameInvisible()
    {
        transform.position = new Vector3(-1, 0, 0);
    }

    public void EnemyJump()
    {
        // Retained for event bindings; the old flag had no consumer.
    }

    public void EnemyKnocBack(float enemyPosX)
    {
        float side = Mathf.Sign(enemyPosX - transform.position.x);
        rb2d.AddForce(Vector2.left * side * JumpPower, ForceMode2D.Impulse);
        Movement = false;
        CancelInvoke(nameof(EnableMovement));
        Invoke(nameof(EnableMovement), 1.5f);
        if (spr != null) spr.color = new Color(193 / 255f, 52 / 255f, 52 / 255f);
    }

    void EnableMovement()
    {
        Movement = true;
        if (spr != null) spr.color = Color.white;
    }

    public void Destruir()
    {
        Destroy(gameObject, 1f);
    }
}
