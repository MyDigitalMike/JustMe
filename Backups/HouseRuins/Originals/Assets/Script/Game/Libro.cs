using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Unity.Cinemachine;

public class Libro : MonoBehaviour
{
    public GameObject Elemento;
    public GameObject Jugador;
    public Tilemap MapaDeCasillas;
    public TileBase Casilla;
    public int Nivel = 1;
    public float currentTime; // Retain the existing save DTO field and serialized name.
    public GameObject SoundBookEffect;

    [Header("Validated procedural world")]
    [SerializeField] TraversalGenerationSettings mundoTraversal = new TraversalGenerationSettings { Profile = WorldProfile.Mixed };
    [SerializeField] TraversalLayout layoutMundo = TraversalLayout.Random;
    [SerializeField] bool semillaMundoFija;
    [SerializeField] int semillaMundo = 1;
    [SerializeField] DiaryProgressionSettings diaryProgression = new DiaryProgressionSettings();
    // Keep these serialized references; their old labels are repurposed as temporary feedback.
    [SerializeField] UnityEngine.UI.Text countDownText;
    [SerializeField, HideInInspector] UnityEngine.UI.Text Enemys;

    TraversalCapabilities measuredCapabilities;
    WorldRunSession run;
    Player traversalPlayer;
    AudioSource bookSound;
    GameObject worldPresentation, fragmentRoot;
    Sprite fragmentSprite;
    int lastGoalFrame = -1;
    public WorldData LastGeneratedWorld { get; private set; }
    public DiaryProgress Diary { get; private set; }
    public GameplayFeedback Feedback { get; private set; }
    public int LevelRevision { get; private set; }
    public int CurrentSeed { get { return LastGeneratedWorld != null ? LastGeneratedWorld.Seed : 0; } }
    public string CurrentSignature { get { return run != null ? run.CurrentSignature : null; } }
    public int RequiredFragments { get { return Diary != null ? Diary.RequiredCount : 0; } }
    public int CollectedFragments { get { return Diary != null ? Diary.CollectedCount : 0; } }

    void Awake()
    {
        run = new WorldRunSession();
        traversalPlayer = Jugador != null ? Jugador.GetComponent<Player>() : null;
        Feedback = GetComponent<GameplayFeedback>();
        if (Feedback == null) Feedback = gameObject.AddComponent<GameplayFeedback>();
        Feedback.Initialize(traversalPlayer, this, countDownText, Enemys);
    }

    void Start()
    {
        bookSound = SoundBookEffect != null ? SoundBookEffect.GetComponent<AudioSource>() : null;
        PauseMenu.EnPausa = false; Time.timeScale = 1f;
        Generacion(1);
    }

