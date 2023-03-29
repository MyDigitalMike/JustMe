using System.Collections;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Advertisements;
using UnityEngine.Experimental.Rendering.LWRP;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class Libro : MonoBehaviour
{
    private Enemigo ScritpEnemy;
    private CoinManager ScritpCoinManager;
    private JumpingHard ScritpJumpingHard;
    public GameObject Elemento;
    public GameObject CoinElement;
    public Metodos metodos;
    public int Nivel = GameLevelStatus.CantidadNiveles2;
    public GameObject Enemigo;
    public GameObject JumpForce;
    public GameObject Jugador;
    public Tilemap MapaDeCasillas;
    public TileBase Casilla;
    [Header("Semilla Generadora")]
    public bool SemillaAleatoria = false;
    public float Semilla = 0;
    [Range(0, 1)]
    public float PorcentajeDeRelleno = 0.4f;

    [Header("Count Timer")]
    public float currentTime = 0f;
    float LimitTime = 20f;
    [Header("Position Of Enemys")]
#pragma warning disable 0649
    [SerializeField] Text countDownText;
    [SerializeField] Text Enemys;
#pragma warning restore 0649
    public GameObject SoundBookEffect;
    AudioSource m_MyAudioSource;
    AudioSource BookSound;
    void Awake()
    {
        ScritpJumpingHard = JumpForce.GetComponent<JumpingHard>();  
    }
    #region Generar Mapa
    /// <summary>
    /// Se genera el mapa con las casillas con la información porporcionada en "Mapa"
    /// </summary>
    /// <param name="Mapa">Información para la generación del mapa de casillas. 1 = Casillas 2 = No hay casillas</param>
    /// <param name="MapaDeCasillas">Referencia al mapa de casillas dónde se generarán las casillas</param>
    /// <param name="Casilla">Casilla la cual se pintara en el mapa de casillas</param>
    public void Generacion(int Nivel)
    {
        //Limpiamoz el mapa de casillas
        MapaDeCasillas.ClearAllTiles();
        //Se Crea El Array bidimencional del mapa
        int[,] Mapa = null;

        if (Nivel <= 4)
        {
            
            if (Nivel == 1)
            {
                Semilla = 1;
                PorcentajeDeRelleno = 0.2f;
                Mapa = Metodos.GenerarMapaAleatorio(240, 34, Semilla, PorcentajeDeRelleno, true);
                Mapa = Metodos.AutomataCelularMoore(Mapa, 3, true, Jugador, 5.55f, 22.49f, Elemento, 57f, 3f);
                Metodos.GenerarMapa(Mapa, MapaDeCasillas, Casilla);
                Enemigos(Enemigo, 32, 4, -1);
                Enemigos(Enemigo, 16, 4, 1);
                CoinGenerator(CoinElement, 10, 1);
                currentTime = 2000f;
            }
            if (Nivel == 2)
            {
                Semilla = 3;
                PorcentajeDeRelleno = 0.2f;
                Mapa = Metodos.GenerarMapaAleatorio(240, 34, Semilla, PorcentajeDeRelleno, true);
                Mapa = Metodos.AutomataCelularMoore(Mapa, 3, true, Jugador, 1.55f, 5.49f, Elemento, 57f, 3f);
                Metodos.GenerarMapa(Mapa, MapaDeCasillas, Casilla);
                Enemigos(Enemigo, 45, 8, -1);
                currentTime = 80f;
            }
            if (Nivel == 3)
            {
                Semilla = 680.6475f;
                PorcentajeDeRelleno = 0.368f;
                Mapa = Metodos.GenerarMapaAleatorio(60, 34, Semilla, PorcentajeDeRelleno, true);
                Mapa= Metodos.AutomataCelularVonNeumann(Mapa, 3, true, Jugador, 2.07f, 26.6f, Elemento, 57f, 3f);
                Metodos.GenerarMapa(Mapa, MapaDeCasillas, Casilla);
                #region Creación de enemigos
                Enemigos(Enemigo, 4.65f, 1.52f, 1);
                Enemigos(Enemigo, 16.32f, 1.52f, -1);
                Enemigos(Enemigo, 29.71f, 3.52f, 1);
                Enemigos(Enemigo, 57.41f, 1.52f, -1);
                #endregion
                #region Creación HardJump
                JumpForced(JumpForce, 12.48f, 5.236f, 200f);
                JumpForced(JumpForce, 15.21f, 10.237f, 100f);
                JumpForced(JumpForce, 15.6f, 13.64f, 100f);
                JumpForced(JumpForce, 15.6f, 1.24f, 100f);
                JumpForced(JumpForce, 31.38f, 3.24f, 150f);
                #endregion
                currentTime = 200f;
            }
        }
        else
        {
            if (Nivel > 4)
            {
                SceneManager.LoadScene("Creditos");
                //Destroy(Enemy, 0f);
            }
        }
    }
    void DestroyWithTag(string destroyTag)
    {
        GameObject[] destroyObject;
        destroyObject = GameObject.FindGameObjectsWithTag(destroyTag);
        foreach (GameObject oneObject in destroyObject)
        {
            Destroy(oneObject);
        }
    }
    #endregion
    public void LimpiarMapa()
    {
        MapaDeCasillas.ClearAllTiles();
    }
    public void Enemigos(GameObject Enemigo, float PositionXEnemy, float PositionYEnemy, int NumberSpeed)
    {

        Enemigo = (GameObject)Instantiate(Enemigo, new Vector3(PositionXEnemy, PositionYEnemy, 0), Quaternion.identity);
        Enemigo.name = "Enemigo";
        Enemigo.tag = "CloneEnemy";
        ScritpEnemy = Enemigo.GetComponent<Enemigo>();
        ScritpEnemy.Speed = NumberSpeed;
        if (ScritpEnemy.Speed < 0)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
        if (ScritpEnemy.Speed > 0)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }

    }
    public void CoinGenerator(GameObject CoinElement, float PositionXCoin, float PositionYCoin)
    {
        CoinElement = (GameObject)Instantiate(CoinElement, new Vector3(PositionXCoin, PositionYCoin, 0), Quaternion.identity);
        CoinElement.name = "Coin";
        CoinElement.tag = "CloneCoin";
        ScritpCoinManager = CoinElement.GetComponent<CoinManager>();
    }
    public float Fuerza = 0;
    public void JumpForced(GameObject JumpHard, float PositionXJump, float PositionYJump, float ImpulseFoce)
    {
        JumpHard = (GameObject)Instantiate(JumpHard, new Vector3(PositionXJump, PositionYJump, 0), Quaternion.identity);
        JumpHard.name = "HardJump";
        JumpHard.tag = "CloneHardJump";
        ScritpJumpingHard = JumpHard.GetComponent<JumpingHard>();
        ScritpJumpingHard.FuerzaJump.forceMagnitude = ImpulseFoce;
    }
    void Start()
    {
        m_MyAudioSource = GetComponent<AudioSource>();
        BookSound = SoundBookEffect.GetComponent<AudioSource>();
        currentTime = LimitTime;
        Generacion(1);
        Timer();
    }
    public void TimerGameOver(Text Text, float LimitTime)
    {
        string Texto = Text.text;
        if (Texto == "0")
        {
            SceneManager.LoadScene("GameOver");
        }
    }
    private void Update()
    {
        ContarEnemigosTotales();
    }
    void Timer()
    {
        currentTime -= 1 * Time.deltaTime;
        countDownText.text = currentTime.ToString("0");
        if (currentTime <= 0)
        {
            currentTime = 0;
            if (currentTime == 0)
            {
                Debug.Log("Fin");
            }
        }
        TimerGameOver(countDownText, LimitTime);
    }
    private void FixedUpdate()
    {
        Timer();

    }
    public void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("JustMe");
    }
    public void SavaGame()
    {
        SaveSystem.SavePlayer(this);
    }
    public void LoadGame()
    {
        GameData data = SaveSystem.LoadGame();
        Nivel = data.CantidadNiveles;
        currentTime = data.currentTime;
        Generacion(Nivel);
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {

        if (collision.gameObject.tag == "Jugador")
        {
            Nivel++;
            BookSound.Play();
            Generacion(Nivel);
        }
    }
    void ContarEnemigosTotales()
    {
        int thingycount = 0;
        GameObject[] thingytofind = GameObject.FindGameObjectsWithTag("CloneEnemy");
        thingycount = thingytofind.Length-1;
        Enemys.text = "Enemigos Activos: " + thingycount.ToString();
    }
}

