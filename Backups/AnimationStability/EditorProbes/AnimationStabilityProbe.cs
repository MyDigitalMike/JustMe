using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Temporary native Editor correction/verification; retired after verification.
[InitializeOnLoad]
public static class AnimationStabilityProbe
{
    const string Output = "Backups/AnimationStability/";
    const string Root = "Assets/Animaciones/Player/";
    const string Art = "Assets/Sprites/Personaje/AlterPersonaje/";
    static readonly EditorCurveBinding Binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static AnimationStabilityProbe() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Temp/AnimationStability/request";
        if (EditorApplication.isCompiling || !File.Exists(request)) return;
        string action;
        try { action = File.ReadAllText(request).Trim(); File.Delete(request); }
        catch (IOException) { return; }
        try
        {
            if (action == "audit") Audit("before-clips.json");
            else if (action == "apply") Apply();
            else if (action == "verify") Verify();
            else if (action == "crouch" || action == "stand")
            {
                if (!EditorApplication.isPlaying) throw new Exception("Requires Play Mode.");
                UnityEngine.Object.FindAnyObjectByType<Player>().SetCrouchHeld(action == "crouch");
            }
            else if (action == "inspect") Inspect();
        }
        catch (Exception e) { File.WriteAllText(Output + "error.txt", e.ToString()); }
    }
    static string[] Paths = {
        Root+"PlayerIdle.anim", Root+"PlayerWalk.anim", Root+"PlayerFall.anim",
        Root+"Expanded/PlayerIdleExpanded.anim", Root+"Expanded/PlayerWalkExpanded.anim",
        Root+"Expanded/PlayerRun.anim", Root+"Expanded/PlayerJump.anim", Root+"Expanded/PlayerFallExpanded.anim"
    };
    static void Inspect()
    {
        var lines = new List<string>();
        foreach(string name in new[]{"PlayerCrouchEnter","PlayerCrouchIdle","PlayerCrawl"})
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"Expanded/"+name+".anim");
            lines.Add(name+": "+string.Join(",",AnimationUtility.GetObjectReferenceCurve(clip,Binding).Select(k=>k.value==null?"NULL":k.value.name)));
        }
        File.WriteAllLines(Output+"native-sprite-bindings.txt",lines);
    }
    [Serializable] public class ClipCheck { public string path; public int poses, mixedKeys; public bool missing; }
    [Serializable] public class ClipReport { public List<ClipCheck> clips = new List<ClipCheck>(); public bool coherent; }
    static ClipReport Audit(string report)
    {
        var result = new ClipReport { coherent = true };
        foreach (string path in Paths)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, Binding);
            var check = new ClipCheck { path = path, poses = keys.Select(k=>k.value).Distinct().Count(),
                missing = keys.Any(k=>k.value == null),
                mixedKeys = keys.Count(k=>AssetDatabase.GetAssetPath(k.value).Contains("/Expanded/Locomotion.png")) };
            result.clips.Add(check); result.coherent &= !check.missing && check.mixedKeys == 0;
        }
        File.WriteAllText(Output + report, JsonUtility.ToJson(result, true)); return result;
    }
    static ObjectReferenceKeyframe[] OriginalKeys(string name)
    {
        // Read the verified original sprite curve; all writes use AnimationUtility.
        string text = File.ReadAllText("Backups/PlayerAnimation/Originals/"+Root+name+".anim");
        string curve = text.Substring(text.IndexOf("  m_PPtrCurves:"));
        curve = curve.Substring(0, curve.IndexOf("    attribute:"));
        return Regex.Matches(curve, @"- time: ([0-9.]+)\s+value: \{fileID: \d+, guid: ([a-f0-9]+), type: 3\}")
            .Cast<Match>().Select(m => new ObjectReferenceKeyframe {
                time = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                value = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(m.Groups[2].Value)) }).ToArray();
    }
    static void Set(string name, ObjectReferenceKeyframe[] keys, float duration, bool loop)
    {
        if (keys.Length == 0 || keys.Any(k=>k.value == null)) throw new Exception("Missing original sprite: "+name);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+name+".anim");
        if (clip == null) throw new Exception("Missing existing clip: "+name);
        AnimationUtility.SetObjectReferenceCurve(clip, Binding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.startTime=0; settings.stopTime=duration; settings.loopTime=loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
    }
    static ObjectReferenceKeyframe[] Sequence(string kind, int start, int count, float duration)
    {
        var keys = Enumerable.Range(0,count).Select(i=>new ObjectReferenceKeyframe {
            time=i*duration/count, value=AssetDatabase.LoadAssetAtPath<Sprite>(Art+kind+" ("+(i+start)+").png") }).ToList();
        keys.Add(new ObjectReferenceKeyframe {time=duration,value=keys.Last().value}); return keys.ToArray();
    }
    static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play before editing clips.");
        var idle = OriginalKeys("PlayerIdle"); var walk = OriginalKeys("PlayerWalk");
        Set("PlayerIdle",idle,1.4833333f,true); Set("Expanded/PlayerIdleExpanded",idle,1.4833333f,true);
        Set("PlayerWalk",walk,.5666667f,true); Set("Expanded/PlayerWalkExpanded",walk,.5666667f,true);
        var run=walk.Select((k,i)=>new ObjectReferenceKeyframe {time=k.time,value=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Run ("+(i+1)+").png")}).ToArray();
        Set("Expanded/PlayerRun",run,.5666667f,true);
        Set("Expanded/PlayerJump",Sequence("Jump",1,15,.45f),.45f,false);
        var fall=Sequence("Jump",16,15,.5f);
        Set("PlayerFall",fall,.5f,false); Set("Expanded/PlayerFallExpanded",fall,.5f,false);
        AssetDatabase.SaveAssets();
        if(!Audit("after-clips.json").coherent) throw new Exception("Mixed locomotion remains.");
    }
    [Serializable] public class PoseCheck {
        public string pose, sprite; public bool selected, physicsUnchanged, feetAligned, naturalScale, correctSprite;
        public float visualHeight, colliderHeight;
    }
    [Serializable] public class VisualReport {
        public string error; public bool inPlayMode, coherent, stoppedClimbFreezes, baseLayerRestored;
        public List<PoseCheck> states=new List<PoseCheck>();
    }
    static void Verify()
    {
        var result=new VisualReport(); Scene scene=default(Scene); GameObject holder=null;
        try
        {
            if(!EditorApplication.isPlaying) throw new Exception("Requires Play Mode.");
            result.inPlayMode=true; result.coherent=Audit("play-clips.json").coherent;
            if(!result.coherent) throw new Exception("Mixed or missing locomotion frames.");
            Player source=UnityEngine.Object.FindAnyObjectByType<Player>();
            if(source==null) throw new Exception("No live player.");
            scene=SceneManager.CreateScene("Animation stability verification",new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            holder=new GameObject("Animation stability holder");holder.SetActive(false);SceneManager.MoveGameObjectToScene(holder,scene);
            var actor=UnityEngine.Object.Instantiate(source.gameObject,holder.transform);var player=actor.GetComponent<Player>();
            foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>(true)) if(b!=player) UnityEngine.Object.DestroyImmediate(b);
            holder.SetActive(true);player.enabled=false;
            var animator=actor.GetComponent<Animator>();var sprite=actor.GetComponent<SpriteRenderer>();
            var body=actor.GetComponent<Rigidbody2D>();var box=actor.GetComponent<BoxCollider2D>();var foot=actor.GetComponentInChildren<CircleCollider2D>();
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();int layer=animator.GetLayerIndex("Traversal Visuals");
            var animation=typeof(Player).GetMethod("UpdateTraversalAnimation",Private);
            var late=typeof(Player).GetMethod("LateUpdate",Private);var posture=typeof(Player).GetMethod("SetCrouched",Private);
            var state=typeof(Player).GetProperty("State");
            string[] names={"PlayerRun","PlayerJump","PlayerCrouchIdle","PlayerCrawl","PlayerClimb","PlayerHang","PlayerPullUp","PlayerCrouchEnter"};
            var states=new[]{TraversalState.Sprinting,TraversalState.Airborne,TraversalState.Crouching,TraversalState.Crawling,TraversalState.Climbing,TraversalState.Hanging,TraversalState.PullingUp,TraversalState.Crouching};
            for(int i=0;i<states.Length;i++)
            {
                bool crouched=i==2||i==3||i==7;posture.Invoke(player,new object[]{crouched});state.SetValue(player,states[i]);player.Grounded=i!=1&&i!=4&&i!=5&&i!=6;
                body.linearVelocity=new Vector2(i==0||i==3?1:0,i==1?1:i==4?player.climbSpeed:0);
                typeof(Player).GetField("animationWasCrouched",Private).SetValue(player,crouched);
                typeof(Player).GetField("crouchAnimationStarted",Private).SetValue(player,Time.time-(i==7?0:1));
                Vector2 size=box.size,offset=box.offset,footOffset=foot.offset;float radius=foot.radius;
                Vector3 scale=actor.transform.localScale;float gravity=body.gravityScale;var constraints=body.constraints;var type=body.bodyType;
                animator.SetFloat("Speed",Mathf.Abs(body.linearVelocity.x));animator.SetBool("Grounded",player.Grounded);animation.Invoke(player,null);animator.Update(.02f);animator.Update(.02f);animator.Update(.02f);late.Invoke(player,null);
                SpriteRenderer visual=crouched?actor.transform.Find("Crouched visual").GetComponent<SpriteRenderer>():sprite;
                var expected=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"Expanded/"+names[i]+".anim");
                var check=new PoseCheck {pose=names[i],sprite=visual.sprite.name,selected=animator.GetCurrentAnimatorStateInfo(layer).IsName(names[i]),
                    correctSprite=AnimationUtility.GetObjectReferenceCurve(expected,Binding).Any(k=>k.value==visual.sprite),
                    naturalScale=!crouched||visual.transform.localScale==Vector3.one,
                    feetAligned=!crouched||Mathf.Abs(visual.bounds.min.y-(body.position.y+player.SoleOffset))<.025f,
                    visualHeight=visual.bounds.size.y,colliderHeight=box.bounds.size.y,
                    physicsUnchanged=box.size==size&&box.offset==offset&&foot.offset==footOffset&&foot.radius==radius&&actor.transform.localScale==scale&&body.gravityScale==gravity&&body.constraints==constraints&&body.bodyType==type};
                result.states.Add(check);
                if(!check.selected||!check.physicsUnchanged||!check.feetAligned||!check.naturalScale||!check.correctSprite) throw new Exception("Pose failed: "+names[i]);
            }
            posture.Invoke(player,new object[]{false});state.SetValue(player,TraversalState.Climbing);body.linearVelocity=Vector2.zero;animation.Invoke(player,null);animator.Update(.02f);animator.Update(.02f);
            float before=animator.GetCurrentAnimatorStateInfo(layer).normalizedTime;animator.Update(.3f);result.stoppedClimbFreezes=Mathf.Abs(animator.GetCurrentAnimatorStateInfo(layer).normalizedTime-before)<.001f;
            state.SetValue(player,TraversalState.Idle);player.Grounded=true;animator.SetBool("Grounded",true);animator.SetFloat("Speed",0);animation.Invoke(player,null);
            // Allow the existing base-layer transition to evaluate over successive frames.
            for(int frame=0;frame<60;frame++){animator.Update(1f/60);late.Invoke(player,null);}
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"PlayerIdle.anim");
            result.baseLayerRestored=animator.GetLayerWeight(layer)==0&&AnimationUtility.GetObjectReferenceCurve(idle,Binding).Any(k=>k.value==sprite.sprite)&&sprite.enabled;
            if(!result.stoppedClimbFreezes||!result.baseLayerRestored) throw new Exception("Freeze/base restoration failed. Sprite="+sprite.sprite.name+", enabled="+sprite.enabled+", layer="+animator.GetLayerWeight(layer));
        }
        catch(Exception e) {result.error=e.ToString();}
        finally {if(holder!=null)UnityEngine.Object.DestroyImmediate(holder);if(scene.IsValid())SceneManager.UnloadSceneAsync(scene);}
        File.WriteAllText(Output+"play-verification.json",JsonUtility.ToJson(result,true));
    }
}
