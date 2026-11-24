using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Temporary, focused Editor verification; archived outside Assets after use.
[InitializeOnLoad]
public static class AtmosphereProposalProbe
{
    const string Output = "Backups/ParallaxProposal/";
    static AtmosphereProposalProbe() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string path = "Temp/AtmosphereProposal/request";
        if (EditorApplication.isCompiling || !File.Exists(path)) return;
        string action;
        try { action = File.ReadAllText(path).Trim(); File.Delete(path); } catch (IOException) { return; }
        try
        {
            if (action == "baseline" || action == "live" || action == "next") Live(action);
            else if (action == "integrate") Integrate();
            else if (action == "mainmenu")
            {
                if (EditorApplication.isPlaying) SceneManager.LoadScene("MainMenu");
                else EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            }
            else if (action == "gameover")
            {
                if (EditorApplication.isPlaying) SceneManager.LoadScene("GameOver");
                else EditorSceneManager.OpenScene("Assets/Scenes/GameOver.unity");
            }
            else if (action == "menucheck") MenuCheck();
            else if (action == "fitmenu")
            {
                Require(!EditorApplication.isPlaying, "Stop Play before layout edit.");
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                Require(EditorSceneManager.SaveScene(scene), "MainMenu layout save failed.");
                File.WriteAllText(Output + "menu-fit.json", "{\"screenMatchMode\":\"Expand\",\"buttonBindingsChanged\":false}");
            }
        }
        catch (Exception e) { File.WriteAllText(Output + "error-" + action + ".txt", e.ToString()); }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Integrate()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play before integration.");
        for (int i = 0; i < SceneManager.sceneCount; i++) Require(!SceneManager.GetSceneAt(i).isDirty, "Preserve unsaved scene first.");
        foreach (string file in new[] { "ApprovedHouseMainMenu", "GameOverHouse" })
        {
            string path = "Assets/Art/MenuBackdrops/" + file + ".png";
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048; importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
        }
        var theme = Resources.Load<RuinedHouseTheme>("RuinedHouseTheme");
        var serialized = new SerializedObject(theme);
        var details = serialized.FindProperty("showDomesticDetails");
        Require(details != null, "Compile presentation changes before integration.");
        details.boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string name in new[] { "MainMenu", "GameOver" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                Require(!scene.GetRootGameObjects().Any(x => x.name == "Atmosphere Backdrop"), "Backdrop already installed: " + name);
                var root = new GameObject("Atmosphere Backdrop", typeof(RectTransform), typeof(Canvas));
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -100;
                string file = name == "MainMenu" ? "ApprovedHouseMainMenu" : "GameOverHouse";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/MenuBackdrops/" + file + ".png");
                Require(texture != null, "Missing menu texture.");
                var image = new GameObject("House illustration", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                image.transform.SetParent(root.transform, false);
                var graphic = image.GetComponent<RawImage>(); graphic.texture = texture; graphic.raycastTarget = false;
                var fit = image.GetComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = (float)texture.width / texture.height;
                var veil = new GameObject("Text contrast", typeof(RectTransform), typeof(Image)); veil.transform.SetParent(root.transform, false);
                var rect = veil.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var shade = veil.GetComponent<Image>(); shade.color = new Color(.025f, .04f, .065f, name == "MainMenu" ? .28f : .13f); shade.raycastTarget = false;
                Require(EditorSceneManager.SaveScene(scene), "Scene save failed: " + name);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        File.WriteAllText(Output + "integrated.json", "{\"menus\":2,\"domesticDetails\":false,\"assetsDeleted\":0,\"runtimeParallaxImplemented\":false}");
    }
    [Serializable] class LiveReport
    {
        public string scene, error; public bool playing, noPaperOrProps, physicalTilesPreserved, compositePathsPreserved, artworkHasNoColliders;
        public int level, required, props, backWalls, paperSolidTiles, paths;
    }
    static List<Vector2[]> Paths(CompositeCollider2D collider)
    {
        var result = new List<Vector2[]>();
        for (int i = 0; i < collider.pathCount; i++) { var points = new Vector2[collider.GetPathPointCount(i)]; collider.GetPath(i, points); result.Add(points); }
        return result;
    }
    static void Live(string action)
    {
        var report = new LiveReport { scene = SceneManager.GetActiveScene().name, playing = EditorApplication.isPlaying };
        try
        {
            Require(report.playing, "Requires Play Mode.");
            var book = UnityEngine.Object.FindAnyObjectByType<Libro>();
            Require(book != null && book.LastGeneratedWorld != null, "No generated world.");
            if (action == "next") book.Generacion(2);
            var map = book.MapaDeCasillas; var world = book.LastGeneratedWorld;
            report.level = book.Nivel; report.required = world.FragmentNodes.Length;
            var bounds = map.cellBounds; var tiles = map.GetTilesBlock(bounds); var collider = map.GetComponent<CompositeCollider2D>();
            var before = Paths(collider); var presentation = map.GetComponent<RuinedHousePresentation>();
            if (action != "baseline") presentation.Rebuild(world, map, Resources.Load<RuinedHouseTheme>("RuinedHouseTheme"));
            var root = map.transform.Find("House artwork"); Require(root != null, "Missing terrain skin.");
            report.props = root.GetComponentsInChildren<SpriteRenderer>(true).Length;
            report.backWalls = root.GetComponentsInChildren<Tilemap>(true).Count(x => x.name == "Domestic back walls");
            var skin = root.Find("House terrain skin").GetComponent<Tilemap>();
            var theme = Resources.Load<RuinedHouseTheme>("RuinedHouseTheme");
            var allowed = new HashSet<TileBase>(theme.stone.Concat(theme.timber));
            foreach (var tile in skin.GetTilesBlock(skin.cellBounds)) if (tile != null && !allowed.Contains(tile)) report.paperSolidTiles++;
            report.physicalTilesPreserved = tiles.SequenceEqual(map.GetTilesBlock(bounds));
            var after = Paths(collider); report.paths = after.Count;
            report.compositePathsPreserved = before.Count == after.Count && before.Select((p, i) => p.SequenceEqual(after[i])).All(x => x);
            report.artworkHasNoColliders = root.GetComponentsInChildren<Collider2D>(true).Length == 0;
            report.noPaperOrProps = report.props == 0 && report.backWalls == 0 && report.paperSolidTiles == 0;
            Require(report.physicalTilesPreserved && report.compositePathsPreserved && report.artworkHasNoColliders, "Physical geometry changed.");
            Require(report.noPaperOrProps, "Wallpaper or furniture is still visible.");
            ScreenCapture.CaptureScreenshot(Output + "gameplay-" + action + ".png");
        }
        catch (Exception e) { report.error = e.ToString(); }
        File.WriteAllText(Output + action + ".json", JsonUtility.ToJson(report, true));
    }
    [Serializable] class MenuReport { public string scene, texture; public bool playing, coversViewport, noRaycastTargets; public int eventSystems, buttons; public Vector2 imageSize, canvasSize, screenSize; }
    static void MenuCheck()
    {
        Require(EditorApplication.isPlaying, "Requires Play Mode.");
        Canvas.ForceUpdateCanvases();
        var root = GameObject.Find("Atmosphere Backdrop"); Require(root != null, "Missing backdrop.");
        var image = root.GetComponentInChildren<RawImage>(); var size = image.rectTransform.rect.size;
        var canvasSize = root.GetComponent<RectTransform>().rect.size;
        var report = new MenuReport { scene = SceneManager.GetActiveScene().name, playing = true,
            texture = AssetDatabase.GetAssetPath(image.texture), coversViewport = size.x >= canvasSize.x - 1 && size.y >= canvasSize.y - 1,
            imageSize = size, canvasSize = canvasSize, screenSize = new Vector2(Screen.width, Screen.height),
            noRaycastTargets = root.GetComponentsInChildren<Graphic>().All(x => !x.raycastTarget),
            eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>().Length,
            buttons = UnityEngine.Object.FindObjectsByType<Button>().Length };
        File.WriteAllText(Output + "menu-" + report.scene + ".json", JsonUtility.ToJson(report, true));
        Require(report.coversViewport && report.noRaycastTargets && report.eventSystems == 1, "Menu backdrop blocks or fails coverage.");
        ScreenCapture.CaptureScreenshot(Output + report.scene + ".png");
    }
}
