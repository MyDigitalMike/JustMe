using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class WorldParallaxProbe
{
    const string Output="Backups/WorldParallax/";
    static WorldParallaxProbe(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string path="Temp/WorldParallax/request";if(EditorApplication.isCompiling||!File.Exists(path))return;
        string a;try{a=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        try
        {
            if(a=="setup")
            {
                if(EditorApplication.isPlaying)throw new Exception("Stop Play.");
                for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve unsaved scene.");
                AssetDatabase.Refresh();var shader=Shader.Find("JustMe/Parallax Ruins");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Shader compile error.");
                const string matPath="Assets/Art/Atmosphere/ParallaxRuins.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,matPath);}
                string p="Assets/Resources/WorldPresentation.prefab";var root=PrefabUtility.LoadPrefabContents(p);
                try{var c=root.GetComponent<WorldBackdropLayers>();if(c==null)c=root.AddComponent<WorldBackdropLayers>();c.materialTemplate=mat;
                    var sprites=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Prototypes/ParallaxDepth/Layers.png").OfType<Sprite>().ToDictionary(x=>x.name);
                    c.sprites=new[]{sprites["DepthArches"],sprites["DepthRooms"],sprites["DepthNear"]};PrefabUtility.SaveAsPrefabAsset(root,p);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
                AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/JustMe.unity");File.WriteAllText(Output+"setup.json","{\"architectureDepths\":3,\"poolSize\":27,\"fogChanged\":false}");
            }
            else if(a=="check")Check();
            else if(a=="next")UnityEngine.Object.FindAnyObjectByType<Libro>().Generacion(2);
            else if(a=="capture")ScreenCapture.CaptureScreenshot(Output+"gameplay.png");
        }
        catch(Exception e){File.WriteAllText(Output+"error-"+a+".txt",e.ToString());}
    }
    [Serializable]class Report{public string error;public int level,instances,renderers;public bool noColliders,shaderOK,stable,toggle,reusesPool,negativeCoordinates;}
    static void Check()
    {
        var r=new Report();
        try
        {
            if(!EditorApplication.isPlaying)throw new Exception("Requires Play.");var book=UnityEngine.Object.FindAnyObjectByType<Libro>();r.level=book.Nivel;
            var all=UnityEngine.Object.FindObjectsByType<WorldBackdropLayers>();r.instances=all.Length;if(all.Length!=1)throw new Exception("Duplicate background.");
            var layer=all[0];var sprites=layer.GetComponentsInChildren<SpriteRenderer>().Where(x=>x.name.StartsWith("Distant architecture")).ToArray();r.renderers=sprites.Length;
            r.noColliders=sprites.All(x=>x.GetComponent<Collider2D>()==null);r.shaderOK=!ShaderUtil.ShaderHasError(layer.materialTemplate.shader);
            var ids=sprites.ToArray();var positions=sprites.Select(x=>x.transform.position).ToArray();layer.Refresh();r.stable=sprites.Select((x,i)=>(x.transform.position-positions[i]).sqrMagnitude<.00001f).All(x=>x);
            bool visible=layer.visible;layer.visible=false;layer.Refresh();r.toggle=sprites.All(x=>!x.enabled);layer.visible=visible;
            var camera=Camera.main;var original=camera.transform.position;
            try{camera.transform.position=new Vector3(-120,-90,original.z);layer.Refresh();r.negativeCoordinates=sprites.All(x=>!float.IsNaN(x.transform.position.x)&&!float.IsNaN(x.transform.position.y));}
            finally{camera.transform.position=original;layer.Refresh();}
            r.reusesPool=ids.SequenceEqual(layer.GetComponentsInChildren<SpriteRenderer>().Where(x=>x.name.StartsWith("Distant architecture")));
            if(r.instances!=1||r.renderers!=27||!r.noColliders||!r.shaderOK||!r.stable||!r.toggle||!r.reusesPool||!r.negativeCoordinates)throw new Exception("Background check failed.");
        }
        catch(Exception e){r.error=e.ToString();}File.WriteAllText(Output+"check-level"+r.level+".json",JsonUtility.ToJson(r,true));
    }
}
