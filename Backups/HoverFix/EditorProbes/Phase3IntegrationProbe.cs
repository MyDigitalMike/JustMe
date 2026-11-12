using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

[InitializeOnLoad]
public static class Phase3IntegrationProbe
{
    [Serializable] class Snapshot
    {
        public string scene, state, error;
        public int enemies, coins, springs, lights, seed, level;
        public float stamina, radius, ambientIntensity;
        public bool backgroundBlack, ordinaryFallSafe, goalAdvanced, staminaPreserved, voidReachedGameOver;
        public Vector3 player;
    }
    static string pending;
    static int frame, expectedLevel;
    static float beforeStamina;
    static Vector3 safeSpawn;
    static Phase3IntegrationProbe() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (pending != null && Time.frameCount > frame + 90)
        {
            Snapshot result = Capture();
            if (pending == "void") result.voidReachedGameOver = result.scene == "GameOver";
            else if (pending == "goal") { result.goalAdvanced = result.level == expectedLevel; result.staminaPreserved = result.stamina < beforeStamina + 15; }
            else if (pending == "fall") result.ordinaryFallSafe = result.scene == "JustMe" && Vector3.Distance(result.player, safeSpawn) < .3f;
            File.WriteAllText("Backups/HoverFix/integration-" + pending + ".json", JsonUtility.ToJson(result, true)); pending = null;
        }
        const string request = "Temp/JustMePhase3/integration.request";
        if (!File.Exists(request)) return;
        string label; try { label = File.ReadAllText(request).Trim(); File.Delete(request); } catch (IOException) { return; }
        try
        {
            if (label == "snapshot") { File.WriteAllText("Backups/HoverFix/integration-snapshot.json", JsonUtility.ToJson(Capture(), true)); return; }
            Libro book = UnityEngine.Object.FindAnyObjectByType<Libro>();
            Player player = book.Jugador.GetComponent<Player>(); Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            Vector3 destination;
            if (label == "goal")
            {
                player.Stamina.TrySpend(50); beforeStamina = player.Stamina.Current; expectedLevel = book.Nivel + 1;
                destination = book.Elemento.transform.position;
            }
            else if (label == "void") destination = book.MapaDeCasillas.GetComponent<TraversalEnvironment>().voidAreas[0].center;
            else
            {
                safeSpawn = WorldTilemapRenderer.StandingPosition(book.LastGeneratedWorld, book.MapaDeCasillas, book.LastGeneratedWorld.Route.Nodes[0], player.SoleOffset);
                destination = safeSpawn + Vector3.up * .35f;
            }
            Vector3 delta = destination - player.transform.position;
            player.transform.position = destination; body.position = destination; body.linearVelocity = Vector2.zero; player.NotifyWarp();
            CinemachineCore.OnTargetObjectWarped(player.transform, delta); Camera.main.transform.position += delta;
            pending = label; frame = Time.frameCount;
        }
        catch (Exception e) { File.WriteAllText("Backups/HoverFix/integration-error.json", JsonUtility.ToJson(new Snapshot { error = e.ToString() }, true)); }
    }
    static Snapshot Capture()
    {
        var result = new Snapshot { scene = SceneManager.GetActiveScene().name };
        Player player = UnityEngine.Object.FindAnyObjectByType<Player>();
        if (player != null) { result.player = player.transform.position; result.state = player.State.ToString(); result.stamina = player.Stamina.Current; }
        Libro book = UnityEngine.Object.FindAnyObjectByType<Libro>();
        if (book != null && book.LastGeneratedWorld != null) { result.seed = book.LastGeneratedWorld.Seed; result.level = book.Nivel; }
        result.enemies = UnityEngine.Object.FindObjectsByType<Enemigo>(FindObjectsSortMode.None).Length;
        result.coins = UnityEngine.Object.FindObjectsByType<CoinManager>(FindObjectsSortMode.None).Length;
        result.springs = UnityEngine.Object.FindObjectsByType<JumpingHard>(FindObjectsSortMode.None).Length;
        Light2D[] lights = UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None); result.lights = lights.Length;
        foreach (Light2D light in lights) if (light.lightType == Light2D.LightType.Global) result.ambientIntensity += light.intensity;
        WorldLighting lighting = UnityEngine.Object.FindAnyObjectByType<WorldLighting>();
        if (lighting != null)
        {
            result.radius = lighting.playerLight.pointLightOuterRadius; result.backgroundBlack = Camera.main != null && Camera.main.clearFlags == CameraClearFlags.SolidColor && Camera.main.backgroundColor.maxColorComponent < .001f;
            foreach (SpriteRenderer sprite in lighting.GetComponentsInChildren<SpriteRenderer>()) result.backgroundBlack &= sprite.color.maxColorComponent < .001f;
            if (Camera.main != null)
                foreach (ScrollingBackground background in Camera.main.GetComponentsInChildren<ScrollingBackground>(true))
                    if (background.bgRend != null) result.backgroundBlack &= !background.bgRend.enabled ||
                        (background.bgRend.sharedMaterial.HasProperty("_BaseColor") && background.bgRend.sharedMaterial.GetColor("_BaseColor").maxColorComponent < .001f);
        }
        return result;
    }
}
