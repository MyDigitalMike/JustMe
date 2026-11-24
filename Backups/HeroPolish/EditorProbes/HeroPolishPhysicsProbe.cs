using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

// Temporary native import/verification probe; archived after verification.
[InitializeOnLoad]
public static class HeroPolishPhysicsProbe
{
    const string Output = "Backups/HeroPolish/Regression/";
    const string Art = "Assets/Art/Character/AnimeHero/";
    const string Clips = "Assets/Animaciones/Player/AnimeHero/";
    static readonly EditorCurveBinding Binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static HeroPolishPhysicsProbe() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string path = "Temp/HeroPolishPhysics/request";
        if (EditorApplication.isCompiling || !File.Exists(path)) return;
        string action; try { action=File.ReadAllText(path).Trim();File.Delete(path); } catch(IOException){return;}
        try { if(action=="baseline") Baseline(); else if(action=="import") Import(); else if(action=="verify") Verify(); else if(action=="show") Show(); else if(action=="capture") ScreenCapture.CaptureScreenshot(Output+"gameplay.png"); }
        catch(Exception e){File.WriteAllText(Output+"error-"+action+".txt",e.ToString());}
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Baseline()
    {
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animaciones/Player/Player.controller");
        bool doubleState=controller.layers.Any(l=>l.stateMachine.states.Any(s=>s.state.name=="PlayerDoubleJump"));
        bool animePull=AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips+"PullUp.anim")!=null;
        bool props=Resources.Load<Sprite>("HeroTraversal/Props")!=null;
        File.WriteAllText(Output+"baseline-red.json","{\"doubleJumpState\":"+doubleState.ToString().ToLower()+",\"animePullUp\":"+animePull.ToString().ToLower()+",\"replacementProps\":"+props.ToString().ToLower()+"}");
    }
    static Dictionary<string,Sprite> Slice(string path,string[] rows,bool props=false)
    {
        AssetDatabase.ImportAsset(path); var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.isReadable=true;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;importer.maxTextureSize=2048;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteGenerateFallbackPhysicsShape=false;importer.SetTextureSettings(settings);importer.SaveAndReimport();
        var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var pixels=tex.GetPixels32();int cols=props?2:6,nrows=props?2:rows.Length,w=tex.width/cols,h=tex.height/nrows;
        Require(tex.width%cols==0&&tex.height%nrows==0,"Atlas grid mismatch: "+path);
        float ppu=props?200:220f*w/256f;importer.spritePixelsPerUnit=ppu;
        var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var previous=provider.GetSpriteRects().ToDictionary(r=>r.name);var rects=new List<SpriteRect>();
        var details=new List<string>();
        for(int row=0;row<nrows;row++)for(int col=0;col<cols;col++)
        {
            string name=props?rows[row*cols+col]:rows[row]+"_"+col.ToString("00");int x0=col*w,y0=(nrows-1-row)*h;
            int minX=w,minY=h,maxX=0,maxY=0,count=0;double sx=0,sy=0;int skinTop=-1,handX=0,handCount=0;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                Color32 p=pixels[x0+x+(y0+y)*tex.width];if(p.a<80)continue;
                minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
                if(p.a>200&&p.r<145&&p.g<185&&p.b<185&&p.g>p.r*1.12f&&p.b>p.r*1.05f){sx+=x;sy+=y;count++;}
                if(x>w*.61f&&p.a>200&&p.r>p.g*1.06f&&p.r<p.g*1.8f&&p.g>130&&p.b>65&&p.b<p.g)
                {if(y>skinTop){skinTop=y;handX=x;handCount=1;}else if(y==skinTop){handX+=x;handCount++;}}
            }
            Require(maxX>minX&&maxY>minY,"Empty cell "+name);
            minX=Math.Max(0,minX-1);minY=Math.Max(0,minY-1);maxX=Math.Min(w-1,maxX+1);maxY=Math.Min(h-1,maxY+1);
            Vector2 anchor=new Vector2((minX+maxX)*.5f,(minY+maxY)*.5f);
            if(!props)
            {
                Require(count>30,"No torso in "+name);anchor.x=(float)(sx/count);
                anchor.y=minY+.48f*ppu;
                if(rows[row]=="Jump"||rows[row]=="DoubleJump")anchor.y=(float)(sy/count);
                if(rows[row]=="Hang"||rows[row]=="Climb")
                {Require(handCount>0,"No hand in "+name);anchor=new Vector2((float)handX/handCount,skinTop-3);}
                if(rows[row]=="PullUp")
                {float[] heights={.9f,.86f,.84f,.48f,.32f,.27f};anchor=new Vector2(w*(col<3?.73f:col<5?.77f:.77f),h*heights[col]);}
            }
            var rect=new Rect(x0+minX,y0+minY,maxX-minX+1,maxY-minY+1);
            var pivot=new Vector2((anchor.x-minX)/rect.width,(anchor.y-minY)/rect.height);
            rects.Add(new SpriteRect{name=name,spriteID=previous.ContainsKey(name)?previous[name].spriteID:GUID.Generate(),rect=rect,pivot=pivot,alignment=SpriteAlignment.Custom});
            details.Add(name+" ppu="+ppu+" rect="+rect+" pivot="+pivot);
        }
        provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));provider.Apply();importer.isReadable=false;importer.SaveAndReimport();
        File.WriteAllLines(Output+Path.GetFileNameWithoutExtension(path)+"-registration.txt",details);
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s=>s.name);
    }
    static AnimationClip Clip(string name,Sprite[] frames,float duration,bool loop)
    {
        string path=Clips+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(clip==null){clip=new AnimationClip{name=name};AssetDatabase.CreateAsset(clip,path);}
        clip.frameRate=frames.Length/duration;var keys=new ObjectReferenceKeyframe[frames.Length+1];
        for(int i=0;i<frames.Length;i++)keys[i]=new ObjectReferenceKeyframe{time=i*duration/frames.Length,value=frames[i]};
        keys[frames.Length]=new ObjectReferenceKeyframe{time=duration,value=loop?frames[0]:frames.Last()};
        AnimationUtility.SetObjectReferenceCurve(clip,Binding,keys);var setting=AnimationUtility.GetAnimationClipSettings(clip);setting.loopTime=loop;setting.startTime=0;setting.stopTime=duration;AnimationUtility.SetAnimationClipSettings(clip,setting);EditorUtility.SetDirty(clip);return clip;
    }
    static void Import()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Stop Play first.");
        for(int i=0;i<SceneManager.sceneCount;i++)Require(!SceneManager.GetSceneAt(i).isDirty,"Unsaved scene; preserve before import.");
        var motion=Slice(Art+"Motion.png",new[]{"Run","Jump","DoubleJump","Climb"});
        var extra=Slice(Art+"Actions.png",new[]{"Idle","Walk","CrouchEnter","Crawl","Hang","PullUp"});
        var props=Slice("Assets/Resources/HeroTraversal/Props.png",new[]{"Rope","Hook","Cornice","CorniceAlt"},true);
        Func<Dictionary<string,Sprite>,string,Sprite[]> seq=(dict,name)=>Enumerable.Range(0,6).Select(i=>dict[name+"_"+i.ToString("00")]).ToArray();
        var clips=new Dictionary<string,AnimationClip>{
            {"PlayerIdel",Clip("Idle",seq(extra,"Idle"),1,true)},
            {"PlayerWalk",Clip("Walk",seq(extra,"Walk"),.6f,true)},
            {"PlayerFall",Clip("Fall",new[]{motion["Jump_04"],motion["Jump_05"]},.35f,false)},
            {"PlayerRun",Clip("Run",seq(motion,"Run"),.375f,true)},
            {"PlayerJump",Clip("Jump",new[]{motion["Jump_01"],motion["Jump_02"],motion["Jump_03"],motion["Jump_04"]},.32f,false)},
            {"PlayerDoubleJump",Clip("DoubleJump",seq(motion,"DoubleJump"),.375f,false)},
            {"PlayerCrouchEnter",Clip("CrouchEnter",seq(extra,"CrouchEnter"),.18f,false)},
            {"PlayerCrouchIdle",Clip("CrouchIdle",new[]{extra["CrouchEnter_05"]},1,true)},
            {"PlayerCrawl",Clip("Crawl",new[]{extra["Crawl_02"],extra["Crawl_03"],extra["Crawl_04"],extra["Crawl_05"],extra["Crawl_04"],extra["Crawl_03"]},.6f,true)},
            {"PlayerClimb",Clip("Climb",seq(motion,"Climb"),.5f,true)},
            {"PlayerHang",Clip("Hang",seq(extra,"Hang"),1,true)},
            {"PlayerPullUp",Clip("PullUp",seq(extra,"PullUp"),1,false)}
        };
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animaciones/Player/Player.controller");
        if(!controller.parameters.Any(p=>p.name=="PullUpProgress"))controller.AddParameter("PullUpProgress",AnimatorControllerParameterType.Float);
        foreach(var layer in controller.layers)foreach(var item in layer.stateMachine.states)
        {
            var state=item.state;if(clips.ContainsKey(state.name))state.motion=clips[state.name];
            if(state.name=="PlayerPullUp"){state.timeParameter="PullUpProgress";state.timeParameterActive=true;}
        }
        var traversal=controller.layers.First(l=>l.name=="Traversal Visuals").stateMachine;
        var doubleState=traversal.states.FirstOrDefault(s=>s.state.name=="PlayerDoubleJump").state;
        if(doubleState==null)
        {
            doubleState=traversal.AddState("PlayerDoubleJump",new Vector3(500,600));doubleState.writeDefaultValues=true;
            var transition=traversal.AddAnyStateTransition(doubleState);transition.hasExitTime=false;transition.duration=0;transition.canTransitionToSelf=false;transition.AddCondition(AnimatorConditionMode.Equals,9,"TraversalPose");
        }
        doubleState.motion=clips["PlayerDoubleJump"];EditorUtility.SetDirty(controller);
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Character/Player.prefab");
        try
        {
            if(prefab.GetComponent<HeroVisualAlignment>()==null)prefab.AddComponent<HeroVisualAlignment>();
            prefab.GetComponent<SpriteRenderer>().sprite=extra["Idle_00"];
            PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/Character/Player.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        AssetDatabase.SaveAssets();
        File.WriteAllText(Output+"import.json","{\"sprites\":"+(motion.Count+extra.Count)+",\"clips\":"+clips.Count+",\"props\":"+props.Count+",\"rootMotion\":false,\"physicsChanged\":false}");
        EditorSceneManager.OpenScene("Assets/Scenes/JustMe.unity",OpenSceneMode.Single);
    }
    [Serializable] class Result
    {
        public string error; public int poses, physicsCases; public bool allAnime, spriteOnly, soleAligned, naturalScale, doubleJumpAccepted, doubleJumpDenied, pullUpBothSides, releaseRestoresGravity, stoppedClimbFreezes, decorativeOnly;
        public float maximumClimbAnchorStep;
        public List<string> detail=new List<string>();
    }
    sealed class Fixture : IDisposable
    {
        public Scene scene; public PhysicsScene2D physics; public Player player; public Rigidbody2D body;
        public Animator animator; public SpriteRenderer sprite; public HeroVisualAlignment art; public TraversalEnvironment environment;
        GameObject staging;
        public Fixture(Player source)
        {
            scene=SceneManager.CreateScene("Hero verification "+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics2D));physics=scene.GetPhysicsScene2D();
            staging=new GameObject("Inactive staging");staging.SetActive(false);SceneManager.MoveGameObjectToScene(staging,scene);
            var actor=UnityEngine.Object.Instantiate(source.gameObject,staging.transform,false);actor.transform.position=new Vector3(0,3);
            player=actor.GetComponent<Player>();player.enabled=false;art=actor.GetComponent<HeroVisualAlignment>();
            foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>(true))if(b!=player&&b!=art)UnityEngine.Object.DestroyImmediate(b);
            var existing=actor.transform.Find("Hero artwork");if(existing!=null)UnityEngine.Object.DestroyImmediate(existing.gameObject);
            body=actor.GetComponent<Rigidbody2D>();body.linearVelocity=Vector2.zero;
            animator=actor.GetComponent<Animator>();sprite=actor.GetComponent<SpriteRenderer>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var env=new GameObject("Nonlethal fixture environment");SceneManager.MoveGameObjectToScene(env,scene);environment=env.AddComponent<TraversalEnvironment>();
            staging.SetActive(true);player.ConfigureTraversal(environment);animator.Rebind();
        }
        public BoxCollider2D Box(Vector2 position,Vector2 size)
        {
            var obj=new GameObject("Solid fixture");SceneManager.MoveGameObjectToScene(obj,scene);obj.layer=10;obj.transform.position=position;var box=obj.AddComponent<BoxCollider2D>();box.size=size;return box;
        }
        public void Animate(float dt=.02f)
        {
            animator.SetFloat("Speed",Mathf.Abs(body.linearVelocity.x));animator.SetBool("Grounded",player.Grounded);
            typeof(Player).GetMethod("UpdateTraversalAnimation",Private).Invoke(player,null);animator.Update(dt);animator.Update(0);art.Apply(player,sprite,animator,environment,dt);
        }
        public void Step(TraversalInput input)
        {player.SampleTraversalInput(input);player.SimulateTraversal(.02f);Require(physics.Simulate(.02f),"Physics fixture failed");Animate();}
        public void Settle()
        {Box(new Vector2(0,-.5f),new Vector2(200,1));body.position=new Vector2(0,-player.SoleOffset+.035f);body.linearVelocity=Vector2.zero;for(int i=0;i<50;i++)Step(default);Require(player.Grounded,"Fixture did not settle");}
        public void Dispose(){if(staging!=null)UnityEngine.Object.DestroyImmediate(staging);if(environment!=null)UnityEngine.Object.DestroyImmediate(environment.gameObject);if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);}
    }
    static void Verify()
    {
        var r=new Result{allAnime=true,spriteOnly=true,soleAligned=true,naturalScale=true};
        try
        {
            Require(EditorApplication.isPlaying,"Requires Play Mode");var source=UnityEngine.Object.FindAnyObjectByType<Player>();Require(source!=null,"No gameplay player");
            foreach(string path in AssetDatabase.FindAssets("t:AnimationClip",new[]{Clips}).Select(AssetDatabase.GUIDToAssetPath))
            {var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);r.spriteOnly&=AnimationUtility.GetCurveBindings(clip).Length==0;foreach(var k in AnimationUtility.GetObjectReferenceCurve(clip,Binding))r.allAnime&=k.value!=null&&AssetDatabase.GetAssetPath(k.value).StartsWith(Art);}
            using(var f=new Fixture(source))
            {
                var state=typeof(Player).GetProperty("State");var posture=typeof(Player).GetMethod("SetCrouched",Private);var box=f.player.GetComponent<BoxCollider2D>();var foot=f.player.GetComponentInChildren<CircleCollider2D>();
                var states=new[]{TraversalState.Idle,TraversalState.Walking,TraversalState.Sprinting,TraversalState.Airborne,TraversalState.Airborne,TraversalState.Crouching,TraversalState.Crawling,TraversalState.Climbing,TraversalState.Hanging,TraversalState.PullingUp};
                f.environment.climbableAreas=new[]{new Bounds(new Vector3(0,3),new Vector3(1,8,2))};
                foreach(int facing in new[]{1,-1})for(int i=0;i<states.Length;i++)
                {
                    f.player.transform.localScale=new Vector3(facing,1,1);posture.Invoke(f.player,new object[]{i==5||i==6});state.SetValue(f.player,states[i]);f.player.Grounded=i<3||i==5||i==6;
                    f.body.linearVelocity=new Vector2(i==1||i==2||i==6?1:0,i==3?1:i==7?f.player.climbSpeed:0);
                    typeof(Player).GetField("animationWasCrouched",Private).SetValue(f.player,i==5||i==6);typeof(Player).GetField("crouchAnimationStarted",Private).SetValue(f.player,Time.time-1);
                    var size=box.size;var offset=box.offset;var footOffset=foot.offset;float radius=foot.radius,gravity=f.body.gravityScale;var constraints=f.body.constraints;var bodyType=f.body.bodyType;var root=f.player.transform.position;
                    for(int frame=0;frame<40;frame++)f.Animate();
                    Require(box.size==size&&box.offset==offset&&foot.offset==footOffset&&foot.radius==radius&&f.body.gravityScale==gravity&&f.body.constraints==constraints&&f.body.bodyType==bodyType&&root==f.player.transform.position,"Visual pose changed physics");
                    r.allAnime&=f.art.Visual!=null&&AssetDatabase.GetAssetPath(f.art.Visual.sprite).StartsWith(Art);
                    r.naturalScale&=f.art.Visual.transform.localScale==Vector3.one&&f.player.transform.localScale==new Vector3(facing,1,1);
                    if(f.player.Grounded)r.soleAligned&=Mathf.Abs(f.art.Visual.bounds.min.y-(f.body.position.y+f.player.SoleOffset))<.015f;
                    r.detail.Add(states[i]+" facing="+facing+" sprite="+f.art.Visual.sprite.name);r.poses++;
                }
                state.SetValue(f.player,TraversalState.Climbing);f.body.linearVelocity=Vector2.zero;f.Animate();f.Animate();var before=f.art.Visual.transform.position;float time=f.animator.GetCurrentAnimatorStateInfo(1).normalizedTime;f.Animate(.3f);
                r.stoppedClimbFreezes=Mathf.Abs(f.animator.GetCurrentAnimatorStateInfo(1).normalizedTime-time)<.001f&&Vector3.Distance(before,f.art.Visual.transform.position)<.001f;
            }
            using(var f=new Fixture(source))
            {
                f.Settle();f.Step(new TraversalInput{Jump=true});Require(f.player.State==TraversalState.Airborne,"First jump failed");
                int cooldown=Mathf.CeilToInt(f.player.jumpCooldown/.02f)+1;for(int i=0;i<cooldown;i++)f.Step(default);
                Require(!f.player.Grounded,"Double jump fixture already landed");float stamina=f.player.Stamina.Current;f.Step(new TraversalInput{Jump=true});f.Animate();
                r.doubleJumpAccepted=f.animator.GetInteger("TraversalPose")==9&&Mathf.Abs(stamina-f.player.Stamina.Current-f.player.staminaSettings.DoubleJumpCost)<.01f;
                for(int i=0;i<cooldown;i++)f.Step(default);stamina=f.player.Stamina.Current;f.Step(new TraversalInput{Jump=true});
                r.doubleJumpDenied=Mathf.Abs(stamina-f.player.Stamina.Current)<.01f;
                r.physicsCases++;
            }
            r.pullUpBothSides=true;r.releaseRestoresGravity=true;
            foreach(int facing in new[]{1,-1})using(var f=new Fixture(source))
            {
                f.Box(new Vector2(1,.5f),new Vector2(2,1));f.player.transform.localScale=new Vector3(facing,1,1);f.body.position=new Vector2(facing==1?-.31f:2.31f,.65f);f.body.linearVelocity=new Vector2(0,-.2f);
                f.Step(new TraversalInput{Horizontal=facing});Require(f.player.State==TraversalState.Hanging,"Did not grab ledge");
                Require(Vector2.Distance(f.art.Visual.transform.position,f.player.GripAnchor)<.001f,"Hanging art anchor drift");
                f.Step(new TraversalInput{Jump=true});Require(f.player.State==TraversalState.PullingUp,"Did not pull up");
                var poses=new HashSet<Sprite>();float previous=0;int count=0;
                while(f.player.State==TraversalState.PullingUp&&count++<150)
                {Require(f.player.PullUpProgress+ .001f>=previous,"Pull up progress moved backwards");previous=f.player.PullUpProgress;poses.Add(f.sprite.sprite);f.Step(default);}
                for(int i=0;i<10;i++)f.Step(default);
                r.pullUpBothSides&=f.player.Grounded&&poses.Count>=5;r.releaseRestoresGravity&=Mathf.Abs(f.body.gravityScale-f.player.BaseGravityScale)<.001f;
                r.detail.Add("PullUp facing="+facing+" distinctFrames="+poses.Count+" landed="+f.player.Grounded);r.physicsCases++;
                f.player.NotifyWarp();Require(f.player.State==TraversalState.Airborne&&f.animator.applyRootMotion==false,"Warp state reset failed");
            }
            using(var f=new Fixture(source))
            {
                f.body.position=new Vector2(0,2);f.environment.climbableAreas=new[]{new Bounds(new Vector3(0,10),new Vector3(.35f,20,2))};
                f.Step(new TraversalInput{Vertical=1});Require(f.player.State==TraversalState.Climbing,"Rope climb failed");
                Vector3 previous=f.art.Visual.transform.position;
                for(int i=0;i<60;i++){f.Step(new TraversalInput{Vertical=1});r.maximumClimbAnchorStep=Mathf.Max(r.maximumClimbAnchorStep,Vector3.Distance(previous,f.art.Visual.transform.position));previous=f.art.Visual.transform.position;}
                Require(r.maximumClimbAnchorStep<.16f,"Rope hand transfer snaps");
                f.Step(default);previous=f.art.Visual.transform.position;for(int i=0;i<10;i++)f.Step(default);Require(Vector3.Distance(previous,f.art.Visual.transform.position)<.001f,"Stopped rope art drifted");
                f.player.Stamina.TrySpend(f.player.Stamina.Current);f.Step(new TraversalInput{Vertical=1});
                Require(f.player.State==TraversalState.Airborne&&f.body.gravityScale==f.player.BaseGravityScale,"Exhaustion did not release rope");r.physicsCases++;
            }
            var live=UnityEngine.Object.FindObjectsByType<TraversalEnvironment>().First(e=>e.gameObject.scene==source.gameObject.scene);
            var dressing=live.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.name=="Braided climbing rope"||s.name=="Weathered rope anchor"||s.name=="Weathered stone edge").ToArray();
            r.decorativeOnly=dressing.All(s=>s.GetComponent<Collider2D>()==null)&&!live.GetComponentsInChildren<SpriteRenderer>().Any(s=>s.name=="Void edge warning");
            r.detail.Add("Live climb regions="+live.climbableAreas.Length+" rope renderers="+live.GetComponentsInChildren<SpriteRenderer>().Count(s=>s.name=="Braided climbing rope"));
            using(var f=new Fixture(source))
            {
                f.environment.climbableAreas=new[]{new Bounds(new Vector3(0,2),new Vector3(.35f,4,2))};var before=f.environment.climbableAreas[0];
                f.environment.DrawDesignatedSurfaces(source.GetComponent<SpriteRenderer>().sharedMaterial,new[]{Vector3.zero,Vector3.right});
                var renderers=f.environment.GetComponentsInChildren<SpriteRenderer>();
                r.decorativeOnly&=renderers.Count(s=>s.name=="Braided climbing rope")==1&&renderers.Count(s=>s.name=="Weathered stone edge")==2&&renderers.All(s=>s.sprite!=null)&&f.environment.GetComponentsInChildren<Collider2D>().Length==0&&f.environment.climbableAreas[0]==before;
                r.detail.Add("Synthetic traversal props="+renderers.Length+" colliderCount="+f.environment.GetComponentsInChildren<Collider2D>().Length);
            }
            Require(r.allAnime&&r.spriteOnly&&r.soleAligned&&r.naturalScale&&r.doubleJumpAccepted&&r.doubleJumpDenied&&r.pullUpBothSides&&r.releaseRestoresGravity&&r.stoppedClimbFreezes ,"Hero integration checks failed");
        }
        catch(Exception e){r.error=e.ToString();}
        File.WriteAllText(Output+"play-verification.json",JsonUtility.ToJson(r,true));
    }
    static Fixture show; static double nextStep, showStarted;
    static void Show()
    {
        Require(EditorApplication.isPlaying,"Requires Play");if(show!=null)show.Dispose();
        show=new Fixture(UnityEngine.Object.FindObjectsByType<Player>().First(p=>p.enabled));
        var platform=show.Box(new Vector2(1,1.5f),new Vector2(2,3));show.body.position=new Vector2(-.31f,.65f);show.body.linearVelocity=Vector2.zero;
        show.environment.climbableAreas=new[]{new Bounds(new Vector3(-.31f,2),new Vector3(.35f,4,2))};
        var material=Resources.Load<Material>("HeroTraversal/Artwork");
        show.environment.ledgeDecorations=new[]{new TraversalEnvironment.LedgeDecoration{Corner=new Vector3(0,3),Inward=1,Width=.34f},new TraversalEnvironment.LedgeDecoration{Corner=new Vector3(2,3),Inward=-1,Width=.34f}};
        show.environment.DrawDesignatedSurfaces(material,new Vector3[0]);
        foreach(var renderer in show.environment.GetComponentsInChildren<SpriteRenderer>())renderer.gameObject.layer=31;
        var tex=new Texture2D(1,1);tex.SetPixel(0,0,Color.white);tex.Apply();var square=Sprite.Create(tex,new Rect(0,0,1,1),Vector2.one*.5f,1);
        var face=new GameObject("Diagnostic platform",typeof(SpriteRenderer));face.transform.SetParent(platform.transform,false);face.layer=31;face.transform.localScale=new Vector3(2,3,1);
        var sr=face.GetComponent<SpriteRenderer>();sr.sprite=square;sr.sharedMaterial=material;sr.color=new Color(.18f,.22f,.25f);
        var cameraObj=new GameObject("Temporary traversal inspection camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObj,show.scene);cameraObj.transform.position=new Vector3(.3f,1.9f,-10);
        var camera=cameraObj.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=2;camera.cullingMask=1<<31;camera.depth=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.04f,.06f);
        show.Animate();show.art.Visual.gameObject.layer=31;show.art.Visual.sharedMaterial=material;showStarted=nextStep=EditorApplication.timeSinceStartup;EditorApplication.update-=ShowTick;EditorApplication.update+=ShowTick;
    }
    static void ShowTick()
    {
        if(!EditorApplication.isPlaying||show==null){EditorApplication.update-=ShowTick;show=null;return;}
        double now=EditorApplication.timeSinceStartup;if(now<nextStep)return;nextStep=now+.02;
        if(show.player.State==TraversalState.PullingUp&&show.player.PullUpProgress>=.5f)return;
        show.Step(show.player.State==TraversalState.PullingUp||show.player.Grounded?default:new TraversalInput{Vertical=1,Horizontal=1});
        show.art.Visual.gameObject.layer=31;show.art.Visual.sharedMaterial=Resources.Load<Material>("HeroTraversal/Artwork");
    }
}
