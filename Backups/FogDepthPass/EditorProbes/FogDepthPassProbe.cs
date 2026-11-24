using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class FogDepthPassProbe
{
    const string Output="Backups/FogDepthPass/";
    static FogDepthPassProbe(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string path="Temp/FogDepthPass/request";
        if(EditorApplication.isCompiling||!File.Exists(path))return;
        string action;try{action=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        try
        {
            if(action=="setup")Setup();
            else if(action=="check")Check();
            else if(action=="capture")ScreenCapture.CaptureScreenshot(Output+SceneManager.GetActiveScene().name+".png");
            else if(action=="main") { if(EditorApplication.isPlaying)SceneManager.LoadScene("JustMe");else EditorSceneManager.OpenScene("Assets/Scenes/JustMe.unity"); }
            else if(action=="next") {UnityEngine.Object.FindAnyObjectByType<Libro>().Generacion(2);}
            else if(action=="prototype") {if(!EditorApplication.isPlaying)EditorSceneManager.OpenScene("Assets/Scenes/Prototypes/ParallaxDepthTest.unity");}
        }
        catch(Exception e){File.WriteAllText(Output+"error-"+action+".txt",e.ToString());}
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static void Setup()
    {
        Require(!EditorApplication.isPlaying,"Stop Play first.");
        for(int i=0;i<SceneManager.sceneCount;i++)Require(!SceneManager.GetSceneAt(i).isDirty,"Preserve unsaved scene first.");
        AssetDatabase.Refresh();var shader=Shader.Find("JustMe/Soft Background Fog");Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"Fog shader compilation failed.");
        const string materialPath="Assets/Art/Atmosphere/SoftFog.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/JustMe.unity");var book=UnityEngine.Object.FindAnyObjectByType<Libro>();var terrain=book.MapaDeCasillas.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>();
            const string prefabPath="Assets/Resources/WorldPresentation.prefab";
            var root=PrefabUtility.LoadPrefabContents(prefabPath);
            try{var fog=root.GetComponent<WorldFog>();if(fog==null)fog=root.AddComponent<WorldFog>();fog.materialTemplate=material;fog.sortingLayerID=terrain.sortingLayerID;fog.sortingOrder=terrain.sortingOrder-50;PrefabUtility.SaveAsPrefabAsset(root,prefabPath);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Prototypes/ParallaxDepthTest.unity");
            var demo=UnityEngine.Object.FindAnyObjectByType<ParallaxDepthDemo>();
            for(int i=0;i<demo.layers.Length;i++){var color=demo.layers[i].color;color.a=1;demo.layers[i].color=color;demo.layers[i].sortingOrder=-60+i*20;}
            var existing=UnityEngine.Object.FindAnyObjectByType<WorldFog>();if(existing==null)existing=new GameObject("Prototype layered fog",typeof(WorldFog)).GetComponent<WorldFog>();
            existing.materialTemplate=material;existing.sortingLayerID=demo.layers[0].sortingLayerID;existing.sortingOrder=-50;demo.fog=existing;
            Require(EditorSceneManager.SaveScene(scene),"Prototype scene save failed.");AssetDatabase.SaveAssets();
        }
        finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        File.WriteAllText(Output+"setup.json","{\"wallTintAlpha\":1,\"fogPlanes\":2,\"worldPrefabUpdated\":true,\"renderPipelineChanged\":false}");
    }
    [Serializable]class Report{public string scene,error;public int fogInstances,planes;public bool shaderOK,coverage,noColliders,maskFollowsLight,toggle,opaqueWalls,templatePreserved;public float maskRadius;}
    static void Check()
    {
        var r=new Report{scene=SceneManager.GetActiveScene().name};
        try
        {
            Require(EditorApplication.isPlaying,"Requires Play.");var fogs=UnityEngine.Object.FindObjectsByType<WorldFog>();r.fogInstances=fogs.Length;Require(fogs.Length==1,"Duplicate/missing fog.");
            var fog=fogs[0];var renderers=fog.GetComponentsInChildren<MeshRenderer>();r.planes=renderers.Length;Require(r.planes==2,"Fog not initialized.");
            r.shaderOK=!ShaderUtil.ShaderHasError(fog.materialTemplate.shader)&&fog.materialTemplate.shader.isSupported;
            r.noColliders=fog.GetComponentsInChildren<Collider2D>().Length==0&&fog.GetComponentsInChildren<Collider>().Length==0;
            var cam=Camera.main;r.coverage=renderers.All(x=>x.bounds.size.x>=cam.orthographicSize*2*cam.aspect&&x.bounds.size.y>=cam.orthographicSize*2);
            bool visible=fog.visible;fog.visible=false;fog.Refresh();r.toggle=renderers.All(x=>!x.enabled);fog.visible=visible;fog.Refresh();
            var lighting=fog.GetComponent<WorldLighting>();r.maskFollowsLight=lighting==null;
            if(lighting!=null)
            {
                bool full=lighting.DebugFullVisibility;
                try{lighting.DebugFullVisibility=false;fog.Refresh();var block=new MaterialPropertyBlock();renderers[0].GetPropertyBlock(block);r.maskRadius=block.GetFloat("_MaskRadius");r.maskFollowsLight=Mathf.Abs(r.maskRadius-lighting.visibilityRadius)<.001f;}
                finally{lighting.DebugFullVisibility=full;fog.Refresh();}
            }
            var demo=UnityEngine.Object.FindAnyObjectByType<ParallaxDepthDemo>();r.opaqueWalls=demo==null||demo.layers.All(x=>x.color.a==1);
            r.templatePreserved=Mathf.Approximately(fog.materialTemplate.GetFloat("_Density"),.4f);
            Require(r.shaderOK&&r.noColliders&&r.coverage&&r.toggle&&r.maskFollowsLight&&r.opaqueWalls&&r.templatePreserved,"Fog verification failed.");
        }
        catch(Exception e){r.error=e.ToString();}
        File.WriteAllText(Output+"check-"+r.scene+".json",JsonUtility.ToJson(r,true));
    }
}
