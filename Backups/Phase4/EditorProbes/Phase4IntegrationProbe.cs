using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

// Temporary Play Mode checks; runtime changes are never saved to scenes.
[InitializeOnLoad]
public static class Phase4IntegrationProbe
{
    [Serializable] public class Check { public string name, detail; public bool passed; }
    [Serializable] public class Snapshot
    {
        public string label, scene, signature; public int seed, level, collected, required, pages, lights;
        public float stamina; public List<string> hud = new List<string>();
    }
    [Serializable] public class Report
    {
        public string error, editorVersion; public bool complete;
        public List<Check> checks = new List<Check>(); public List<Snapshot> snapshots = new List<Snapshot>();
    }
    static Report report; static IEnumerator<int> routine; static int until;
    static Phase4IntegrationProbe() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (routine == null)
            {
                const string path = "Temp/Phase4/integration.request";
                if (!File.Exists(path)) return;
                try { File.ReadAllText(path); File.Delete(path); } catch (IOException) { return; }
                report = new Report { editorVersion = Application.unityVersion }; routine = Run(); until = 0;
            }
            if (Time.frameCount < until) return;
            if (routine.MoveNext()) until = Time.frameCount + routine.Current;
            else { report.complete = true; Write(); routine.Dispose(); routine = null; }
        }
        catch (Exception error)
        {
            report.error = error.ToString(); Write(); routine = null; Time.timeScale = 1f;
        }
    }
    static void Write() { File.WriteAllText("Backups/Phase4/integration.json", JsonUtility.ToJson(report, true)); }
    static void Expect(string name, bool condition, string detail = "")
    { report.checks.Add(new Check { name = name, passed = condition, detail = detail }); Write(); }
    static Libro Book() { return UnityEngine.Object.FindAnyObjectByType<Libro>(); }
    static Player Actor(Libro book) { return book.Jugador.GetComponent<Player>(); }
    static void Warp(Player player, Vector3 destination)
    {
        var body = player.GetComponent<Rigidbody2D>(); Vector3 delta = destination - player.transform.position;
        player.transform.position = destination; body.position = destination; body.linearVelocity = Vector2.zero; player.NotifyWarp();
        CinemachineCore.OnTargetObjectWarped(player.transform, delta);
        if (Camera.main != null) Camera.main.transform.position += delta;
        Physics2D.SyncTransforms();
    }
    static Snapshot Capture(string label)
    {
        var s = new Snapshot { label = label, scene = SceneManager.GetActiveScene().name };
        Libro b = Book();
        if (b != null && b.LastGeneratedWorld != null)
        {
            s.level = b.Nivel; s.seed = b.CurrentSeed; s.signature = b.CurrentSignature; s.required = b.RequiredFragments;
            s.collected = b.CollectedFragments; s.stamina = Actor(b).Stamina.Current;
        }
        s.pages = UnityEngine.Object.FindObjectsByType<DiaryPage>(FindObjectsSortMode.None).Length;
        s.lights = UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None).Length;
        GameObject canvas = GameObject.Find("Canvas GUI");
        if (canvas != null) foreach (Transform child in canvas.transform) if (child.gameObject.activeInHierarchy) s.hud.Add(child.name);
        report.snapshots.Add(s); Write(); return s;
    }
    static IEnumerator<int> Collect(Libro book)
    {
        Player p = Actor(book); p.enabled = false;
        for (int id = 0; id < book.RequiredFragments; id++)
        {
            int node = book.LastGeneratedWorld.FragmentNodes[id];
            Warp(p, WorldTilemapRenderer.StandingPosition(book.LastGeneratedWorld, book.MapaDeCasillas, book.LastGeneratedWorld.Route.Nodes[node], p.SoleOffset));
            yield return 10;
            Expect("Real overlap collects page " + (id + 1) + " at level " + book.Nivel, book.CollectedFragments == id + 1, "count=" + book.CollectedFragments);
        }
    }
    static void ClickButton(string name)
    {
        GameObject go = GameObject.Find(name); if (go == null) throw new Exception("Missing navigation button " + name);
        Button button = go.GetComponent<Button>(); if (button == null || !button.interactable) throw new Exception("Inactive navigation button " + name);
        button.onClick.Invoke();
    }
    static IEnumerator<int> Run()
    {
        yield return 15;
        Libro b = Book(); if (b == null || b.LastGeneratedWorld == null) throw new Exception("No accepted runtime world");
        Player p = Actor(b); p.enabled = false;
        Snapshot initial = Capture("normal HUD");
        Expect("Initial level has three pages", initial.level == 1 && initial.pages == 3 && initial.collected == 0);
        Expect("Normal HUD only Pause", initial.hud.Count == 1 && initial.hud[0] == "ButtonPause", string.Join(",", initial.hud));
        Expect("No legacy entities", UnityEngine.Object.FindObjectsByType<Enemigo>(FindObjectsSortMode.None).Length == 0 && UnityEngine.Object.FindObjectsByType<CoinManager>(FindObjectsSortMode.None).Length == 0 && UnityEngine.Object.FindObjectsByType<JumpingHard>(FindObjectsSortMode.None).Length == 0);
        bool triggerOnly = true;
        foreach (DiaryPage page in UnityEngine.Object.FindObjectsByType<DiaryPage>(FindObjectsSortMode.None))
            triggerOnly &= page.GetComponent<Collider2D>().isTrigger && page.GetComponent<Rigidbody2D>() == null && page.GetComponent<Light2D>() == null;
        Expect("Pages add no support or lights", triggerOnly && initial.lights == 2);
        AudioSource music = GameObject.Find("BackgroundSound").GetComponent<AudioSource>();
        Expect("Gameplay music disabled", !music.enabled && !music.playOnAwake && !music.isPlaying);
        bool parallaxOff = true;
        foreach (ParallaxBackground item in UnityEngine.Object.FindObjectsByType<ParallaxBackground>(FindObjectsInactive.Include, FindObjectsSortMode.None)) parallaxOff &= !item.enabled;
        foreach (ScrollingBackground item in UnityEngine.Object.FindObjectsByType<ScrollingBackground>(FindObjectsInactive.Include, FindObjectsSortMode.None)) parallaxOff &= !item.enabled && (item.bgRend == null || !item.bgRend.enabled);
        WorldLighting lighting = UnityEngine.Object.FindAnyObjectByType<WorldLighting>();
        foreach (SpriteRenderer sprite in lighting.GetComponentsInChildren<SpriteRenderer>(true)) parallaxOff &= !sprite.gameObject.activeInHierarchy;
        Expect("Old parallax presentation disabled", parallaxOff);
        b.Feedback.DiagnosticsEnabled = true; yield return 20;
        Snapshot diagnostics = Capture("diagnostics explicitly on");
        Expect("Diagnostic toggle displays stamina", b.Feedback.DiagnosticsEnabled && diagnostics.hud.Contains("CountDown"));
        b.Feedback.DiagnosticsEnabled = false; yield return 5;
        Expect("Diagnostic toggle hides stamina", !Capture("diagnostics off").hud.Contains("CountDown"));
        PauseMenu pause = UnityEngine.Object.FindAnyObjectByType<PauseMenu>();
        pause.Pause(); yield return 5; Expect("Pause works", Time.timeScale == 0 && PauseMenu.EnPausa && pause.pauseMenuUI.activeInHierarchy);
        pause.Resume(); yield return 5; Expect("Resume works", Time.timeScale == 1 && !PauseMenu.EnPausa && !pause.pauseMenuUI.activeInHierarchy);
        Warp(p, b.Elemento.transform.position); yield return 15;
        Expect("Exit locked before collection", b.Nivel == 1 && b.CurrentSeed == initial.seed && b.CollectedFragments == 0);
        var collect = Collect(b); while (collect.MoveNext()) yield return collect.Current;
        Expect("All initial pages collected once", b.Diary.IsComplete && b.CollectedFragments == 3 && UnityEngine.Object.FindObjectsByType<DiaryPage>(FindObjectsSortMode.None).Length == 0);
        Expect("Duplicate and stale page callbacks rejected", !b.TryCollectFragment(0, b.LevelRevision) && !b.TryCollectFragment(0, b.LevelRevision - 1));
        p.Stamina.TrySpend(50); float spent = p.Stamina.Current;
        Warp(p, b.Elemento.transform.position); yield return 15;
        Snapshot second = Capture("level 2");
        Expect("Completed exit advances once and resets pages", second.level == 2 && second.required == 4 && second.collected == 0 && second.pages == 4);
        Expect("Next level has fresh geometry and seed", second.seed != initial.seed && second.signature != initial.signature);
        Expect("Level transition preserves stamina", Mathf.Abs(second.stamina - spent) < .01f, "before=" + spent + ",after=" + second.stamina);
        yield return 100; Expect("Transient feedback disappears", !Capture("no permanent feedback").hud.Contains("Objective feedback"));
        b.Generacion(3); yield return 10;
        collect = Collect(b); while (collect.MoveNext()) yield return collect.Current;
        Warp(p, b.Elemento.transform.position); yield return 15;
        Snapshot fourth = Capture("continuous level 4");
        Expect("Level 3 continues to 4 instead of Credits", fourth.scene == "JustMe" && fourth.level == 4 && fourth.required == 6 && fourth.collected == 0);
        b.Generacion(10); yield return 10;
        Expect("Runtime level 10 requires cap of twelve", b.RequiredFragments == 12 && Capture("level 10 cap").pages == 12);
        pause.LoadMenu(); yield return 20;
        Expect("Pause menu navigation preserved", SceneManager.GetActiveScene().name == "MainMenu");
        ClickButton("IniciarJuego"); yield return 30;
        Snapshot firstRun = Capture("independent new game 1");
        Expect("New run resets progression", firstRun.scene == "JustMe" && firstRun.level == 1 && firstRun.required == 3 && firstRun.collected == 0 && firstRun.stamina == 100);
        UnityEngine.Object.FindAnyObjectByType<PauseMenu>().LoadMenu(); yield return 20;
        ClickButton("IniciarJuego"); yield return 30;
        Snapshot secondRun = Capture("independent new game 2");
        Expect("Independent New Games differ", secondRun.seed != firstRun.seed && secondRun.signature != firstRun.signature);
        b = Book(); p = Actor(b); p.enabled = true;
        Warp(p, b.MapaDeCasillas.GetComponent<TraversalEnvironment>().voidAreas[0].center); yield return 30;
        Expect("Lethal void reaches GameOver", SceneManager.GetActiveScene().name == "GameOver");
        UnityEngine.Object.FindAnyObjectByType<MainMenu>().QuitMenu(); yield return 20;
        Expect("GameOver returns to MainMenu", SceneManager.GetActiveScene().name == "MainMenu");
        ClickButton("IniciarJuego"); yield return 30;
        Capture("final fresh gameplay");
    }
}
