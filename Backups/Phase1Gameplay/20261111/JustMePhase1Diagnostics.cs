using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
internal static class JustMePhase1Diagnostics
{
    private const string OutputDirectory = "Temp/JustMePhase1";

    [Serializable]
    private sealed class Report
    {
        public List<Measurement> measurements = new List<Measurement>();
        public bool terrainPrefabUsesComposite;
        public bool fixedSeedRepeatsBaseMap;
        public string error;
    }

    [Serializable]
    private sealed class Measurement
    {
        public bool composite;
        public bool gridCollisions;
        public float heightRange;
        public float maximumUpwardSpeed;
        public float horizontalDistance;
        public int contactLossSteps;
        public int diagonalContactSteps;
        public int colliderShapes;
        public float stoppingDistance;
        public float jumpHeight;
        public bool landedAfterJump;
        public float fallDistance;
    }

    static JustMePhase1Diagnostics()
    {
        EditorApplication.update += RunRequestedProbe;
    }

    private static void RunRequestedProbe()
    {
        string request = Path.Combine(OutputDirectory, "run-probe");
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !File.Exists(request))
        {
            return;
        }

        string label = File.ReadAllText(request).Trim();
        File.Delete(request);
        if (label != "baseline" && label != "after")
        {
            return;
        }

        var report = new Report();
        try
        {
            GameObject terrainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tilemap.prefab");
            report.terrainPrefabUsesComposite = terrainPrefab.GetComponent<CompositeCollider2D>() != null
                && terrainPrefab.GetComponent<TilemapCollider2D>().compositeOperation == Collider2D.CompositeOperation.Merge;
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            try
            {
                int[,] first = Metodos.GenerarMapaAleatorio(60, 34, 1f, 0.2f, true);
                int[,] second = Metodos.GenerarMapaAleatorio(60, 34, 1f, 0.2f, true);
                report.fixedSeedRepeatsBaseMap = true;
                for (int x = 0; x < 60; x++)
                {
                    for (int y = 0; y < 34; y++)
                    {
                        report.fixedSeedRepeatsBaseMap &= first[x, y] == second[x, y];
                    }
                }
            }
            finally
            {
                UnityEngine.Random.state = randomState;
            }
            Player player = UnityEngine.Object.FindAnyObjectByType<Player>();
            if (player == null)
            {
                throw new InvalidOperationException("Open the JustMe scene before running the probe.");
            }

            for (int combination = 0; combination < 4; combination++)
            {
                report.measurements.Add(Measure(player, (combination & 1) != 0, (combination & 2) != 0));
            }
        }
        catch (Exception exception)
        {
            report.error = exception.ToString();
        }

