using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Enemigo : MonoBehaviour
{
    public float Speed = 1f;
    public float MaxSpeed = 1f;
    private Rigidbody2D rb2d;
    AudioSource m_MyAudioSource;
    AudioSource GhostSound;
    public GameObject GhostSoundEffect;
    public Animator Animar;
#pragma warning disable 0649
    [SerializeField] Text EnemysDown;
#pragma warning restore 0649
    void Start()
    {
        m_MyAudioSource = GetComponent<AudioSource>();
        GhostSound = GhostSoundEffect.GetComponent<AudioSource>();
        rb2d = GetComponent<Rigidbody2D>();
        Animar = GetComponent<Animator>();
    }
    void FixedUpdate()
    {
        rb2d.AddForce(Vector2.right * Speed);
        float LimitedSpeed = Mathf.Clamp(rb2d.velocity.x, -MaxSpeed, MaxSpeed);
        rb2d.velocity = new Vector2(LimitedSpeed, rb2d.velocity.y);
        if (rb2d.velocity.x > -0.01f && rb2d.velocity.x < 0.01f)
        {
            Speed = -Speed;
            rb2d.velocity = new Vector2(Speed, rb2d.velocity.y);
        }
        if (Speed < 0)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
        if (Speed > 0)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Jugador")
        {
            float OffsetY = 0.4f;
            if (transform.position.y + OffsetY < collision.transform.position.y)
            {
                collision.SendMessage("EnemyJump");
                Animar.SetBool("Dead", true);
                GhostSound.Play();
                //m_MyAudioSource.Play();
                Invoke("Destruir", 0.60f);
            }
            else
            {
                collision.SendMessage("EnemyKnocBack", transform.position.x);
            }
        }
    }
    public void Destruir()
    {
        gameObject.SetActive(false);
    }
}
