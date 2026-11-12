using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Unity.Cinemachine;

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

    [Header("Surface and caves prototype (disable for legacy levels)")]
    [SerializeField] bool usarMundoProcedural = true;
    [SerializeField] WorldGenerationSettings mundo = new WorldGenerationSettings();
    [SerializeField] bool semillaMundoFija = true;
    [SerializeField] int semillaMundo = 1;
    [SerializeField] bool usarMundoHardcore = true;
    [SerializeField] TraversalGenerationSettings mundoTraversal = new TraversalGenerationSettings { Profile = WorldProfile.Mixed };
    TraversalCapabilities measuredCapabilities;
    Player traversalPlayer;
    int displayedStamina = -1;
    private int? runSeed;
    private GameObject worldPresentation;
    private int lastGoalFrame = -1;
    public WorldData LastGeneratedWorld { get; private set; }

    [Header("Count Timer")]
    public float currentTime = 0f;
    float LimitTime = 20f;
    [Header("Position Of Enemys")]
#pragma warning disable 0649
    [SerializeField] Text countDownText;
    [SerializeField, HideInInspector] Text Enemys;
#pragma warning restore 0649
    public GameObject SoundBookEffect;
    AudioSource m_MyAudioSource;
    AudioSource BookSound;
    void Awake()
    {
        ScritpJumpingHard = JumpForce.GetComponent<JumpingHard>();
        // Retain the reference for legacy scene compatibility; presentation is retired.
        if (Enemys != null && Enemys.gameObject.scene.IsValid()) Enemys.gameObject.SetActive(false);
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
        if (usarMundoProcedural && Nivel >= 1 && Nivel <= 3)
        {
            GenerarMundoProcedural(Nivel);
            return;
        }
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
                Mapa = Metodos.GenerarMapaAleatorio(60, 34, Semilla, PorcentajeDeRelleno, true);
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
                Mapa = Metodos.GenerarMapaAleatorio(60, 34, Semilla, PorcentajeDeRelleno, true);
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
            if (Nivel == 4)
            {
                SceneManager.LoadScene("Creditos");
                //Destroy(Enemy, 0f);
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
    private void GenerarMundoProcedural(int nivel)
    {
        if (usarMundoHardcore) { GenerarMundoHardcore(nivel); return; }
        int? seed = semillaMundoFija ? unchecked(semillaMundo + nivel - 1)
            : (runSeed.HasValue ? (int?)unchecked(runSeed.Value + nivel - 1) : null);
        WorldData data = ProceduralWorld.Generate(mundo, seed);
        Nivel = nivel;
        if (!runSeed.HasValue) runSeed = unchecked(data.Seed - nivel + 1);
        WorldTilemapRenderer.Apply(data, MapaDeCasillas, Casilla);
        LastGeneratedWorld = data;
        DestroyWithTag("CloneEnemy");
        DestroyWithTag("CloneCoin");
        DestroyWithTag("CloneHardJump");
        PlaceInWorld(Jugador, data.SpawnX, data.SpawnY);
        PlaceInWorld(Elemento, data.GoalX, data.GoalY);
        if (Elemento.GetComponent<Collider2D>() == null)
        {
            // The recovered book prefab has no collision receiver. Derive it from its sprite.
            var goal = Elemento.AddComponent<BoxCollider2D>();
            SpriteRenderer renderer = Elemento.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
            {
                goal.size = renderer.sprite.bounds.size;
                goal.offset = renderer.sprite.bounds.center;
            }
            goal.isTrigger = true;
        }
        PlaceInWorld(CoinElement, mundo.Width / 3, data.SpawnY);
        Vector3 middle = WorldTilemapRenderer.CellCenter(MapaDeCasillas, mundo.Width / 2, data.SpawnY);
        Enemigos(Enemigo, middle.x, middle.y, 1);
        Vector3 enemyCell = WorldTilemapRenderer.CellCenter(MapaDeCasillas, mundo.Width * 3 / 4, data.SpawnY);
        Enemigos(Enemigo, enemyCell.x, enemyCell.y, -1);
        if (nivel == 3)
        {
            Vector3 spring = WorldTilemapRenderer.CellCenter(MapaDeCasillas, mundo.Width * 2 / 3, data.SpawnY);
            JumpForced(JumpForce, spring.x, spring.y, 100f);
        }
        currentTime = nivel == 1 ? 2000f : (nivel == 2 ? 80f : 200f);
        ConfigureWorldPresentation();
    }

    private void GenerarMundoHardcore(int nivel)
    {
        traversalPlayer = Jugador.GetComponent<Player>();
        if (measuredCapabilities == null)
        {
            TextAsset calibration = Resources.Load<TextAsset>("TraversalCapabilities");
            if (calibration == null) throw new System.InvalidOperationException("Measured traversal calibration is missing.");
            measuredCapabilities = JsonUtility.FromJson<TraversalCapabilities>(calibration.text);
        }
        ValidateCalibration(traversalPlayer, measuredCapabilities);
        int? seed = semillaMundoFija ? unchecked(semillaMundo + nivel - 1)
            : runSeed.HasValue ? (int?)unchecked(runSeed.Value + nivel - 1) : null;
        WorldData data = ProceduralWorld.GenerateTraversal(mundoTraversal, measuredCapabilities, traversalPlayer.staminaSettings, seed);
        if (!runSeed.HasValue) runSeed = unchecked(data.Seed - nivel + 1);
        WorldTilemapRenderer.Apply(data, MapaDeCasillas, Casilla);
        LastGeneratedWorld = data; Nivel = nivel;
        DestroyWithTag("CloneEnemy"); DestroyWithTag("CloneCoin"); DestroyWithTag("CloneHardJump");
        foreach (Enemigo enemy in FindObjectsByType<Enemigo>(FindObjectsInactive.Include))
            if (enemy.gameObject.scene == gameObject.scene) enemy.gameObject.SetActive(false);
        foreach (JumpingHard spring in FindObjectsByType<JumpingHard>(FindObjectsInactive.Include))
            if (spring.gameObject.scene == gameObject.scene) spring.gameObject.SetActive(false);
        foreach (CoinManager coin in FindObjectsByType<CoinManager>(FindObjectsInactive.Include))
            if (coin.gameObject.scene == gameObject.scene) coin.gameObject.SetActive(false);
        if (CoinElement != null && CoinElement.scene.IsValid()) CoinElement.SetActive(false);
        var environment = MapaDeCasillas.GetComponent<TraversalEnvironment>();
        if (environment == null) environment = MapaDeCasillas.gameObject.AddComponent<TraversalEnvironment>();
        WorldTilemapRenderer.ConfigureTraversal(data, MapaDeCasillas, environment, traversalPlayer);
        traversalPlayer.VoidEntered -= HandleTraversalVoid;
        traversalPlayer.VoidEntered += HandleTraversalVoid;
        PlaceActor(Jugador, WorldTilemapRenderer.StandingPosition(data, MapaDeCasillas, data.Route.Nodes[0], traversalPlayer.SoleOffset));
        TraversalNode goal = data.Route.Nodes[data.Route.Links[data.Route.PrimaryLinks[data.Route.PrimaryLinks.Length - 1]].ToNode];
        PlaceActor(Elemento, WorldTilemapRenderer.StandingPosition(data, MapaDeCasillas, goal, traversalPlayer.SoleOffset));
        if (Elemento.GetComponent<Collider2D>() == null)
        {
            var trigger = Elemento.AddComponent<BoxCollider2D>(); trigger.isTrigger = true;
            SpriteRenderer renderer = Elemento.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null) { trigger.size = renderer.sprite.bounds.size; trigger.offset = renderer.sprite.bounds.center; }
        }
        displayedStamina = -1; ConfigureWorldPresentation();
    }

    private static void ValidateCalibration(Player player, TraversalCapabilities c)
    {
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        bool current = System.Math.Abs(player.Speed - c.SourceWalkCommand) < .001 && System.Math.Abs(player.MaxSpeed - c.SourceMaxSpeed) < .001 &&
            System.Math.Abs(player.JumpPower - c.SourceJumpImpulse) < .001 && System.Math.Abs(player.BaseGravityScale - c.SourceGravityScale) < .001 &&
            System.Math.Abs(body.mass - c.SourceMass) < .001 && System.Math.Abs(body.linearDamping - c.SourceLinearDamping) < .001 &&
            System.Math.Abs(player.sprintMultiplier - c.SourceSprintMultiplier) < .001 && System.Math.Abs(player.crawlSpeedMultiplier - c.SourceCrawlMultiplier) < .001 &&
            System.Math.Abs(player.climbSpeed - c.SourceClimbCommand) < .001 && System.Math.Abs(player.airControl - c.SourceAirControl) < .001 &&
            System.Math.Abs(player.acceleration - c.SourceAcceleration) < .001 && System.Math.Abs(player.deceleration - c.SourceDeceleration) < .001 &&
            System.Math.Abs(player.crouchedBoxRatio - c.SourceCrouchRatio) < .001 && System.Math.Abs(player.SoleOffset - c.SoleLocalY) < .015 &&
            System.Math.Abs(player.StandingTop - player.SoleOffset - c.StandingHeight) < .015 &&
            System.Math.Abs(player.jumpCooldown - c.SourceJumpCooldown) < .001 && System.Math.Abs(Time.fixedDeltaTime - c.FixedStep) < .0001 &&
            System.Math.Abs(Physics2D.gravity.x - c.SourceGravityX) < .001 && System.Math.Abs(Physics2D.gravity.y - c.SourceGravityY) < .001 &&
            System.Math.Abs(player.maximumLedgeHeight - c.SourceLedgeHeight) < .001 && System.Math.Abs(player.ledgeReach - c.SourceLedgeReach) < .001 &&
            System.Math.Abs(player.pullUpSpeed - c.SourcePullUpSpeed) < .001;
        foreach (Collider2D collider in player.GetComponentsInChildren<Collider2D>())
            if (collider.attachedRigidbody == body && !collider.isTrigger)
                current &= collider.bounds.min.x - body.position.x >= -.3f && collider.bounds.max.x - body.position.x <= .3f;
        CircleCollider2D foot = player.GetComponentInChildren<CircleCollider2D>();
        current &= foot != null && !foot.isTrigger;
        if (!current) throw new System.InvalidOperationException("Player tuning differs from the measured traversal calibration. Re-measure before accepting a new world.");
    }

    private void HandleTraversalVoid()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("GameOver");
    }

    private void OnDestroy()
    {
        if (traversalPlayer != null) traversalPlayer.VoidEntered -= HandleTraversalVoid;
    }

    private void PlaceActor(GameObject target, Vector3 position)
    {
        if (!target.scene.IsValid()) throw new System.InvalidOperationException("Cannot place an asset reference.");
        position.z = target.transform.position.z;
        Vector3 delta = position - target.transform.position; target.transform.position = position;
        Rigidbody2D body = target.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = position; body.linearVelocity = Vector2.zero; }
        if (target == Jugador)
        {
            traversalPlayer.NotifyWarp();
            CinemachineCore.OnTargetObjectWarped(target.transform, delta);
            if (Camera.main != null) Camera.main.transform.position += delta;
        }
    }

    private void PlaceInWorld(GameObject target, int x, int y)
    {
        if (target == null) return;
        if (!target.scene.IsValid())
            throw new System.InvalidOperationException("Only scene instances can be placed in the generated world: " + target.name);
        Vector3 position = WorldTilemapRenderer.CellCenter(MapaDeCasillas, x, y);
        position.z = target.transform.position.z;
        Vector3 delta = position - target.transform.position;
        target.transform.position = position;
        Rigidbody2D body = target.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = position; body.linearVelocity = Vector2.zero; }
        if (target == Jugador)
        {
            CinemachineCore.OnTargetObjectWarped(target.transform, delta);
            Camera camera = Camera.main;
            if (camera != null) camera.transform.position += delta;
        }
    }

    private void ConfigureWorldPresentation()
    {
        if (worldPresentation == null)
        {
            GameObject prototype = Resources.Load<GameObject>("WorldPresentation");
            if (prototype != null) worldPresentation = Instantiate(prototype);
        }
        if (worldPresentation != null)
        {
            WorldLighting lighting = worldPresentation.GetComponent<WorldLighting>();
            if (lighting != null) { lighting.player = Jugador.transform; lighting.surfaceHeight = mundo.SurfaceHeight; }
        }
        Camera camera = Camera.main;
        if (camera != null)
            foreach (ScrollingBackground background in camera.GetComponentsInChildren<ScrollingBackground>())
            {
                background.enabled = false;
                if (background.bgRend != null) background.bgRend.sortingOrder = -30;
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
    void Timer()
    {
        if (usarMundoProcedural && usarMundoHardcore)
        {
            if (traversalPlayer != null && countDownText != null)
            {
                int value = Mathf.FloorToInt(traversalPlayer.Stamina.Current);
                if (value != displayedStamina) { countDownText.text = "Stamina " + value; displayedStamina = value; }
            }
            return;
        }
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
            CompleteLevel();
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (usarMundoProcedural && other.CompareTag("Jugador")) CompleteLevel();
    }

    private void CompleteLevel()
    {
        // Both player colliders may enter the goal during the same physics step.
        if (lastGoalFrame == Time.frameCount) return;
        lastGoalFrame = Time.frameCount;
        Nivel++;
        if (BookSound != null)
        {
            if (BookSound.gameObject.scene.IsValid()) BookSound.Play();
            else if (BookSound.clip != null) AudioSource.PlayClipAtPoint(BookSound.clip, transform.position, BookSound.volume);
        }
        Generacion(Nivel);
    }
}

