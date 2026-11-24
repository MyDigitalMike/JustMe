using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PlayerAnimationProbe
{
    const string Root = "Assets/Sprites/Personaje/AlterPersonaje/";
    const string Clips = "Assets/Animaciones/Player/Expanded/";
    [Serializable] public class SheetResult { public string name; public int sprites, discardedPixels; public float pixelsPerUnit; }
    [Serializable] public class Report { public string error; public List<SheetResult> sheets = new List<SheetResult>(); public List<string> clips = new List<string>(); }
    static PlayerAnimationProbe() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Temp/PlayerAnimation/request";
        if (EditorApplication.isCompiling || !File.Exists(request)) return;
        string action;
        try { action = File.ReadAllText(request).Trim(); File.Delete(request); }
        catch (IOException) { return; }
        try
        {
            if (action == "capabilities")
            {
                Type cap = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.U2D.Sprites.ISpriteFrameEditCapability")).FirstOrDefault(t => t != null);
                File.WriteAllText("Backups/PlayerAnimation/capabilities.json", "{\"frameEditCapability\":" + (cap != null ? "true" : "false") + ",\"spriteProvider\":true}");
            }
            if (action == "import") Import();
            if (action == "apply") ApplyController();
            if (action == "preview") Preview();
            if (action == "verify") Verify();
            if (action == "timing") NormalizeTiming();
        }
        catch (Exception e) { File.WriteAllText("Backups/PlayerAnimation/editor-error.txt", e.ToString()); }
    }

    static void CheckCapability(ISpriteEditorDataProvider provider)
    {
        var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (capability == null) throw new Exception("Sprite importer does not expose edit capabilities.");
        var flags = capability.GetEditCapability();
        foreach (EEditCapability flag in new[]{ EEditCapability.CreateAndDeleteSprite, EEditCapability.EditPivot, EEditCapability.EditSpriteRect, EEditCapability.EditSpriteName })
            if (!flags.HasCapability(flag)) throw new Exception("Unsupported sprite edit: " + flag);
    }
    static Texture2D Read(string path)
    { var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); if (!texture.LoadImage(File.ReadAllBytes(path))) throw new Exception("Invalid PNG: " + path); return texture; }
    static int Cyan(Color32[] pixels, int width, RectInt r)
    {
        int count = 0;
        for(int y=r.yMin;y<r.yMax;y++) for(int x=r.xMin;x<r.xMax;x++)
        { Color32 c=pixels[y*width+x]; if(c.a>220 && c.r<100 && c.g>140 && c.b>160) count++; }
        return count;
    }
    // Import metadata only: reject isolated low-alpha artifacts from the render mesh.
    // The PNG, original art and every original Sprite GUID remain untouched.
    static List<List<Vector2Int>> Components(Color32[] pixels, int width, RectInt cell, out int discarded)
    {
        int w=cell.width,h=cell.height;var seen=new bool[w*h];var all=new List<List<Vector2Int>>();
        for(int start=0;start<seen.Length;start++)
        {
            if(seen[start])continue;
            int sx=start%w,sy=start/w;seen[start]=true;
            if(pixels[(cell.y+sy)*width+cell.x+sx].a<220)continue;
            var part=new List<Vector2Int>();var q=new Queue<int>();q.Enqueue(start);
            while(q.Count>0)
            {
                int n=q.Dequeue(),x=n%w,y=n/w;part.Add(new Vector2Int(cell.x+x,cell.y+y));
                foreach(var d in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
                {
                    int xx=x+d.x,yy=y+d.y;if(xx<0||yy<0||xx>=w||yy>=h)continue;int ni=yy*w+xx;
                    if(seen[ni])continue;seen[ni]=true;
                    if(pixels[(cell.y+yy)*width+cell.x+xx].a>=220)q.Enqueue(ni);
                }
            }
            all.Add(part);
        }
        if(all.Count==0)throw new Exception("Empty generated sprite cell.");
        int largest=all.Max(p=>p.Count);discarded=all.Where(p=>p.Count<Math.Max(64,largest*.015f)).Sum(p=>p.Count);
        return all.Where(p=>p.Count>=Math.Max(64,largest*.015f)).ToList();
    }
    static Vector2[] Outline(List<Vector2Int> pixels, Rect rect)
    {
        // Row envelopes follow the silhouette, preserving separate substantial parts.
        var rows=pixels.GroupBy(p=>p.y).OrderBy(g=>g.Key).ToArray();var points=new List<Vector2>();
        foreach(var row in rows)points.Add(new Vector2(row.Min(p=>p.x),row.Key));
        foreach(var row in rows.Reverse())points.Add(new Vector2(row.Max(p=>p.x)+1,row.Key+1));
        var simple=new List<Vector2>();
        for(int i=0;i<points.Count;i++)
        {
            Vector2 before=points[(i+points.Count-1)%points.Count],at=points[i],after=points[(i+1)%points.Count];
            if(Mathf.Abs((at.x-before.x)*(after.y-at.y)-(at.y-before.y)*(after.x-at.x))>.01f)simple.Add(at-rect.center);
        }
        return simple.ToArray();
    }
    static Sprite[] Slice(string name,int columns,int rows,Report report)
    {
        File.WriteAllText("Backups/PlayerAnimation/phase.txt","Reading "+name);
        string path=Root+"Expanded/"+name+".png";
        Texture2D image=Read(path),reference=Read(Root+"Idle (1).png");Color32[] px=image.GetPixels32();
        int referenceEyes=Cyan(reference.GetPixels32(),reference.width,new RectInt(0,0,reference.width,reference.height));
        var cells=new List<RectInt>();var eyeAreas=new List<int>();
        for(int row=0;row<rows;row++) for(int column=0;column<columns;column++)
        {
            int x0=Mathf.RoundToInt(column*image.width/(float)columns),x1=Mathf.RoundToInt((column+1)*image.width/(float)columns);
            int y0=Mathf.RoundToInt((rows-row-1)*image.height/(float)rows),y1=Mathf.RoundToInt((rows-row)*image.height/(float)rows);
            var cell=new RectInt(x0,y0,x1-x0,y1-y0);cells.Add(cell);eyeAreas.Add(Cyan(px,image.width,cell));
        }
        float ppu=450*Mathf.Sqrt(eyeAreas.OrderBy(x=>x).ElementAt(eyeAreas.Count/2)/(float)referenceEyes);
        File.WriteAllText("Backups/PlayerAnimation/phase.txt","Preparing importer "+name);
        var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit=ppu;ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.npotScale=TextureImporterNPOTScale.None;
        ti.textureCompression=TextureImporterCompression.Uncompressed;ti.maxTextureSize=2048;ti.filterMode=FilterMode.Bilinear;ti.wrapMode=TextureWrapMode.Clamp;
        var settings=new TextureImporterSettings();ti.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.Tight;settings.spriteGenerateFallbackPhysicsShape=false;ti.SetTextureSettings(settings);ti.SaveAndReimport();
        var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(ti);provider.InitSpriteEditorDataProvider();CheckCapability(provider);
        var outlines=provider.GetDataProvider<ISpriteOutlineDataProvider>();var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if(outlines==null||names==null)throw new Exception("Sprite outline/name provider unavailable.");
        var previous=provider.GetSpriteRects().ToDictionary(r=>r.name);var rects=new List<SpriteRect>();var shapes=new Dictionary<GUID,List<Vector2[]>>();int dropped=0;
        for(int i=0;i<cells.Count;i++)
        {
            int discarded;var parts=Components(px,image.width,cells[i],out discarded);dropped+=discarded;
            var points=parts.SelectMany(p=>p).ToArray();int minX=points.Min(p=>p.x),maxX=points.Max(p=>p.x),minY=points.Min(p=>p.y),maxY=points.Max(p=>p.y);
            Rect bounds=new Rect(minX,minY,maxX-minX+1,maxY-minY+1);string padded=name+"_"+i.ToString("D2");string spriteName=previous.ContainsKey(padded)?padded:name+"_"+i;
            var sprite=new SpriteRect{name=spriteName,spriteID=previous.ContainsKey(spriteName)?previous[spriteName].spriteID:GUID.Generate(),rect=bounds,alignment=SpriteAlignment.Custom,
                pivot=new Vector2(.5f,name=="LowTraversal"?0:Mathf.Clamp01((reference.height/900f)*ppu/bounds.height))};
            rects.Add(sprite);shapes.Add(sprite.spriteID,parts.Select(p=>Outline(p,bounds)).Where(p=>p.Length>=3).ToList());
        }
        File.WriteAllText("Backups/PlayerAnimation/phase.txt","Applying outlines "+name);
        provider.SetSpriteRects(rects.ToArray());names.SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
        foreach(var rect in rects){outlines.SetOutlines(rect.spriteID,shapes[rect.spriteID]);outlines.SetTessellationDetail(rect.spriteID,.1f);}
        provider.Apply();ti.SaveAndReimport();UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(reference);
        Sprite[] sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>int.Parse(s.name.Substring(s.name.LastIndexOf('_')+1))).ToArray();
        if(sprites.Length!=columns*rows||sprites.Any(s=>s.triangles.Length<3))throw new Exception("Invalid sliced sprite mesh: "+name);
        report.sheets.Add(new SheetResult{name=name,sprites=sprites.Length,pixelsPerUnit=ppu,discardedPixels=dropped});return sprites;
    }
    static Sprite[] Originals(string kind,int first,int count)
    { return Enumerable.Range(first,count).Select(i=>AssetDatabase.LoadAssetAtPath<Sprite>(Root+kind+" ("+i+").png")).ToArray(); }
    static Sprite[] Merge(Sprite[] old,Sprite[] extra)
    { return old.Select((s,i)=>new{sprite=s,time=i/(float)old.Length}).Concat(extra.Select((s,i)=>new{sprite=s,time=(i+.5f)/extra.Length})).OrderBy(x=>x.time).Select(x=>x.sprite).ToArray(); }
    static AnimationClip Clip(string name,Sprite[] sprites,float duration,bool loop,Report report)
    {
        if(sprites.Any(s=>s==null))throw new Exception("Missing sprite for "+name);
        string path=Clips+name+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}
        var keys=new ObjectReferenceKeyframe[sprites.Length+1];for(int i=0;i<sprites.Length;i++)keys[i]=new ObjectReferenceKeyframe{time=i*duration/sprites.Length,value=sprites[i]};
        keys[sprites.Length]=new ObjectReferenceKeyframe{time=duration,value=loop?sprites[0]:sprites[sprites.Length-1]};
        AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),keys);clip.frameRate=60;
        var config=AnimationUtility.GetAnimationClipSettings(clip);config.loopTime=loop;config.startTime=0;config.stopTime=duration;AnimationUtility.SetAnimationClipSettings(clip,config);EditorUtility.SetDirty(clip);report.clips.Add(name+":"+sprites.Length);return clip;
    }
    static void Import()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before importing animation assets.");
        File.WriteAllText("Backups/PlayerAnimation/phase.txt","Beginning import");
        Directory.CreateDirectory(Clips);AssetDatabase.Refresh();var report=new Report();
        try
        {
            Sprite[] move=Slice("Locomotion",8,4,report),low=Slice("LowTraversal",4,4,report),vertical=Slice("VerticalTraversal",4,4,report);
            Clip("PlayerIdleExpanded",Merge(Originals("Idle",1,16),move.Take(8).ToArray()),1.4833333f,true,report);
            Clip("PlayerWalkExpanded",Merge(Originals("Walk",1,20),move.Skip(8).Take(8).ToArray()),.5666667f,true,report);
            Clip("PlayerRun",Merge(Originals("Run",1,20),move.Skip(16).Take(8).ToArray()),.5666667f,true,report);
            Clip("PlayerJump",Merge(Originals("Jump",1,15),move.Skip(24).Take(4).ToArray()),.45f,false,report);
            Clip("PlayerFallExpanded",Merge(Originals("Jump",16,15),move.Skip(28).Take(4).ToArray()),.5f,false,report);
            Clip("PlayerCrouchEnter",low.Take(4).ToArray(),.18f,false,report);Clip("PlayerCrouchIdle",low.Skip(4).Take(4).ToArray(),.8f,true,report);
            Clip("PlayerCrawl",low.Skip(8).Take(8).ToArray(),.8f,true,report);Clip("PlayerClimb",vertical.Take(8).ToArray(),.6f,true,report);
            Clip("PlayerHang",vertical.Skip(8).Take(4).ToArray(),1f,true,report);Clip("PlayerPullUp",vertical.Skip(12).Take(4).ToArray(),.5f,false,report);
            AssetDatabase.SaveAssets();
        }
        catch(Exception e){report.error=e.ToString();}
        File.WriteAllText("Backups/PlayerAnimation/import.json",JsonUtility.ToJson(report,true));
    }
    static void ApplyController()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before editing clips/controller.");
        foreach(string pair in new[]{"PlayerIdle|PlayerIdleExpanded","PlayerWalk|PlayerWalkExpanded","PlayerFall|PlayerFallExpanded"})
        {
            string[] p=pair.Split('|');var from=AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips+p[1]+".anim");var to=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animaciones/Player/"+p[0]+".anim");
            if(from==null||to==null)throw new Exception("Missing validated animation clip.");
            var binding=EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite");AnimationUtility.SetObjectReferenceCurve(to,binding,AnimationUtility.GetObjectReferenceCurve(from,binding));
            var settings=AnimationUtility.GetAnimationClipSettings(to);settings.loopTime=AnimationUtility.GetAnimationClipSettings(from).loopTime;AnimationUtility.SetAnimationClipSettings(to,settings);EditorUtility.SetDirty(to);
        }
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animaciones/Player/Player.controller");
        if(controller.parameters.All(p=>p.name!="TraversalPose"))controller.AddParameter("TraversalPose",AnimatorControllerParameterType.Int);
        if(controller.parameters.All(p=>p.name!="ClimbPlayback"))controller.AddParameter("ClimbPlayback",AnimatorControllerParameterType.Float);
        if(controller.layers.Any(l=>l.name=="Traversal Visuals"))throw new Exception("Traversal layer already applied.");
        controller.AddLayer("Traversal Visuals");var layers=controller.layers;var layer=layers[layers.Length-1];layer.defaultWeight=1;
        var sm=layer.stateMachine;var idle=sm.AddState("Base animation");idle.writeDefaultValues=false;sm.defaultState=idle;
        Transition(sm,idle,0);
        string[] names={"PlayerRun","PlayerJump","PlayerCrouchIdle","PlayerCrawl","PlayerClimb","PlayerHang","PlayerPullUp","PlayerCrouchEnter"};
        for(int i=0;i<names.Length;i++)
        {
            var state=sm.AddState(names[i]);state.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips+names[i]+".anim");state.writeDefaultValues=false;
            if(names[i]=="PlayerClimb"){state.speedParameterActive=true;state.speedParameter="ClimbPlayback";}
            Transition(sm,state,i+1);
        }
        controller.layers=layers;EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
        File.WriteAllText("Backups/PlayerAnimation/applied.json","{\"baseStatesPreserved\":true,\"newVisualStates\":8,\"newSprites\":64}");
    }
    static void Transition(AnimatorStateMachine sm,AnimatorState target,int pose)
    { var t=sm.AddAnyStateTransition(target);t.AddCondition(AnimatorConditionMode.Equals,pose,"TraversalPose");t.duration=0;t.hasExitTime=false;t.canTransitionToSelf=false; }
    static void Preview()
    {
        foreach(string name in new[]{"LowTraversal","VerticalTraversal","Locomotion"})
        {
            var sprite=AssetDatabase.LoadAllAssetsAtPath(Root+"Expanded/"+name+".png").OfType<Sprite>().OrderBy(s=>s.name).First();
            Texture2D preview=AssetPreview.GetAssetPreview(sprite);if(preview!=null)File.WriteAllBytes("Backups/PlayerAnimation/preview-"+name+".png",preview.EncodeToPNG());
        }
    }
    static void NormalizeTiming()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before normalizing clip timing.");
        var reference=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"Idle (1).png");
        var idleSprites=AssetDatabase.LoadAllAssetsAtPath(Root+"Expanded/Locomotion.png").OfType<Sprite>().OrderBy(s=>int.Parse(s.name.Substring(s.name.LastIndexOf('_')+1))).Take(8).ToArray();
        float factor=idleSprites.Select(s=>s.bounds.size.y).OrderBy(v=>v).ElementAt(4)/reference.bounds.size.y;
        if(Mathf.Abs(factor-1)>.005f)
            foreach(string name in new[]{"Locomotion","VerticalTraversal"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(Root+"Expanded/"+name+".png");
                var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();CheckCapability(provider);
                var rects=provider.GetSpriteRects();foreach(var rect in rects)rect.pivot=new Vector2(rect.pivot.x,rect.pivot.y*factor);
                provider.SetSpriteRects(rects);provider.Apply();importer.spritePixelsPerUnit*=factor;importer.SaveAndReimport();
            }
        var binding=EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite");
        foreach(string path in Directory.GetFiles(Clips,"*.anim").Concat(new[]{"Assets/Animaciones/Player/PlayerIdle.anim","Assets/Animaciones/Player/PlayerWalk.anim","Assets/Animaciones/Player/PlayerFall.anim"}))
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);var keys=AnimationUtility.GetObjectReferenceCurve(clip,binding);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.startTime=0;settings.stopTime=keys.Max(k=>k.time);AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
        }
        AssetDatabase.SaveAssets();File.WriteAllText("Backups/PlayerAnimation/timing.json","{\"normalized\":true}");
    }
    [Serializable] public class VisualCheck { public int pose; public string sprite; public bool selected, physicsUnchanged, feetAligned; }
    [Serializable] public class VisualReport { public string error; public List<VisualCheck> states=new List<VisualCheck>(); public bool stoppedClimbFreezes, baseLayerRestored; }
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Verify()
    {
        var result=new VisualReport();Scene scene=default(Scene);GameObject holder=null;
        try
        {
            if(!EditorApplication.isPlaying)throw new Exception("Visual verification requires Play Mode.");
            Player source=UnityEngine.Object.FindAnyObjectByType<Player>();if(source==null)throw new Exception("No scene player.");
            scene=SceneManager.CreateScene("Animation verification",new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            holder=new GameObject("Animation verification holder");holder.SetActive(false);SceneManager.MoveGameObjectToScene(holder,scene);
            var actor=UnityEngine.Object.Instantiate(source.gameObject,holder.transform);var player=actor.GetComponent<Player>();
            foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>(true))if(b!=player)UnityEngine.Object.DestroyImmediate(b);
            holder.SetActive(true);player.enabled=false;
            var animator=actor.GetComponent<Animator>();var sprite=actor.GetComponent<SpriteRenderer>();var body=actor.GetComponent<Rigidbody2D>();var box=actor.GetComponent<BoxCollider2D>();
            animator.enabled=false;animator.Rebind();int layer=animator.GetLayerIndex("Traversal Visuals");if(layer<0)throw new Exception("Missing traversal animation layer.");
            MethodInfo animation=typeof(Player).GetMethod("UpdateTraversalAnimation",Private),late=typeof(Player).GetMethod("LateUpdate",Private),posture=typeof(Player).GetMethod("SetCrouched",Private);
            var state=typeof(Player).GetProperty("State");
            string[] names={"PlayerRun","PlayerJump","PlayerCrouchIdle","PlayerCrawl","PlayerClimb","PlayerHang","PlayerPullUp","PlayerCrouchEnter"};
            var states=new[]{TraversalState.Sprinting,TraversalState.Airborne,TraversalState.Crouching,TraversalState.Crawling,TraversalState.Climbing,TraversalState.Hanging,TraversalState.PullingUp,TraversalState.Crouching};
            for(int i=0;i<states.Length;i++)
            {
                bool crouched=i==2||i==3||i==7;posture.Invoke(player,new object[]{crouched});state.SetValue(player,states[i]);player.Grounded=i!=1&&i!=4&&i!=5&&i!=6;
                body.linearVelocity=new Vector2(i==0||i==3?1:0,i==1?1:i==4?player.climbSpeed:0);
                typeof(Player).GetField("animationWasCrouched",Private).SetValue(player,crouched);
                typeof(Player).GetField("crouchAnimationStarted",Private).SetValue(player,Time.time-(i==7?0:1));
                Vector2 size=box.size,offset=box.offset;Vector3 scale=actor.transform.localScale;float gravity=body.gravityScale;var constraints=body.constraints;var type=body.bodyType;
                animator.SetFloat("Speed",Mathf.Abs(body.linearVelocity.x));animator.SetBool("Grounded",player.Grounded);animation.Invoke(player,null);animator.Update(.02f);animator.Update(.02f);late.Invoke(player,null);
                bool aligned=true;if(crouched){var visual=actor.transform.Find("Crouched visual").GetComponent<SpriteRenderer>();aligned=Mathf.Abs(visual.bounds.min.y-(body.position.y+player.SoleOffset))<.025f && visual.bounds.max.y<=box.bounds.max.y+.025f;}
                var check=new VisualCheck{pose=i+1,sprite=sprite.sprite!=null?sprite.sprite.name:"null",selected=animator.GetCurrentAnimatorStateInfo(layer).IsName(names[i]),feetAligned=aligned,
                    physicsUnchanged=box.size==size&&box.offset==offset&&actor.transform.localScale==scale&&body.gravityScale==gravity&&body.constraints==constraints&&body.bodyType==type};
                result.states.Add(check);if(!check.selected||!check.physicsUnchanged||!aligned)throw new Exception("Visual state failed: "+names[i]);
            }
            posture.Invoke(player,new object[]{false});state.SetValue(player,TraversalState.Climbing);body.linearVelocity=Vector2.zero;animation.Invoke(player,null);animator.Update(.02f);animator.Update(.02f);
            float before=animator.GetCurrentAnimatorStateInfo(layer).normalizedTime;animator.Update(.3f);result.stoppedClimbFreezes=Mathf.Abs(animator.GetCurrentAnimatorStateInfo(layer).normalizedTime-before)<.001f;
            state.SetValue(player,TraversalState.Idle);player.Grounded=true;animator.SetBool("Grounded",true);animator.SetFloat("Speed",0);animation.Invoke(player,null);animator.Update(.7f);
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animaciones/Player/PlayerIdle.anim");var keys=AnimationUtility.GetObjectReferenceCurve(idle,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"));
            result.baseLayerRestored=animator.GetLayerWeight(layer)==0&&keys.Any(k=>k.value==sprite.sprite);
            if(!result.stoppedClimbFreezes||!result.baseLayerRestored)throw new Exception("Animation freeze or base-layer restoration failed.");
        }
        catch(Exception e){result.error=e.ToString();}
        finally{if(holder!=null)UnityEngine.Object.DestroyImmediate(holder);if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);}
        File.WriteAllText("Backups/PlayerAnimation/visual-verification.json",JsonUtility.ToJson(result,true));
    }
}
