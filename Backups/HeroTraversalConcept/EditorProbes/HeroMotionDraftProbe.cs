using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

[InitializeOnLoad]
public static class HeroMotionDraftProbe
{
    const string Output="Backups/HeroTraversalConcept/", Art="Assets/Art/Prototypes/HeroMotion/", Clips="Assets/Animaciones/Prototypes/HeroMotion/";
    static readonly string[] Names={"Run","Jump","DoubleJump","Climb"};
    static HeroMotionDraftProbe(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string path="Temp/HeroMotionDraft/request";if(EditorApplication.isCompiling||!File.Exists(path))return;
        string a;try{a=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        try{if(a=="build")Build();else if(a=="check")Check();else if(a=="capture")ScreenCapture.CaptureScreenshot(Output+"preview.png");}
        catch(Exception e){File.WriteAllText(Output+"error-"+a+".txt",e.ToString());}
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Build()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Stop Play first.");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)Require(!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty,"Preserve unsaved scene first.");
        Require(!File.Exists(Clips+"HeroMotionDraft.controller"),"Draft already imported; preserve its GUIDs.");
        string path=Art+"MotionDraft.png";AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=128;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.isReadable=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=2048;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteGenerateFallbackPhysicsShape=false;importer.SetTextureSettings(settings);importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var pixels=texture.GetPixels32();int w=texture.width/6,h=texture.height/4;
        var rects=new List<SpriteRect>();var registration=new List<string>();
        for(int row=0;row<4;row++)for(int col=0;col<6;col++)
        {
            int x0=col*w,y0=(3-row)*h,count=0,opaque=0;double sx=0,sy=0;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                var p=pixels[(x0+x)+(y0+y)*texture.width];if(p.a>180)opaque++;
                if(p.a>200&&p.r<145&&p.g<185&&p.b<185&&p.g>p.r*1.12f&&p.b>p.r*1.05f){sx+=x;sy+=y;count++;}
            }
            Require(opaque>200&&count>40,"Empty or unrecognized frame: "+row+"/"+col);
            // Same pixels-per-unit for every pose. Register the torso, never resize folded poses.
            var pivot=new Vector2((float)(sx/count/w),(float)(sy/count/h));
            string name=Names[row]+"_"+col.ToString("00");
            rects.Add(new SpriteRect{name=name,spriteID=GUID.Generate(),alignment=SpriteAlignment.Custom,pivot=pivot,rect=new Rect(x0,y0,w,h)});
            registration.Add(name+" torsoPivot="+pivot+" opaquePixels="+opaque);
        }
        var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));provider.Apply();
        importer.isReadable=false;importer.SaveAndReimport();
        var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s=>s.name);Require(sprites.Count==24,"Expected 24 sprites.");
        var controller=AnimatorController.CreateAnimatorControllerAtPath(Clips+"HeroMotionDraft.controller");
        for(int row=0;row<4;row++)
        {
            int fps=row==0?12:row==3?10:8;
            var clip=new AnimationClip{name=Names[row],frameRate=fps};var keys=new ObjectReferenceKeyframe[7];
            for(int i=0;i<7;i++)keys[i]=new ObjectReferenceKeyframe{time=(float)i/fps,value=sprites[Names[row]+"_"+(i%6).ToString("00")]};
            AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),keys);
            var clipSettings=AnimationUtility.GetAnimationClipSettings(clip);clipSettings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,clipSettings);
            AssetDatabase.CreateAsset(clip,Clips+Names[row]+".anim");var state=controller.layers[0].stateMachine.AddState(Names[row]);state.motion=clip;if(row==0)controller.layers[0].stateMachine.defaultState=state;
        }
        var shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");Require(shader!=null,"Missing installed sprite shader.");
        var material=new Material(shader);AssetDatabase.CreateAsset(material,Art+"Preview.mat");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Draft camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=1.5f;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.08f,.105f);
        var actor=new GameObject("New character draft",typeof(SpriteRenderer),typeof(Animator),typeof(HeroMotionPreview));
        var renderer=actor.GetComponent<SpriteRenderer>();renderer.sprite=sprites["Run_00"];renderer.sharedMaterial=material;renderer.sortingOrder=5;
        var animator=actor.GetComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
        var preview=actor.GetComponent<HeroMotionPreview>();preview.animator=animator;
        preview.ropeGuide=Guide("Hand contact guide",new Vector3(.4f,-1.5f),new Vector3(.4f,1.5f),material);
        preview.floorGuide=Guide("Registration guide",new Vector3(-4,-.75f),new Vector3(4,-.75f),material);
        preview.ropeGuide.SetActive(false);
        Directory.CreateDirectory("Assets/Scenes/Prototypes");Require(EditorSceneManager.SaveScene(scene,"Assets/Scenes/Prototypes/HeroMotionDraft.unity"),"Preview scene save failed.");
        AssetDatabase.SaveAssets();File.WriteAllLines(Output+"frame-registration.txt",registration);
        File.WriteAllText(Output+"import.json","{\"sprites\":24,\"clips\":4,\"physicsAdded\":false,\"productionAnimatorReplaced\":false,\"uniformPixelsPerUnit\":128}");
    }
    static GameObject Guide(string name,Vector3 a,Vector3 b,Material material)
    {
        var obj=new GameObject(name,typeof(LineRenderer));var line=obj.GetComponent<LineRenderer>();line.sharedMaterial=material;line.startColor=line.endColor=new Color(.4f,.46f,.46f,.65f);line.startWidth=line.endWidth=.012f;line.positionCount=2;line.SetPositions(new[]{a,b});line.sortingOrder=0;return obj;
    }
    [Serializable]class Report{public string error;public int sprites,clips;public bool distinctActions,onlySpriteCurves,noPhysics,noRootMotion,sampledAllStates;}
    static void Check()
    {
        var r=new Report();
        try
        {
            Require(EditorApplication.isPlaying,"Requires Play.");var preview=UnityEngine.Object.FindAnyObjectByType<HeroMotionPreview>();Require(preview!=null,"Open draft scene.");
            r.sprites=AssetDatabase.LoadAllAssetsAtPath(Art+"MotionDraft.png").OfType<Sprite>().Count();r.clips=4;r.onlySpriteCurves=true;r.distinctActions=true;
            var actionSprites=new HashSet<Sprite>();
            for(int a=0;a<4;a++)
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips+Names[a]+".anim");var bindings=AnimationUtility.GetObjectReferenceCurveBindings(clip);
                r.onlySpriteCurves&=bindings.Length==1&&bindings[0].propertyName=="m_Sprite"&&AnimationUtility.GetCurveBindings(clip).Length==0;
                var frames=AnimationUtility.GetObjectReferenceCurve(clip,bindings[0]).Select(k=>(Sprite)k.value).Distinct().ToArray();Require(frames.Length==6,"Invalid clip frames.");foreach(var sprite in frames)r.distinctActions&=actionSprites.Add(sprite);
                preview.Select(a);preview.animator.Update(.15f);Require(preview.GetComponent<SpriteRenderer>().sprite!=null,"Missing live animation sprite.");
            }
            r.sampledAllStates=true;r.noRootMotion=!preview.animator.applyRootMotion;r.noPhysics=UnityEngine.Object.FindObjectsByType<Collider2D>().Length==0&&UnityEngine.Object.FindObjectsByType<Rigidbody2D>().Length==0;
            Require(r.sprites==24&&r.distinctActions&&r.onlySpriteCurves&&r.noRootMotion&&r.noPhysics,"Draft checks failed.");preview.Select(0);
        }
        catch(Exception e){r.error=e.ToString();}File.WriteAllText(Output+"checks.json",JsonUtility.ToJson(r,true));
    }
}