        File.WriteAllText(Path.Combine(OutputDirectory, label + ".json"), JsonUtility.ToJson(report, true));
    }

    private static Measurement Measure(Player sourcePlayer, bool merge, bool gridCollisions)
    {
        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = SceneManager.CreateScene("JustMe physics probe " + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        try
        {
            SceneManager.SetActiveScene(scene);
            var gridObject = new GameObject("Probe grid", typeof(Grid));
            SceneManager.MoveGameObjectToScene(gridObject, scene);
            GameObject terrainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tilemap.prefab");
            GameObject terrain = UnityEngine.Object.Instantiate(terrainPrefab);
            SceneManager.MoveGameObjectToScene(terrain, scene);
            terrain.transform.SetParent(gridObject.transform, false);
            Tilemap tilemap = terrain.GetComponent<Tilemap>();
            TilemapCollider2D tileCollider = terrain.GetComponent<TilemapCollider2D>();
            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Casillas/SueloAvanzado.asset");
            tilemap.ClearAllTiles();
            for (int x = -2; x < 25; x++)
            {
                for (int y = -2; y <= 0; y++)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }

            if (gridCollisions)
            {
                for (int x = -2; x < 25; x++)
                {
                    for (int y = -2; y <= 0; y++)
                    {
                        tilemap.SetColliderType(new Vector3Int(x, y, 0), Tile.ColliderType.Grid);
                    }
                }
            }

            CompositeCollider2D composite = null;
            if (merge)
            {
                Rigidbody2D terrainBody = terrain.GetComponent<Rigidbody2D>();
                if (terrainBody == null)
                {
                    terrainBody = terrain.AddComponent<Rigidbody2D>();
                }
                terrainBody.bodyType = RigidbodyType2D.Static;
                composite = terrain.GetComponent<CompositeCollider2D>();
                if (composite == null)
                {
                    composite = terrain.AddComponent<CompositeCollider2D>();
                }
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                composite.generationType = CompositeCollider2D.GenerationType.Manual;
                tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
            else
            {
                tileCollider.compositeOperation = Collider2D.CompositeOperation.None;
                CompositeCollider2D unusedComposite = terrain.GetComponent<CompositeCollider2D>();
                if (unusedComposite != null)
                {
                    unusedComposite.enabled = false;
                }
            }

            tileCollider.ProcessTilemapChanges();
            if (composite != null)
            {
                composite.GenerateGeometry();
            }

            GameObject actor = UnityEngine.Object.Instantiate(sourcePlayer.gameObject);
            foreach (MonoBehaviour behaviour in actor.GetComponentsInChildren<MonoBehaviour>())
            {
                UnityEngine.Object.DestroyImmediate(behaviour);
            }
            foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
            }
            SceneManager.MoveGameObjectToScene(actor, scene);
            Rigidbody2D body = actor.GetComponent<Rigidbody2D>();
            body.position = new Vector2(1.5f, 3f);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            PhysicsScene2D physicsScene = scene.GetPhysicsScene2D();
            const float step = 0.02f;
            for (int i = 0; i < 100; i++)
            {
                physicsScene.Simulate(step);
            }

            float startX = body.position.x;
            float minimumY = body.position.y;
            float maximumY = body.position.y;
            var contacts = new ContactPoint2D[16];
            var result = new Measurement { composite = merge, gridCollisions = gridCollisions };
            for (int i = 0; i < 400; i++)
            {
                body.linearVelocity = new Vector2(sourcePlayer.Speed, body.linearVelocity.y);
                physicsScene.Simulate(step);
                minimumY = Mathf.Min(minimumY, body.position.y);
                maximumY = Mathf.Max(maximumY, body.position.y);
                result.maximumUpwardSpeed = Mathf.Max(result.maximumUpwardSpeed, body.linearVelocity.y);
                int count = body.GetContacts(contacts);
                bool supported = false;
                bool diagonal = false;
                for (int contactIndex = 0; contactIndex < count; contactIndex++)
                {
                    supported |= contacts[contactIndex].normal.y > 0.5f;
                    diagonal |= Mathf.Abs(contacts[contactIndex].normal.x) > 0.05f && contacts[contactIndex].normal.y > 0.05f;
                }
                if (!supported)
                {
                    result.contactLossSteps++;
                }
                if (diagonal)
                {
                    result.diagonalContactSteps++;
                }
            }

            result.heightRange = maximumY - minimumY;
            result.horizontalDistance = body.position.x - startX;
            result.colliderShapes = composite != null ? composite.shapeCount : tileCollider.shapeCount;

            float stopX = body.position.x;
            for (int i = 0; i < 50; i++)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                physicsScene.Simulate(step);
            }
            result.stoppingDistance = Mathf.Abs(body.position.x - stopX);

            float groundHeight = body.position.y;
            body.AddForce(Vector2.up * sourcePlayer.JumpPower, ForceMode2D.Impulse);
            for (int i = 0; i < 100; i++)
            {
                physicsScene.Simulate(step);
                result.jumpHeight = Mathf.Max(result.jumpHeight, body.position.y - groundHeight);
            }
            result.landedAfterJump = body.GetContacts(contacts) > 0 && Mathf.Abs(body.position.y - groundHeight) < 0.02f;

            body.position = new Vector2(30f, groundHeight + 2f);
            body.linearVelocity = Vector2.zero;
            float fallStart = body.position.y;
            for (int i = 0; i < 50; i++)
            {
                physicsScene.Simulate(step);
            }
            result.fallDistance = fallStart - body.position.y;
            return result;
        }
        finally
        {
            SceneManager.SetActiveScene(previousScene);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            SceneManager.UnloadSceneAsync(scene);
        }
    }

    [MenuItem("Tools/JustMe/Phase 1/Apply Terrain Composition")]
    private static void ApplyTerrainComposition()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Exit Play Mode before editing the terrain prefab.");
        }

        const string path = "Assets/Prefabs/Tilemap.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = root.AddComponent<Rigidbody2D>();
            }
            body.bodyType = RigidbodyType2D.Static;
            CompositeCollider2D composite = root.GetComponent<CompositeCollider2D>();
            if (composite == null)
            {
                composite = root.AddComponent<CompositeCollider2D>();
            }
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            root.GetComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success)
            {
                throw new InvalidOperationException("Could not save the terrain prefab.");
            }
            File.WriteAllText(Path.Combine(OutputDirectory, "terrain-applied.json"), "{\"applied\":true}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