    public void Generacion(int nivel)
    {
        if (nivel < 1) throw new System.ArgumentOutOfRangeException("nivel");
        if (traversalPlayer == null || MapaDeCasillas == null || Elemento == null || Casilla == null)
            throw new System.InvalidOperationException("Libro requires the existing scene Player, tilemap, tile and exit references.");
        if (measuredCapabilities == null)
        {
            TextAsset calibration = Resources.Load<TextAsset>("TraversalCapabilities");
            if (calibration == null) throw new System.InvalidOperationException("Measured traversal calibration is missing.");
            measuredCapabilities = JsonUtility.FromJson<TraversalCapabilities>(calibration.text);
        }
        ValidateCalibration(traversalPlayer, measuredCapabilities);
        mundoTraversal.Layout = layoutMundo;
        mundoTraversal.RequiredFragments = diaryProgression.RequiredForLevel(nivel);
        WorldData data = run.GenerateNext(nivel, semillaMundoFija, semillaMundo,
            seed => ProceduralWorld.GenerateTraversal(mundoTraversal, measuredCapabilities, traversalPlayer.staminaSettings, seed));
        RetireFragments();
        WorldTilemapRenderer.Apply(data, MapaDeCasillas, Casilla);
        LastGeneratedWorld = data; Nivel = nivel; LevelRevision++;
        Diary = new DiaryProgress(data.RequiredFragments, LevelRevision);
        Feedback.ClearMessage();
        var environment = MapaDeCasillas.GetComponent<TraversalEnvironment>();
        if (environment == null) environment = MapaDeCasillas.gameObject.AddComponent<TraversalEnvironment>();
        WorldTilemapRenderer.ConfigureTraversal(data, MapaDeCasillas, environment, traversalPlayer);
        traversalPlayer.VoidEntered -= HandleTraversalVoid; traversalPlayer.VoidEntered += HandleTraversalVoid;
        PlaceActor(Jugador, WorldTilemapRenderer.StandingPosition(data, MapaDeCasillas, data.Route.Nodes[0], traversalPlayer.SoleOffset));
        TraversalNode goal = data.Route.Nodes[data.Route.Links[data.Route.PrimaryLinks[data.Route.PrimaryLinks.Length - 1]].ToNode];
        PlaceActor(Elemento, WorldTilemapRenderer.StandingPosition(data, MapaDeCasillas, goal, traversalPlayer.SoleOffset));
        if (Elemento.GetComponent<Collider2D>() == null)
        {
            var trigger = Elemento.AddComponent<BoxCollider2D>(); trigger.isTrigger = true;
            SpriteRenderer renderer = Elemento.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null) { trigger.size = renderer.sprite.bounds.size; trigger.offset = renderer.sprite.bounds.center; }
        }
        ConfigureWorldPresentation(); SpawnFragments(data);
    }

    void SpawnFragments(WorldData data)
    {
        if (fragmentSprite == null) fragmentSprite = DiaryPage.CreatePaperSprite();
        fragmentRoot = new GameObject("Diary fragments"); fragmentRoot.transform.SetParent(MapaDeCasillas.transform, true);
        var terrainRenderer = MapaDeCasillas.GetComponent<TilemapRenderer>();
        Rigidbody2D collector = traversalPlayer.GetComponent<Rigidbody2D>();
        for (int id = 0; id < data.FragmentNodes.Length; id++)
        {
            int node = data.FragmentNodes[id];
            var page = new GameObject("Diary fragment " + (id + 1), typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(DiaryPage));
            page.transform.SetParent(fragmentRoot.transform, true);
            page.transform.position = WorldTilemapRenderer.StandingPosition(data, MapaDeCasillas, data.Route.Nodes[node], traversalPlayer.SoleOffset) + Vector3.down * .08f;
            page.transform.localScale = Vector3.one * .35f;
            var renderer = page.GetComponent<SpriteRenderer>(); renderer.sprite = fragmentSprite;
            renderer.sharedMaterial = terrainRenderer.sharedMaterial; renderer.sortingLayerID = terrainRenderer.sortingLayerID; renderer.sortingOrder = 4;
            var trigger = page.GetComponent<BoxCollider2D>(); trigger.size = fragmentSprite.bounds.size; trigger.isTrigger = true;
            page.GetComponent<DiaryPage>().Initialize(id, node, LevelRevision, collector, TryCollectFragment);
        }
    }

    public bool TryCollectFragment(int id, int revision)
    {
        if (Diary == null || !Diary.TryCollect(id, revision)) return false;
        Feedback.ShowMessage("Trozo recogido " + CollectedFragments + "/" + RequiredFragments);
        return true;
    }

    void RetireFragments()
    {
        if (fragmentRoot != null) { fragmentRoot.SetActive(false); Destroy(fragmentRoot); fragmentRoot = null; }
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

    void ConfigureWorldPresentation()
    {
        if (worldPresentation == null)
        {
            GameObject prototype = Resources.Load<GameObject>("WorldPresentation");
            if (prototype != null) worldPresentation = Instantiate(prototype);
        }
        if (worldPresentation != null)
        {
            WorldLighting lighting = worldPresentation.GetComponent<WorldLighting>();
            if (lighting != null) { lighting.player = Jugador.transform; lighting.surfaceHeight = mundoTraversal.Terrain.SurfaceHeight; }
        }
    }

    void PlaceActor(GameObject target, Vector3 position)
    {
        if (!target.scene.IsValid()) throw new System.InvalidOperationException("Cannot place an asset reference.");
        position.z = target.transform.position.z;
        Vector3 delta = position - target.transform.position; target.transform.position = position;
        Rigidbody2D body = target.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = position; body.linearVelocity = Vector2.zero; }
        if (target == Jugador)
        {
            traversalPlayer.NotifyWarp(); CinemachineCore.OnTargetObjectWarped(target.transform, delta);
            if (Camera.main != null) Camera.main.transform.position += delta;
        }
    }

    void HandleTraversalVoid() { Time.timeScale = 1f; SceneManager.LoadScene("GameOver"); }
    void OnDestroy()
    {
        if (traversalPlayer != null) traversalPlayer.VoidEntered -= HandleTraversalVoid;
        RetireFragments();
        if (fragmentSprite != null) { Destroy(fragmentSprite.texture); Destroy(fragmentSprite); }
    }

    public void LimpiarMapa() { RetireFragments(); MapaDeCasillas.ClearAllTiles(); }
    public void StartGame() { PauseMenu.EnPausa = false; Time.timeScale = 1f; SceneManager.LoadScene("JustMe"); }
    public void SavaGame() { SaveSystem.SavePlayer(this); }
    public void LoadGame()
    {
        GameData data = SaveSystem.LoadGame();
        if (data == null) return;
        currentTime = data.currentTime; Generacion(System.Math.Max(1, data.CantidadNiveles));
    }
    void OnCollisionEnter2D(Collision2D collision) { if (collision.gameObject.CompareTag("Jugador")) CompleteLevel(); }
    void OnTriggerEnter2D(Collider2D other) { if (other.CompareTag("Jugador")) CompleteLevel(); }
    void CompleteLevel()
    {
        if (lastGoalFrame == Time.frameCount) return;
        lastGoalFrame = Time.frameCount;
        if (Diary == null || !Diary.IsComplete) { Feedback.ShowMessage("Faltan trozos " + CollectedFragments + "/" + RequiredFragments); return; }
        try
        {
            Generacion(checked(Nivel + 1));
            if (bookSound != null)
            {
                if (bookSound.gameObject.scene.IsValid()) bookSound.Play();
                else if (bookSound.clip != null) AudioSource.PlayClipAtPoint(bookSound.clip, transform.position, bookSound.volume);
            }
        }
        catch (System.Exception error)
        {
            Debug.LogError("World generation rejected: " + error.Message, this);
            Feedback.ShowMessage("No se pudo crear el siguiente mundo");
        }
    }
}
