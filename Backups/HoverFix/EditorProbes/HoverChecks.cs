using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class HoverChecks
{
    const float Dt=.02f;
    [Serializable] public class Contact { public string a,b; public Vector2 point,normal; public float separation; }
    [Serializable] public class Sample
    {
        public string state,bodyType,constraints;
        public Vector2 position,velocity;
        public float gravity,stamina,time,bodyLeft,bodyRight,feet,spriteLeft,spriteRight;
        public bool grounded;
        public List<Contact> contacts=new List<Contact>();
    }
    [Serializable] public class Case
    {
        public string name,error; public float gap;
        public bool firstHangRemote, fell, explicitReleaseFell, exhaustedFell, warpCleared, markerHasCollider, invalidLedgeReleased, candidateJumpLanded, anchorHeld, jumpedAndGrabbed;
        public int markerCount,terrainPaths;
        public Sample firstHang, end, released, exhausted;
        public List<Sample> transitions=new List<Sample>();
    }
    [Serializable] public class Report { public string editor,error; public List<Case> cases=new List<Case>(); }
    static HoverChecks(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Temp/HoverFix/request";
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !File.Exists(request))return;
        string command; try{command=File.ReadAllText(request).Trim();File.Delete(request);}catch(IOException){return;}
        var report=new Report{editor=Application.unityVersion};
        try
        {
            var source=UnityEngine.Object.FindAnyObjectByType<Player>();
            if(source==null)throw new Exception("No live Player");
            if(command=="live") report.cases.Add(new Case{name="live",end=Capture(source,0)});
            else if(command=="invalid") foreach(bool mirror in new[]{false,true}) report.cases.Add(InvalidLedge(source,mirror));
            else if(command=="transitions") foreach(bool mirror in new[]{false,true}) report.cases.Add(CloseWalls(source,mirror));
            else
            {
                foreach(float gap in new[]{.35f,.5f,.75f,1f})foreach(bool mirror in new[]{false,true})
                    report.cases.Add(Gap(source,gap,mirror));
                report.cases.Add(Generated(source));
                foreach(bool mirror in new[]{false,true})
                {
                    report.cases.Add(InvalidLedge(source,mirror));
                    report.cases.Add(CloseWalls(source,mirror));
                }
                foreach(bool mirror in new[]{false,true})report.cases.Add(Ledge(source,mirror));
            }
        }catch(Exception e){report.error=e.ToString();}
        Directory.CreateDirectory("Backups/HoverFix");
        File.WriteAllText("Backups/HoverFix/"+command+".json",JsonUtility.ToJson(report,true));
        Debug.Log("Hover diagnostics: "+command+", cases="+report.cases.Count+", harnessError="+!string.IsNullOrEmpty(report.error));
    }
    static Sample Capture(Player p,float time)
    {
        var b=p.GetComponent<Rigidbody2D>();var box=p.GetComponent<BoxCollider2D>();var sprite=p.GetComponent<SpriteRenderer>();
        var r=new Sample{state=p.State.ToString(),grounded=p.Grounded,position=b.position,velocity=b.linearVelocity,gravity=b.gravityScale,
            stamina=p.Stamina.Current,bodyType=b.bodyType.ToString(),constraints=b.constraints.ToString(),time=time,
            bodyLeft=box.bounds.min.x,bodyRight=box.bounds.max.x,feet=b.position.y+p.SoleOffset,
            spriteLeft=sprite.bounds.min.x,spriteRight=sprite.bounds.max.x};
        var contacts=new ContactPoint2D[32];var filter=new ContactFilter2D{useTriggers=false};filter.SetLayerMask(p.collisionLayer);
        int count=b.GetContacts(filter,contacts);
        for(int i=0;i<count;i++)r.contacts.Add(new Contact{a=contacts[i].collider.name,b=contacts[i].otherCollider.name,point=contacts[i].point,normal=contacts[i].normal,separation=contacts[i].separation});
        return r;
    }
    static void Observe(Rig f,Case r,TraversalInput input,int ticks)
    {
        string last="";
        for(int i=0;i<ticks;i++)
        {
            f.Step(input);var state=f.Player.State.ToString();
            if(state!=last){r.transitions.Add(Capture(f.Player,f.Time));last=state;}
            if(r.firstHang==null && f.Player.State==TraversalState.Hanging)
            {
                r.firstHang=Capture(f.Player,f.Time);
                float side=input.Horizontal>0? r.firstHang.bodyRight : r.firstHang.bodyLeft;
                float wall=input.Horizontal>0?r.gap:0;
                r.firstHangRemote=Mathf.Abs(wall-side)>.1f;
            }
            if(f.Body.position.y < -1)r.fell=true;
            if(f.Player.State==TraversalState.Dead)break;
        }
        r.end=Capture(f.Player,f.Time);
    }
    static Case Gap(Player source,float gap,bool mirror)
    {
        var r=new Case{name="walk-off "+(mirror?"left":"right"),gap=gap};
        using(var f=new Rig(source))
        {
            f.Box("Left terrain",new Vector2(-2,-1),new Vector2(4,2));
            f.Box("Right terrain",new Vector2(gap+2,-1),new Vector2(4,2));
            f.Player.transform.localScale=new Vector3(Mathf.Abs(f.Player.transform.localScale.x)*(mirror?-1:1),f.Player.transform.localScale.y,f.Player.transform.localScale.z);
            f.Place(new Vector2(mirror?gap+.65f:-.65f,-f.Player.SoleOffset+.02f));
            for(int i=0;i<30;i++)f.Step(default(TraversalInput));
            Observe(f,r,new TraversalInput{Horizontal=mirror?-1:1},100);
            // The released-input hold distinguishes normal contact from a grip and checks finite exhaustion.
            Observe(f,r,default(TraversalInput),450);
        }
        return r;
    }
    static Case Generated(Player source,bool widen=false)
    {
        var r=new Case{name=widen?"candidate 1u gap (test data only)":"generated first marked gap seed1"};
        using(var f=new Rig(source))
        {
            var caps=JsonUtility.FromJson<TraversalCapabilities>(Resources.Load<TextAsset>("TraversalCapabilities").text);
            var settings=new TraversalGenerationSettings{Profile=WorldProfile.Mixed};
            var world=ProceduralWorld.GenerateTraversal(settings,caps,source.staminaSettings,1);
            if(widen)
            {
                var hazard=world.VoidRegions[0];hazard.MaxX++;
                for(int y=hazard.MinY;y<=hazard.MaxY+1;y++)world.Tiles[hazard.MaxX,y]=0;
            }
            r.gap=(float)((world.VoidRegions[0].MaxX-world.VoidRegions[0].MinX+1)*world.CellSize);
            var grid=f.Object("Grid");grid.AddComponent<Grid>();
            var terrain=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tilemap.prefab"));
            SceneManager.MoveGameObjectToScene(terrain,f.Scene);terrain.transform.SetParent(grid.transform,false);
            var map=terrain.GetComponent<Tilemap>();
            WorldTilemapRenderer.Apply(world,map,AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Casillas/SueloAvanzado.asset"));
            var env=terrain.AddComponent<TraversalEnvironment>();WorldTilemapRenderer.ConfigureTraversal(world,map,env,f.Player);
            foreach(var renderer in terrain.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            foreach(var transform in terrain.GetComponentsInChildren<Transform>())if(transform.name=="Void edge warning")
            {r.markerCount++;r.markerHasCollider|=transform.GetComponent<Collider2D>()!=null;}
            r.terrainPaths=terrain.GetComponent<CompositeCollider2D>().pathCount;
            var jump=Array.Find(world.Route.Links,l=>l.Primary && l.Action==TraversalAction.Jump);
            var start=WorldTilemapRenderer.StandingPosition(world,map,world.Route.Nodes[jump.FromNode],f.Player.SoleOffset);
            f.Place(start-Vector3.right*.4f);for(int i=0;i<30;i++)f.Step(default(TraversalInput));
            Observe(f,r,new TraversalInput{Horizontal=1},160);
            Observe(f,r,default(TraversalInput),450);
            if(widen)
            {
                f.Place(start-Vector3.right*.9f);for(int i=0;i<30;i++)f.Step(default(TraversalInput));
                for(int i=0;i<20;i++)f.Step(new TraversalInput{Horizontal=1,Sprint=true});
                for(int i=0;i<80;i++)
                {
                    f.Step(new TraversalInput{Horizontal=1,Sprint=true,Jump=i==0});
                    if(i>5 && f.Player.Grounded){r.candidateJumpLanded=f.Body.position.x>start.x+r.gap && Mathf.Abs(f.Body.position.y-start.y)<.15f;break;}
                    if(f.Player.State==TraversalState.Dead)break;
                }
            }
        }
        return r;
    }
    static Case Ledge(Player source,bool mirror)
    {
        var r=new Case{name="legitimate ledge "+(mirror?"left":"right")};
        using(var f=new Rig(source))
        {
            f.Box("Ledge",new Vector2(1,.5f),new Vector2(2,1));
            f.Player.transform.localScale=new Vector3(Mathf.Abs(f.Player.transform.localScale.x)*(mirror?-1:1),f.Player.transform.localScale.y,f.Player.transform.localScale.z);
            f.Place(new Vector2(mirror?2.31f:-.31f,.65f));f.Body.linearVelocity=new Vector2(0,-.2f);
            Observe(f,r,new TraversalInput{Horizontal=mirror?-1:1},1);
            f.Step(new TraversalInput{Vertical=-1});for(int i=0;i<10;i++)f.Step(default(TraversalInput));
            r.released=Capture(f.Player,f.Time);r.explicitReleaseFell=f.Body.linearVelocity.y<-.2f && f.Body.gravityScale==f.Player.BaseGravityScale;
            // Recreate the same placement using the public warp transition and retain depleted stamina.
            float before=f.Player.Stamina.Current;f.Place(new Vector2(mirror?2.31f:-.31f,.65f));
            r.warpCleared=f.Player.State==TraversalState.Airborne && f.Body.gravityScale==f.Player.BaseGravityScale && f.Player.Stamina.Current==before;
            f.Body.linearVelocity=new Vector2(0,-.2f);f.Step(new TraversalInput{Horizontal=mirror?-1:1});
            for(int i=0;i<400;i++)f.Step(default(TraversalInput));
            r.exhausted=Capture(f.Player,f.Time);r.exhaustedFell=f.Player.State==TraversalState.Airborne && f.Body.linearVelocity.y<-.2f;
        }
        return r;
    }
    static Case CloseWalls(Player source,bool mirror)
    {
        var r=new Case{name="jump between close walls "+(mirror?"left":"right"),gap=.75f};
        using(var f=new Rig(source))
        {
            f.Box("Floor",new Vector2(.375f,-.5f),new Vector2(5,1));
            f.Box("Left wall",new Vector2(-1,.75f),new Vector2(2,1.5f));
            f.Box("Right wall",new Vector2(1.75f,.75f),new Vector2(2,1.5f));
            f.Player.transform.localScale=new Vector3(Mathf.Abs(f.Player.transform.localScale.x)*(mirror?-1:1),f.Player.transform.localScale.y,f.Player.transform.localScale.z);
            f.Place(new Vector2(.375f,-f.Player.SoleOffset+.02f));
            for(int i=0;i<30;i++)f.Step(default(TraversalInput));
            for(int i=0;i<70;i++)
            {
                f.Step(new TraversalInput{Horizontal=mirror?-1:1,Jump=i==0});
                if(f.Player.State==TraversalState.Hanging){r.firstHang=Capture(f.Player,f.Time);r.jumpedAndGrabbed=true;break;}
            }
            Vector2 anchor=f.Body.position;
            for(int i=0;i<25;i++)f.Step(new TraversalInput{Horizontal=mirror?1:-1});
            r.anchorHeld=f.Player.State==TraversalState.Hanging && Vector2.Distance(anchor,f.Body.position)<.025f;
            f.Step(new TraversalInput{Vertical=-1});for(int i=0;i<8;i++)f.Step(default(TraversalInput));
            r.explicitReleaseFell=f.Player.State==TraversalState.Airborne && f.Body.linearVelocity.y<-.2f && f.Body.gravityScale==f.Player.BaseGravityScale;
            // Respawn while a grip is active, retaining the shared stamina amount.
            f.Place(new Vector2(mirror?.31f:.44f,1.15f));f.Body.linearVelocity=new Vector2(0,-.2f);
            f.Step(new TraversalInput{Horizontal=mirror?-1:1});
            float before=f.Player.Stamina.Current;bool wasHanging=f.Player.State==TraversalState.Hanging;
            f.Place(new Vector2(.375f,1.2f));
            r.warpCleared=wasHanging && f.Player.State==TraversalState.Airborne && f.Body.gravityScale==f.Player.BaseGravityScale && f.Player.Stamina.Current==before;
            f.Step(default(TraversalInput));r.end=Capture(f.Player,f.Time);
        }
        return r;
    }
    static Case InvalidLedge(Player source,bool mirror)
    {
        var r=new Case{name="removed ledge "+(mirror?"left":"right")};
        using(var f=new Rig(source))
        {
            var platform=f.Box("Removed ledge",new Vector2(1,.5f),new Vector2(2,1));
            f.Player.transform.localScale=new Vector3(Mathf.Abs(f.Player.transform.localScale.x)*(mirror?-1:1),f.Player.transform.localScale.y,f.Player.transform.localScale.z);
            f.Place(new Vector2(mirror?2.31f:-.31f,.65f));f.Body.linearVelocity=new Vector2(0,-.2f);
            f.Step(new TraversalInput{Horizontal=mirror?-1:1});r.firstHang=Capture(f.Player,f.Time);
            platform.enabled=false;
            for(int i=0;i<12;i++)f.Step(default(TraversalInput));
            r.end=Capture(f.Player,f.Time);
            r.invalidLedgeReleased=f.Player.State==TraversalState.Airborne && f.Body.gravityScale==f.Player.BaseGravityScale && f.Body.linearVelocity.y<-.2f;
        }
        return r;
    }
    sealed class Rig:IDisposable
    {
        readonly Scene previous;public readonly Scene Scene;readonly PhysicsScene2D physics;
        public readonly Player Player;public readonly Rigidbody2D Body;public float Time;
        public Rig(Player source)
        {
            previous=SceneManager.GetActiveScene();Scene=SceneManager.CreateScene("Hover_"+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            SceneManager.SetActiveScene(Scene);physics=Scene.GetPhysicsScene2D();
            var holder=new GameObject("inactive actor");holder.SetActive(false);
            var actor=UnityEngine.Object.Instantiate(source.gameObject,holder.transform,false);Player=actor.GetComponent<Player>();Player.enabled=false;
            foreach(var script in actor.GetComponentsInChildren<MonoBehaviour>(true))if(script!=Player)UnityEngine.Object.DestroyImmediate(script);
            foreach(var anim in actor.GetComponentsInChildren<Animator>(true))anim.enabled=false;
            foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            foreach(var audio in actor.GetComponentsInChildren<AudioSource>(true))audio.enabled=false;
            holder.SetActive(true);Body=actor.GetComponent<Rigidbody2D>();
            var env=Object("Nonlethal environment").AddComponent<TraversalEnvironment>();Player.ConfigureTraversal(env);
            SceneManager.SetActiveScene(previous);
        }
        public GameObject Object(string name){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,Scene);return go;}
        public BoxCollider2D Box(string name,Vector2 center,Vector2 size){var go=Object(name);go.layer=10;go.transform.position=center;var box=go.AddComponent<BoxCollider2D>();box.size=size;return box;}
        public void Place(Vector2 p){Body.position=p;Body.linearVelocity=Vector2.zero;Player.NotifyWarp();Physics2D.SyncTransforms();}
        public void Step(TraversalInput input){Player.SampleTraversalInput(input);Player.SimulateTraversal(Dt);physics.Simulate(Dt);Time+=Dt;}
        public void Dispose(){SceneManager.SetActiveScene(previous);foreach(var root in Scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(root);SceneManager.UnloadSceneAsync(Scene);}
    }
}
