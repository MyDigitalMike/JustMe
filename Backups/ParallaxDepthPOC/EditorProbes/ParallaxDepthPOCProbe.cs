using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

[InitializeOnLoad]
public static class ParallaxDepthPOCProbe
{
    const string Output = "Backups/ParallaxDepthPOC/";
    static ParallaxDepthPOCProbe() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Temp/ParallaxDepthPOC/request";
        if (EditorApplication.isCompiling || !File.Exists(request)) return;
        string action; try { action = File.ReadAllText(request).Trim(); File.Delete(request); } catch (IOException) { return; }
        try
        {
            if (action == "build") Build();
            else if (action == "check") Check();
            else if (action == "capture") ScreenCapture.CaptureScreenshot(Output + "playmode.png");
            else if (action == "toggle") UnityEngine.Object.FindAnyObjectByType<ParallaxDepthDemo>().DepthEnabled = !UnityEngine.Object.FindAnyObjectByType<ParallaxDepthDemo>().DepthEnabled;
        }
        catch (Exception e) { File.WriteAllText(Output + "error-" + action + ".txt", e.ToString()); }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Build()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play first.");
        for (int i = 0; i < SceneManager.sceneCount; i++) Require(!SceneManager.GetSceneAt(i).isDirty, "Preserve unsaved scene first.");
        const string atlas = "Assets/Art/Prototypes/ParallaxDepth/Layers.png";
        AssetDatabase.ImportAsset(atlas);
        var importer = (TextureImporter)AssetImporter.GetAtPath(atlas);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
        importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false; importer.SetTextureSettings(settings);
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        string[] names = { "DepthFar", "DepthArches", "DepthRooms", "DepthNear" };
        var rects = names.Select((name, i) => new SpriteRect { name = name, spriteID = GUID.Generate(), alignment = SpriteAlignment.Center,
            pivot = new Vector2(.5f, .5f), rect = new Rect(i % 2 * (width / 2), (1 - i / 2) * (height / 2), width / 2, height / 2) }).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(atlas).OfType<Sprite>().ToDictionary(s => s.name);
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"); Require(shader != null, "Missing installed unlit sprite shader.");
        string materialPath = "Assets/Art/Prototypes/ParallaxDepth/Unlit.mat";
        Require(!File.Exists(materialPath), "Prototype already built; do not regenerate its assets.");
        var material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Character/Player.prefab"));
        player.transform.position = new Vector3(0, 1, 0); Physics2D.SyncTransforms();
        float sole = player.GetComponentsInChildren<Collider2D>().Where(c => !c.isTrigger).Min(c => c.bounds.min.y);
        player.transform.position += Vector3.up * (-2.5f - sole + .03f);
        var playerRenderer = player.GetComponent<SpriteRenderer>();
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CinemachineBrain)); cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 4.5f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .04f, .065f); camera.transform.position = new Vector3(0, 0, -10);
        var virtualObject = new GameObject("Prototype follow", typeof(CinemachineCamera), typeof(CinemachinePositionComposer));
        var virtualCamera = virtualObject.GetComponent<CinemachineCamera>(); virtualCamera.Follow = player.transform; virtualCamera.Lens.OrthographicSize = 4.5f;
        virtualObject.GetComponent<CinemachinePositionComposer>().TargetOffset = Vector3.up * 2;
        var light = new GameObject("Prototype global light", typeof(Light2D)).GetComponent<Light2D>(); light.lightType = Light2D.LightType.Global; light.intensity = 1;
        var root = new GameObject("Parallax Depth Prototype", typeof(ParallaxDepthDemo)); var demo = root.GetComponent<ParallaxDepthDemo>();
        demo.targetCamera = camera; demo.layers = new SpriteRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            var plane = new GameObject(names[i], typeof(SpriteRenderer)); plane.transform.SetParent(root.transform, false);
            var renderer = plane.GetComponent<SpriteRenderer>(); renderer.sprite = sprites[names[i]]; renderer.sharedMaterial = material;
            renderer.sortingLayerID = playerRenderer.sortingLayerID; renderer.sortingOrder = playerRenderer.sortingOrder - 40 + i;
            plane.transform.position = new Vector3(0, 0, 5 + i);
            plane.transform.localScale = Vector3.one * (14f / renderer.sprite.bounds.size.y);
            if (i == 1) renderer.color = new Color(.65f, .72f, .8f, .85f);
            if (i == 2) renderer.color = new Color(.68f, .73f, .8f, .9f);
            demo.layers[i] = renderer;
        }
        var theme = Resources.Load<RuinedHouseTheme>("RuinedHouseTheme");
        Sprite timber = ((Tile)theme.timber[0]).sprite; Sprite stone = ((Tile)theme.stone[0]).sprite;
        Solid("Test floor", new Vector2(0, -3), new Vector2(12, 1), timber, material, playerRenderer);
        Solid("Left test wall", new Vector2(-5.8f, -.5f), new Vector2(.4f, 4), stone, material, playerRenderer);
        Solid("Right test wall", new Vector2(5.8f, -.5f), new Vector2(.4f, 4), stone, material, playerRenderer);
        Directory.CreateDirectory("Assets/Scenes/Prototypes");
        Require(EditorSceneManager.SaveScene(scene, "Assets/Scenes/Prototypes/ParallaxDepthTest.unity"), "Scene save failed.");
        AssetDatabase.SaveAssets(); File.WriteAllText(Output + "built.json", "{\"layers\":4,\"sourcePlayerPrefabUnchanged\":true,\"addedToBuildSettings\":false}");
    }
    static void Solid(string name, Vector2 position, Vector2 size, Sprite sprite, Material material, SpriteRenderer player)
    {
        var obj = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D)); obj.layer = 10; obj.transform.position = position;
        obj.GetComponent<BoxCollider2D>().size = size;
        var sr = obj.GetComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sharedMaterial = material; sr.sortingLayerID = player.sortingLayerID; sr.sortingOrder = player.sortingOrder;
        obj.transform.localScale = Vector3.one;
        // Scale the visual child only: the explicit world-size support collider remains unchanged.
        var art = new GameObject("Surface", typeof(SpriteRenderer)); art.transform.SetParent(obj.transform, false);
        var visual = art.GetComponent<SpriteRenderer>(); visual.sprite = sprite; visual.sharedMaterial = material; visual.sortingLayerID = sr.sortingLayerID; visual.sortingOrder = sr.sortingOrder;
        art.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1); UnityEngine.Object.DestroyImmediate(sr);
    }
    [Serializable] class Report { public string error; public bool distinctDepths, xyMotion, stoppedStable, toggleNoSnap, flatMode, restored, noLayerColliders; public float[] observedSpeeds; }
    static void Check()
    {
        var result = new Report();
        try
        {
            Require(EditorApplication.isPlaying, "Requires Play Mode."); var demo = UnityEngine.Object.FindAnyObjectByType<ParallaxDepthDemo>();
            var cam = demo.targetCamera; var brain = cam.GetComponent<CinemachineBrain>(); bool brainEnabled = brain.enabled; brain.enabled = false;
            var cameraPosition = cam.transform.position; var positions = demo.layers.Select(x => x.transform.position).ToArray();
            try
            {
                demo.DepthEnabled = true; demo.Rebase(); cam.transform.position += new Vector3(4, 1, 0); demo.RefreshLayers();
                result.observedSpeeds = demo.layers.Select((x, i) => 1 - (x.transform.position.x - positions[i].x) / 4).ToArray();
                result.distinctDepths = result.observedSpeeds.Select((x, i) => Mathf.Abs(x - demo.apparentSpeeds[i]) < .0001f).All(x => x);
                result.xyMotion = demo.layers.Select((x, i) => Mathf.Abs((x.transform.position.y - positions[i].y) - (1 - demo.apparentSpeeds[i])) < .0001f).All(x => x);
                var moved = demo.layers.Select(x => x.transform.position).ToArray(); demo.RefreshLayers(); result.stoppedStable = demo.layers.Select((x, i) => x.transform.position == moved[i]).All(x => x);
                demo.DepthEnabled = false; demo.RefreshLayers(); result.toggleNoSnap = demo.layers.Select((x, i) => x.transform.position == moved[i]).All(x => x);
                cam.transform.position += Vector3.right * 2; demo.RefreshLayers(); result.flatMode = demo.layers.Select((x, i) => x.transform.position == moved[i]).All(x => x);
                result.noLayerColliders = demo.layers.All(x => x.GetComponent<Collider2D>() == null);
                Require(result.distinctDepths && result.xyMotion && result.stoppedStable && result.toggleNoSnap && result.flatMode && result.noLayerColliders, "Depth comparison failed.");
            }
            finally { cam.transform.position = cameraPosition; for (int i = 0; i < demo.layers.Length; i++) demo.layers[i].transform.position = positions[i]; demo.DepthEnabled = true; demo.Rebase(); brain.enabled = brainEnabled; result.restored = true; }
        }
        catch (Exception e) { result.error = e.ToString(); }
        File.WriteAllText(Output + "checks.json", JsonUtility.ToJson(result, true));
    }
}
