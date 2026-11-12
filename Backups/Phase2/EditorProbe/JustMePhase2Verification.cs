using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityStandardAssets.CrossPlatformInput;
using UnityEngine.Tilemaps;
using Unity.Cinemachine;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
internal static class JustMePhase2Verification
{
    const string Dir = "Temp/JustMePhase2";
    [Serializable] class Report
    {
        public List<Sample> samples = new List<Sample>(); public string error;
        public bool walking, stopped, jumped, landed, fellFromEdge, wallNotGround;
        public bool counterHidden, generated, composite, parallaxStationary, parallaxMoved, parallaxCoverage;
        public int missingScripts, seed;
        public bool doubleJump, cooldownRetainsDoubleJump, thirdJumpRejected, walkedLeft;
        public float seamHeightRange;
    }
    [Serializable] class Sample
    {
        public float startX, endX, endY, minimumNormalY = 1, maximumNormalY;
        public bool circleSolid, supported, reportedGrounded;
    }
    static JustMePhase2Verification() { EditorApplication.update += Poll; }
    static int goalExpected, goalFrame;
    [Serializable] class GoalReport { public int expectedLevel, actualLevel, seed, enemies, springs; public bool advanced, safeSpawn, cameraContainsPlayer; public Vector3 player; }
    static void Poll()
    {
        if (goalExpected != 0 && EditorApplication.isPlaying && Time.frameCount > goalFrame + 10)
        {
            Libro book = UnityEngine.Object.FindAnyObjectByType<Libro>();
            Vector3 spawn = WorldTilemapRenderer.CellCenter(book.MapaDeCasillas, book.LastGeneratedWorld.SpawnX, book.LastGeneratedWorld.SpawnY);
            Vector3 view = Camera.main.WorldToViewportPoint(book.Jugador.transform.position);
            var result = new GoalReport { expectedLevel = goalExpected, actualLevel = book.Nivel, seed = book.LastGeneratedWorld.Seed,
                advanced = book.Nivel == goalExpected, player = book.Jugador.transform.position,
                safeSpawn = Mathf.Abs(book.Jugador.transform.position.x - spawn.x) < .3f && Mathf.Abs(book.Jugador.transform.position.y - spawn.y) < .5f,
                cameraContainsPlayer = view.x >= 0 && view.x <= 1 && view.y >= 0 && view.y <= 1 && view.z > 0,
                enemies = GameObject.FindGameObjectsWithTag("CloneEnemy").Length, springs = GameObject.FindGameObjectsWithTag("CloneHardJump").Length };
            File.WriteAllText(Dir + "/goal-" + goalExpected + ".json", JsonUtility.ToJson(result, true)); goalExpected = 0;
        }
        string request = Dir + "/probe";
        if (EditorApplication.isCompiling || !File.Exists(request)) return;
        string label;
        try { label = File.ReadAllText(request).Trim(); File.Delete(request); }
        catch (IOException) { return; }
        if (label == "references" && !EditorApplication.isPlaying)
        {
            var results = new List<string>();
            foreach (string path in new[] { "Assets/Scenes/JustMe.unity", "Assets/Scenes/GameOver.unity", "Assets/Scenes/MainMenu.unity" })
            {
                Scene preview = EditorSceneManager.OpenPreviewScene(path);
                try { int count = 0; foreach (GameObject root in preview.GetRootGameObjects()) count += MissingReferences(root); results.Add(path + ": " + count); }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            foreach (string path in new[] { "Assets/Prefabs/Character/Player.prefab", "Assets/Prefabs/Character/Libro.prefab", "Assets/Prefabs/Tilemap.prefab", "Assets/Resources/WorldPresentation.prefab" })
                results.Add(path + ": " + MissingReferences(AssetDatabase.LoadAssetAtPath<GameObject>(path)));
            File.WriteAllLines(Dir + "/references.txt", results); return;
        }
        if (label == "prepare" && !EditorApplication.isPlaying)
        {
            try { PreparePresentation(); File.WriteAllText(Dir + "/prepared.txt", "ok"); }
            catch (Exception e) { File.WriteAllText(Dir + "/prepared.txt", e.ToString()); }
            return;
        }
        if (!EditorApplication.isPlaying) return;
        if (label == "goal")
        {
            Libro book = UnityEngine.Object.FindAnyObjectByType<Libro>();
            goalExpected = book.Nivel + 1; goalFrame = Time.frameCount;
            Vector3 delta = book.Elemento.transform.position - book.Jugador.transform.position;
            book.Jugador.transform.position += delta;
            Rigidbody2D body = book.Jugador.GetComponent<Rigidbody2D>(); body.position = book.Jugador.transform.position; body.linearVelocity = Vector2.zero;
            CinemachineCore.OnTargetObjectWarped(book.Jugador.transform, delta); Camera.main.transform.position += delta;
            Physics2D.SyncTransforms(); return;
        }
        var report = new Report();
        try
        {
            Player player = UnityEngine.Object.FindAnyObjectByType<Player>();
            if (player == null) throw new Exception("Open JustMe in Play Mode.");
            foreach (float x in new[] { 0.5f, 1.0f, 1.15f, 1.25f, 1.32f })
                foreach (bool solid in new[] { true, false }) report.samples.Add(Measure(player, x, solid));
            TestMovement(player, report);
            Libro libro = UnityEngine.Object.FindAnyObjectByType<Libro>();
            report.generated = libro != null && libro.LastGeneratedWorld != null;
            if (report.generated) report.seed = libro.LastGeneratedWorld.Seed;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    report.missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
            var serialized = new SerializedObject(libro);
            var counter = serialized.FindProperty("Enemys").objectReferenceValue as UnityEngine.UI.Text;
            report.counterHidden = counter != null && !counter.gameObject.activeInHierarchy;
            report.composite = libro.MapaDeCasillas.GetComponent<CompositeCollider2D>().pathCount > 0;
            TestParallax(report);
        }
        catch (Exception e) { report.error = e.ToString(); }
        File.WriteAllText(Dir + "/" + label + ".json", JsonUtility.ToJson(report, true));
    }
    static Sample Measure(Player source, float x, bool circleSolid)
    {
        Scene old = SceneManager.GetActiveScene();
        Scene scene = SceneManager.CreateScene("Phase2 contact probe " + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        try
        {
            SceneManager.SetActiveScene(scene);
            var floor = new GameObject("platform", typeof(BoxCollider2D));
            floor.layer = 10; floor.transform.position = new Vector3(0.5f, -0.5f);
            var player = UnityEngine.Object.Instantiate(source.gameObject, new Vector3(x, 1.2f), Quaternion.identity);
            Player controller = player.GetComponent<Player>(); controller.enabled = false;
            foreach (MonoBehaviour script in player.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != controller) UnityEngine.Object.DestroyImmediate(script);
            foreach (Animator animator in player.GetComponentsInChildren<Animator>()) animator.enabled = false;
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            player.GetComponentInChildren<CircleCollider2D>().isTrigger = !circleSolid;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>(); body.linearVelocity = Vector2.zero;
            var contacts = new ContactPoint2D[16]; var result = new Sample { startX = x, circleSolid = circleSolid };
            for (int i = 0; i < 200; i++) scene.GetPhysicsScene2D().Simulate(0.02f);
            int count = body.GetContacts(contacts);
            for (int i = 0; i < count; i++)
            {
                result.minimumNormalY = Mathf.Min(result.minimumNormalY, contacts[i].normal.y);
                result.maximumNormalY = Mathf.Max(result.maximumNormalY, contacts[i].normal.y);
            }
            result.supported = count > 0 && Mathf.Abs(body.linearVelocity.y) < 0.05f;
            controller.SendMessage("FixedUpdate"); result.reportedGrounded = controller.Grounded;
            result.endX = body.position.x; result.endY = body.position.y;
            return result;
        }
        finally { SceneManager.SetActiveScene(old); UnityEngine.Object.DestroyImmediate(scene.GetRootGameObjects()[0]); EditorSceneCleanup(scene); }
    }
    static void EditorSceneCleanup(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
        SceneManager.UnloadSceneAsync(scene);
    }

    static void TestMovement(Player source, Report report)
    {
        Scene old = SceneManager.GetActiveScene();
        Scene scene = SceneManager.CreateScene("Phase2 movement " + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        try
        {
            SceneManager.SetActiveScene(scene);
            var grid = new GameObject("grid", typeof(Grid));
            var floor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tilemap.prefab"));
            floor.transform.SetParent(grid.transform, false);
            Tilemap map = floor.GetComponent<Tilemap>(); map.ClearAllTiles();
            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Casillas/SueloAvanzado.asset");
            for (int x = -2; x < 6; x++) for (int y = -3; y < 0; y++) map.SetTile(new Vector3Int(x, y, 0), tile);
            floor.GetComponent<TilemapCollider2D>().ProcessTilemapChanges(); floor.GetComponent<CompositeCollider2D>().GenerateGeometry();
            GameObject actor = UnityEngine.Object.Instantiate(source.gameObject, new Vector3(0, 1), Quaternion.identity);
            Player player = actor.GetComponent<Player>(); player.enabled = false;
            foreach (MonoBehaviour script in actor.GetComponentsInChildren<MonoBehaviour>(true)) if (script != player) UnityEngine.Object.DestroyImmediate(script);
            foreach (Animator animator in actor.GetComponentsInChildren<Animator>()) animator.enabled = false;
            Rigidbody2D body = actor.GetComponent<Rigidbody2D>(); body.linearVelocity = Vector2.zero;
            CrossPlatformInputManager.SwitchActiveInputMethod(CrossPlatformInputManager.ActiveInputMethod.Touch);
            CrossPlatformInputManager.SetAxisZero("Horizontal");
            for (int i = 0; i < 120; i++) { player.SendMessage("Update"); player.SendMessage("FixedUpdate"); scene.GetPhysicsScene2D().Simulate(.02f); }
            float startX = body.position.x;
            CrossPlatformInputManager.SetAxisPositive("Horizontal");
            float minimumY = body.position.y, maximumY = body.position.y;
            for (int i = 0; i < 100; i++) { player.SendMessage("Update"); player.SendMessage("FixedUpdate"); scene.GetPhysicsScene2D().Simulate(.02f); minimumY = Mathf.Min(minimumY, body.position.y); maximumY = Mathf.Max(maximumY, body.position.y); }
            report.walking = body.position.x - startX > 1f;
            float rightX = body.position.x; CrossPlatformInputManager.SetAxisNegative("Horizontal");
            for (int i = 0; i < 100; i++) { player.SendMessage("Update"); player.SendMessage("FixedUpdate"); scene.GetPhysicsScene2D().Simulate(.02f); minimumY = Mathf.Min(minimumY, body.position.y); maximumY = Mathf.Max(maximumY, body.position.y); }
            report.walkedLeft = rightX - body.position.x > 1f; report.seamHeightRange = maximumY - minimumY;
            CrossPlatformInputManager.SetAxisZero("Horizontal"); player.SendMessage("Update"); player.SendMessage("FixedUpdate");
            report.stopped = Mathf.Abs(body.linearVelocity.x) < .001f;
            float startY = body.position.y;
            var queue = typeof(Player).GetField("jumpQueued", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            queue.SetValue(player, true); player.jumpBufferTime = 0; player.SendMessage("FixedUpdate");
            scene.GetPhysicsScene2D().Simulate(.02f);
            float firstVelocity = body.linearVelocity.y;
            queue.SetValue(player, true); player.SendMessage("FixedUpdate"); scene.GetPhysicsScene2D().Simulate(.02f);
            bool cooldownBlocked = body.linearVelocity.y < firstVelocity;
            player.jumpCooldown = 0; float beforeDouble = body.linearVelocity.y;
            queue.SetValue(player, true); player.SendMessage("FixedUpdate"); scene.GetPhysicsScene2D().Simulate(.02f);
            report.doubleJump = body.linearVelocity.y > beforeDouble + 2f;
            report.cooldownRetainsDoubleJump = cooldownBlocked && report.doubleJump;
            float beforeThird = body.linearVelocity.y;
            queue.SetValue(player, true); player.SendMessage("FixedUpdate"); scene.GetPhysicsScene2D().Simulate(.02f);
            report.thirdJumpRejected = body.linearVelocity.y < beforeThird;
            float highest = body.position.y;
            for (int i = 0; i < 130; i++) { scene.GetPhysicsScene2D().Simulate(.02f); player.SendMessage("FixedUpdate"); highest = Mathf.Max(highest, body.position.y); }
            report.jumped = highest - startY > .5f;
            report.landed = player.Grounded && Mathf.Abs(body.position.y - startY) < .02f;
            body.position = new Vector2(7, 1); body.linearVelocity = Vector2.zero;
            for (int i = 0; i < 80; i++) { scene.GetPhysicsScene2D().Simulate(.02f); player.SendMessage("FixedUpdate"); }
            report.fellFromEdge = body.position.y < -5 && !player.Grounded;
            var wall = new GameObject("wall", typeof(BoxCollider2D)); wall.layer = 10; wall.transform.position = new Vector3(10, 0);
            wall.GetComponent<BoxCollider2D>().size = new Vector2(1, 20);
            body.position = new Vector2(9.2f, 2); body.linearVelocity = new Vector2(2, -1);
            for (int i = 0; i < 6; i++) scene.GetPhysicsScene2D().Simulate(.02f);
            player.SendMessage("FixedUpdate"); report.wallNotGround = !player.Grounded;
        }
        finally
        {
            CrossPlatformInputManager.SetAxisZero("Horizontal");
            CrossPlatformInputManager.SwitchActiveInputMethod(CrossPlatformInputManager.ActiveInputMethod.Hardware);
            SceneManager.SetActiveScene(old); EditorSceneCleanup(scene);
        }
    }

    static void TestParallax(Report report)
    {
        ParallaxBackground p = UnityEngine.Object.FindAnyObjectByType<ParallaxBackground>();
        if (p == null || p.layers == null || p.layers.Length < 2) return;
        Vector3 position = p.layers[0].copies[1].transform.position;
        p.SendMessage("LateUpdate"); p.SendMessage("LateUpdate");
        report.parallaxStationary = position == p.layers[0].copies[1].transform.position;
        Vector3 camera = p.targetCamera.transform.position;
        try
        {
            p.targetCamera.transform.position += Vector3.right * 2;
            p.SendMessage("LateUpdate");
            report.parallaxMoved = p.layers[0].copies[1].transform.position != position;
            report.parallaxCoverage = true;
            foreach (var layer in p.layers)
            {
                float halfView = p.targetCamera.orthographicSize * p.targetCamera.aspect;
                report.parallaxCoverage &= layer.copies[0].bounds.min.x <= p.targetCamera.transform.position.x - halfView
                    && layer.copies[2].bounds.max.x >= p.targetCamera.transform.position.x + halfView;
            }
        }
        finally { p.targetCamera.transform.position = camera; p.SendMessage("LateUpdate"); }
    }

    static void PreparePresentation()
    {
        const string path = "Assets/Resources/WorldPresentation.prefab";
        if (File.Exists(path)) throw new Exception("Presentation prefab already exists; preserve it.");
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) throw new Exception("Installed URP 2D unlit sprite shader unavailable.");
        var material = new Material(shader);
        AssetDatabase.CreateAsset(material, "Assets/Resources/WorldBackgroundUnlit.mat");
        var root = new GameObject("WorldPresentation", typeof(ParallaxBackground), typeof(WorldLighting));
        try
        {
            ParallaxBackground parallax = root.GetComponent<ParallaxBackground>();
            parallax.layers = new ParallaxBackground.Layer[2];
            for (int depth = 0; depth < 2; depth++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imagenes/Nature/Backgrounds/Elements/cloudLayer" + (depth + 1) + ".png");
                if (sprite == null) throw new Exception("Existing cloud layer sprite is unavailable.");
                var layer = new ParallaxBackground.Layer { copies = new SpriteRenderer[3], factor = new Vector2(depth == 0 ? .2f : .5f, 1f), offset = new Vector2(0, depth == 0 ? 2 : -1) };
                for (int i = 0; i < 3; i++)
                {
                    var cloud = new GameObject("Depth " + depth + " repeat " + i, typeof(SpriteRenderer));
                    cloud.transform.SetParent(root.transform, false);
                    float scale = 32f / sprite.bounds.size.x; cloud.transform.localScale = Vector3.one * scale;
                    SpriteRenderer renderer = cloud.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
                    renderer.sharedMaterial = material; renderer.sortingOrder = -20 + depth * 5;
                    renderer.color = new Color(.18f, .3f, .42f, depth == 0 ? .3f : .2f);
                    layer.copies[i] = renderer;
                }
                parallax.layers[depth] = layer;
            }
            WorldLighting lighting = root.GetComponent<WorldLighting>();
            var ambient = new GameObject("Ambient", typeof(Light2D)); ambient.transform.SetParent(root.transform, false);
            lighting.ambient = ambient.GetComponent<Light2D>(); lighting.ambient.lightType = Light2D.LightType.Global;
            lighting.ambient.color = new Color(.8f, .85f, 1f); lighting.ambient.intensity = .3f;
            var local = new GameObject("Player cave illumination", typeof(Light2D)); local.transform.SetParent(root.transform, false);
            lighting.playerLight = local.GetComponent<Light2D>(); lighting.playerLight.lightType = Light2D.LightType.Point;
            lighting.playerLight.pointLightOuterRadius = 4; lighting.playerLight.pointLightInnerRadius = 1;
            lighting.playerLight.intensity = .7f; lighting.playerLight.color = new Color(1f, .9f, .75f);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static int MissingReferences(GameObject root)
    {
        if (root == null) return 1;
        int missing = 0;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
            if (PrefabUtility.GetPrefabInstanceStatus(child.gameObject) == PrefabInstanceStatus.MissingAsset) missing++;
        }
        return missing;
    }
}
