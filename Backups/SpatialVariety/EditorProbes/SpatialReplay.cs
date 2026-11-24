using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class SpatialReplay
{
    const float Dt = .02f;
    [Serializable] public class Result { public string profile, error; public int seed; public int required, collected, completedLinks, failedLink = -1; public float stamina, seconds; public Vector2 position; public bool reachedGoal; }
    [Serializable] public class Report { public List<Result> worlds = new List<Result>(); public string error; }
    static SpatialReplay() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Temp/SpatialVariety/replay.request";
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !File.Exists(request)) return;
        string label;
        try { label = File.ReadAllText(request).Trim(); File.Delete(request); } catch (IOException) { return; }
        if (label == "preview") { PublishPreview(); return; }
        var report = new Report();
        try
        {
            Player source = UnityEngine.Object.FindAnyObjectByType<Player>();
            if (source == null) throw new Exception("No actual scene Player.");
            TraversalCapabilities caps = JsonUtility.FromJson<TraversalCapabilities>(Resources.Load<TextAsset>("TraversalCapabilities").text);
            if(label=="exploration" || label=="preference")
            {
                foreach(int seed in label=="preference"?new[]{0,1,2,4,11,42}:new[]{2,4,11}) report.worlds.Add(Replay(source,caps,TraversalLayout.Random,12,seed));
                File.WriteAllText("Backups/SpatialVariety/physical-collection-replay-"+label+".json",JsonUtility.ToJson(report,true));
                return;
            }
            foreach(TraversalLayout layout in label=="cross"?new[]{TraversalLayout.Cross}:new[]{TraversalLayout.Horizontal,TraversalLayout.Ascent,TraversalLayout.Descent,TraversalLayout.U,TraversalLayout.Switchback,TraversalLayout.Cross,TraversalLayout.Random})
                foreach(int seed in new[]{0,1}) report.worlds.Add(Replay(source,caps,layout,3,seed));
            if(label!="cross") report.worlds.Add(Replay(source,caps,TraversalLayout.Switchback,12,42));
            report.worlds.Add(Replay(source,caps,TraversalLayout.Cross,12,42));
            if(label!="cross") report.worlds.Add(Replay(source,caps,TraversalLayout.Random,12,42));
        }
        catch (Exception e) { report.error = e.ToString(); }
        File.WriteAllText("Backups/SpatialVariety/physical-collection-replay-"+label+".json", JsonUtility.ToJson(report, true));
    }

    [Serializable] public class Preview { public int seed, level, width, height; public string layout, error; }
    static void PublishPreview()
    {
        var result=new Preview();
        try
        {
            Libro book=UnityEngine.Object.FindAnyObjectByType<Libro>();
            if(book==null || book.LastGeneratedWorld==null)throw new Exception("No current runtime world");
            var so=new SerializedObject(book);
            var layout=so.FindProperty("layoutMundo");var fixedMode=so.FindProperty("semillaMundoFija");var seed=so.FindProperty("semillaMundo");
            int oldLayout=layout.intValue,oldSeed=seed.intValue;bool oldFixed=fixedMode.boolValue;
            try { layout.intValue=(int)TraversalLayout.Random;fixedMode.boolValue=true;seed.intValue=2;so.ApplyModifiedPropertiesWithoutUndo();book.Generacion(1); }
            finally { so.Update();layout.intValue=oldLayout;fixedMode.boolValue=oldFixed;seed.intValue=oldSeed;so.ApplyModifiedPropertiesWithoutUndo(); }
            result.seed=book.CurrentSeed;result.level=book.Nivel;result.layout=book.LastGeneratedWorld.Route.Layout.ToString();
            result.width=book.LastGeneratedWorld.Tiles.GetLength(0);result.height=book.LastGeneratedWorld.Tiles.GetLength(1);
            Selection.activeGameObject=book.MapaDeCasillas.gameObject;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(book.MapaDeCasillas.GetComponent<TilemapRenderer>().bounds,true);
            EditorApplication.isPaused=true;
        }
        catch(Exception e){result.error=e.ToString();}
        File.WriteAllText("Backups/SpatialVariety/native-preview.json",JsonUtility.ToJson(result,true));
    }

    static Result Replay(Player source, TraversalCapabilities caps, TraversalLayout layout, int required, int seed)
    {
        var result = new Result { profile = layout.ToString(), required = required, seed = seed };
        Sprite paper = null;
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.CreateScene("Traversal replay " + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        try
        {
            SceneManager.SetActiveScene(scene);
            var settings = new TraversalGenerationSettings { Layout = layout, RequiredFragments = required };
            WorldData world = ProceduralWorld.GenerateTraversal(settings, caps, source.staminaSettings, seed);
            // Replay every returning false path too, rather than certifying only the page branch.
            var itinerary = new List<int>(); var visitedBranches = new HashSet<int>();
            foreach (int index in world.Route.PrimaryLinks)
            {
                TraversalLink main = world.Route.Links[index];
                if (main.Action != TraversalAction.Rest)
                    for (int i = 0; i < world.Route.Links.Length; i++)
                    {
                        TraversalLink candidate = world.Route.Links[i];
                        if (candidate.Primary || candidate.BranchId < 2 || candidate.FromNode != main.FromNode || !visitedBranches.Add(candidate.BranchId)) continue;
                        for (int j = 0; j < world.Route.Links.Length; j++)
                            if (!world.Route.Links[j].Primary && world.Route.Links[j].BranchId == candidate.BranchId) itinerary.Add(j);
                    }
                itinerary.Add(index);
            }
            world.CollectionLinks = itinerary.ToArray();
            var grid = new GameObject("Replay Grid", typeof(Grid));
            var terrain = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tilemap.prefab"));
            terrain.transform.SetParent(grid.transform, false);
            Tilemap map = terrain.GetComponent<Tilemap>();
            WorldTilemapRenderer.Apply(world, map, AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Casillas/SueloAvanzado.asset"));
            var holder = new GameObject("Replay actor holder"); holder.SetActive(false);
            GameObject actor = UnityEngine.Object.Instantiate(source.gameObject, holder.transform);
            Player player = actor.GetComponent<Player>();
            foreach (MonoBehaviour script in actor.GetComponentsInChildren<MonoBehaviour>(true)) if (script != player) UnityEngine.Object.DestroyImmediate(script);
            foreach (Animator animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (AudioSource audio in actor.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            holder.SetActive(true); player.enabled = false;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            var environment = terrain.AddComponent<TraversalEnvironment>();
            WorldTilemapRenderer.ConfigureTraversal(world, map, environment, player);
            foreach (Renderer renderer in grid.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            body.position = WorldTilemapRenderer.StandingPosition(world, map, world.Route.Nodes[0], player.SoleOffset);
            body.linearVelocity = Vector2.zero; player.NotifyWarp();
            var progress = new DiaryProgress(required,1);
            paper = DiaryPage.CreatePaperSprite();
            for(int id=0;id<world.FragmentNodes.Length;id++)
            {
                int node=world.FragmentNodes[id];
                var page=new GameObject("Replay fragment",typeof(SpriteRenderer),typeof(BoxCollider2D),typeof(DiaryPage));
                page.transform.position=WorldTilemapRenderer.StandingPosition(world,map,world.Route.Nodes[node],player.SoleOffset)+Vector3.down*.08f;
                page.transform.localScale=Vector3.one*.35f;
                page.GetComponent<SpriteRenderer>().sprite=paper;
                page.GetComponent<SpriteRenderer>().enabled=false;
                page.GetComponent<BoxCollider2D>().size=paper.bounds.size;
                page.GetComponent<DiaryPage>().Initialize(id,node,1,body,progress.TryCollect);
            }
            var pilot = new Pilot(player, body, scene.GetPhysicsScene2D(), result);
            for (int i = 0; i < 30; i++) pilot.Step(default(TraversalInput));
            foreach (int index in world.CollectionLinks)
            {
                result.failedLink = index;
                TraversalLink link = world.Route.Links[index];
                TraversalNode target = world.Route.Nodes[link.ToNode];
                Vector3 goal = WorldTilemapRenderer.StandingPosition(world, map, target, player.SoleOffset);
                if (link.Action == TraversalAction.Rest)
                    for (int i = 0; i < Mathf.CeilToInt((float)link.Duration / Dt); i++) pilot.Step(default(TraversalInput));
                else if (link.Action == TraversalAction.Walk || link.Action == TraversalAction.Sprint || link.Action == TraversalAction.Crawl)
                    pilot.Move(goal.x, link.Action == TraversalAction.Sprint, link.Action == TraversalAction.Crawl, (float)link.Duration * 1.8f + 2);
                else if (link.Action == TraversalAction.Jump || link.Action == TraversalAction.DoubleJump)
                    pilot.Jump(goal, link.Action == TraversalAction.DoubleJump, (float)caps.DoubleJumpPressSeconds);
                else if (link.Action == TraversalAction.Climb)
                {
                    float shaftX = WorldTilemapRenderer.ModelPosition(map, link.ClimbX, 0).x;
                    pilot.Move(shaftX, false, false, 3);
                    bool up = world.Route.Nodes[link.FromNode].FloorY < target.FloorY;
                    pilot.Climb(goal, up, (float)link.Duration * 1.8f + 2);
                }
                result.completedLinks++;
            }
            TraversalNode end = world.Route.Nodes[world.Route.Links[world.Route.PrimaryLinks[world.Route.PrimaryLinks.Length - 1]].ToNode];
            Vector3 final = WorldTilemapRenderer.StandingPosition(world, map, end, player.SoleOffset);
            result.reachedGoal = Mathf.Abs(body.position.x - final.x) < .08 && Mathf.Abs(body.position.y - final.y) < .12 && player.Grounded;
            result.collected=progress.CollectedCount;
            if(!progress.IsComplete)throw new Exception("Physical itinerary missed fragments: "+progress.CollectedCount+"/"+required);
            if(!result.reachedGoal)throw new Exception("Physical itinerary did not reach Goal");
            result.failedLink = -1; result.stamina = player.Stamina.Current; result.position = body.position;
        }
        catch (Exception e) { result.error = e.Message; }
        finally
        {
            SceneManager.SetActiveScene(previous);
            foreach (GameObject root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            SceneManager.UnloadSceneAsync(scene);
            if(paper!=null){UnityEngine.Object.DestroyImmediate(paper.texture);UnityEngine.Object.DestroyImmediate(paper);}
        }
        return result;
    }

    sealed class Pilot
    {
        readonly Player player; readonly Rigidbody2D body; readonly PhysicsScene2D scene; readonly Result result;
        public Pilot(Player p, Rigidbody2D b, PhysicsScene2D s, Result r) { player = p; body = b; scene = s; result = r; }
        public void Step(TraversalInput input)
        {
            if (result.seconds > 2400) throw new Exception("Bounded physical replay timeout.");
            player.SampleTraversalInput(input); player.SimulateTraversal(Dt); scene.Simulate(Dt); result.seconds += Dt;
            result.position = body.position; result.stamina = player.Stamina.Current;
            if (player.State == TraversalState.Dead) throw new Exception("Physical route entered a lethal void at " + body.position);
        }
        public void Move(float targetX, bool sprint, bool crouch, float timeout)
        {
            for (int i = 0; i < Mathf.CeilToInt(timeout / Dt); i++)
            {
                float delta = targetX - body.position.x;
                if (Mathf.Abs(delta) < .035f) return;
                Step(new TraversalInput { Horizontal = Mathf.Sign(delta), Sprint = sprint, Crouch = crouch });
            }
            throw new Exception("Movement blocked: state=" + player.State + ", targetX=" + targetX + ", position=" + body.position + ", stamina=" + player.Stamina.Current);
        }
        public void Jump(Vector3 target, bool twice, float secondPress)
        {
            float direction = Mathf.Sign(target.x - body.position.x);
            for (int tick = 0; tick < 120; tick++)
            {
                bool jump = tick == 0 || (twice && tick == Mathf.RoundToInt(secondPress / Dt));
                Step(new TraversalInput { Horizontal = direction, Sprint = true, Jump = jump });
                if (tick > 5 && player.Grounded)
                {
                    Move(target.x, false, false, 3);
                    if (Mathf.Abs(body.position.y - target.y) > .15f) throw new Exception("Jump landed on the wrong floor.");
                    return;
                }
                if (player.State == TraversalState.Hanging) throw new Exception("Jump required an unmodeled ledge grip.");
            }
            throw new Exception("Jump did not land.");
        }
        public void Climb(Vector3 target, bool up, float timeout)
        {
            for (int tick = 0; tick < Mathf.CeilToInt(timeout / Dt); tick++)
            {
                Step(new TraversalInput { Vertical = up ? 1 : -1, Horizontal = up ? Mathf.Sign(target.x - body.position.x) : 0 });
                if (player.Grounded && Mathf.Abs(body.position.y - target.y) < .12f)
                { Move(target.x, false, false, 3); return; }
                if (player.Stamina.Current < player.staminaSettings.ClimbDrainPerSecond * Dt && player.State == TraversalState.Airborne)
                    throw new Exception("Climb exhausted before reaching its landing.");
            }
            throw new Exception("Climb failed to dismount: state=" + player.State + ", position=" + body.position + ", target=" + target + ", stamina=" + player.Stamina.Current);
        }
    }
}
