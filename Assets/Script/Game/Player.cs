using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityStandardAssets.CrossPlatformInput;
public class Player : MonoBehaviour
{
    public float Speed = 5f;
    public float MaxSpeed = 10f;
    public bool Grounded;
    public float JumpPower = 5f;
    private SpriteRenderer spr;
    private Rigidbody2D rb2d;
    private Animator Animar;
    private bool Jump;
    private bool DoubleJump;
    private bool Movement = true;
    private float horizontalInput;
    private float verticalInput;
    private float lastJumpTime = 0f;
    public float jumpCooldown = 0.5f;
    public LayerMask collisionLayer;
    AudioSource m_MyAudioSource;
    GameObject Jugador;


    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
        Animar = GetComponent<Animator>();
        spr = GetComponent<SpriteRenderer>();
        m_MyAudioSource = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        Animar.SetFloat("Speed", Mathf.Abs(rb2d.velocity.x));
        Animar.SetBool("Grounded", Grounded);
        if (Grounded)
        {
            DoubleJump = true;
        }
        if (CrossPlatformInputManager.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W))
        {
            if (Grounded)
            {
                AlterJump();
                DoubleJump = true;
            }
            else if (DoubleJump)
            {
                AlterJump();
                DoubleJump = false;
            }
        }
    }
    private void FixedUpdate()
    {
#if UNITY_STANDALONE || UNITY_WEBPLAYER
        Vector3 fixedVelocity = rb2d.velocity;
        fixedVelocity.x *= 1f;
        verticalInput = Input.GetAxis("Vertical");
        horizontalInput = Input.GetAxis("Horizontal");

        Vector2 movement = new Vector2(horizontalInput, 0f).normalized * Speed * Time.deltaTime;
        rb2d.velocity = new Vector2(horizontalInput * Speed, rb2d.velocity.y);
        rb2d.velocity = new Vector2(Mathf.Clamp(rb2d.velocity.x, -MaxSpeed, MaxSpeed), rb2d.velocity.y);

        Vector2 raycastDirection = Vector2.down;
        float raycastDistance = 0.55f;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, raycastDistance, collisionLayer);

        Debug.DrawRay(transform.position, raycastDirection * raycastDistance, Color.red);

        Grounded = hit.collider != null;

        if (horizontalInput > 0)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
        else if (horizontalInput < 0)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
#else
        moveInput = Input.GetAxis("Horizontal");
        Vector3 fixedVelocity = rb2d.velocity;
        fixedVelocity.x *= 1f;
        verticalInput = Input.GetAxis("Vertical");
        horizontalInput = Input.GetAxis("Horizontal");
        Vector2 movement = new Vector2(horizontalInput, 0f).normalized * Speed * Time.deltaTime;
        rb2d.velocity = new Vector2(horizontalInput * Speed, rb2d.velocity.y);
        rb2d.velocity = new Vector2(Mathf.Clamp(rb2d.velocity.x, -MaxSpeed, MaxSpeed), rb2d.velocity.y);
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 0.1f, collisionLayer);
        Grounded = hit.collider != null;
        if (horizontalInput > 0)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
        else if (horizontalInput < 0)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
#endif
    }
    private void OnBecameInvisible()
    {
        transform.position = new Vector3(-1, 0, 0);
    }
    public void EnemyJump()
    {
        Jump = true;
    }
    public void EnemyKnocBack(float enemyPosX)
    {
        Jump = true;
        float Side = Mathf.Sign(enemyPosX - transform.position.x);
        rb2d.AddForce(Vector2.left * Side * JumpPower, ForceMode2D.Impulse);
        Movement = false;
        Invoke("EnableMovement", 1.5f);
        Color cl = new Color(193 / 255f, 52 / 255f, 52 / 255f);
        spr.color = cl;
    }
    void EnableMovement()
    {
        Movement = true;
        spr.color = Color.white;
    }
    public void Destruir()
    {
        Enemigo.Destroy(gameObject, 1f);
    }
    void AlterJump()
    {
        if (Time.time - lastJumpTime > jumpCooldown)
        {

            rb2d.AddForce(new Vector2(0f, JumpPower), ForceMode2D.Impulse);
            lastJumpTime = Time.time;
        }
    }
}
