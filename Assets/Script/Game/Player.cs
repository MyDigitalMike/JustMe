using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.CrossPlatformInput;
public class Player : MonoBehaviour
{
    public float Speed = 5f;
    public float MaxSpeed = 5f;
    public bool Grounded;
    public float JumpPower = 10f;
    private SpriteRenderer spr;
    private Rigidbody2D rb2d;
    private Animator Animar;
    private bool Jump;
    private bool DoubleJump;
    private bool Movement = true;
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
                Jump = true;
                DoubleJump = true;
            }
            else if (DoubleJump)
            {
                Jump = true;
                DoubleJump = false;
            }
        }
    }
    private void FixedUpdate()
    {
        
#if UNITY_STANDALONE || UNITY_WEBPLAYER
        Vector3 fixedVelocity = rb2d.velocity;
        fixedVelocity.x *= 1f;

        if (Grounded)
        {
            rb2d.velocity = fixedVelocity;
        }

        float h = Input.GetAxis("Horizontal");
        if (!Movement) h = 0;
        rb2d.AddForce(Vector2.right * Speed * h);
        float LimitedSpeed = Mathf.Clamp(rb2d.velocity.x, -MaxSpeed, MaxSpeed);
        rb2d.velocity = new Vector2(LimitedSpeed, rb2d.velocity.y);
        if (h > 0.1f)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
        if (h < -0.1f)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
        if (Jump)
        {
            rb2d.AddForce(Vector2.up * JumpPower, ForceMode2D.Impulse);
            Jump = false;
        }
#else
        float h = CrossPlatformInputManager.GetAxis("Horizontal");
        rb2d.AddForce(Vector2.right * Speed * h);
        float LimitedSpeed = Mathf.Clamp(rb2d.velocity.x, -MaxSpeed, MaxSpeed);
        rb2d.velocity = new Vector2(LimitedSpeed, rb2d.velocity.y);
        if (h > 0.1f)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
        if (h < -0.1f)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
        if (Jump)
        {
            rb2d.AddForce(Vector2.up * JumpPower, ForceMode2D.Impulse);
            Jump = false;
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
        Color cl = new Color(193/255f,52/255f,52/255f);
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
}
