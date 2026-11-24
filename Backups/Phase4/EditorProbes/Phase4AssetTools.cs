using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Phase4AssetTools
{
    static readonly string[] Targets={"Assets/Prefabs/Character/JumpHard.prefab","Assets/Prefabs/UI/Coin.prefab","Assets/Prefabs/Character/EnemigoSimple.prefab"};
    static readonly string[] ScenePaths={"Assets/Scenes/JustMe.unity","Assets/Scenes/JustMeLevelCreator.unity"};
    const string BookPath="Assets/Prefabs/Character/Libro.prefab";
    const string PresentationPath="Assets/Resources/WorldPresentation.prefab";
    [Serializable] public class Dependency { public string asset;public string[] dependencies; }
    [Serializable] public class SceneRecord
    {
        public string path; public int missingScripts,missingPrefabs,retiredInstances; public bool dirty;
        public List<string> targets=new List<string>(),hud=new List<string>(),audio=new List<string>(),bookReferences=new List<string>();
    }
    [Serializable] public class Report
    {
        public string command,error; public bool complete; public List<Dependency> dependencies=new List<Dependency>();
        public List<SceneRecord> scenes=new List<SceneRecord>();public List<string> retired=new List<string>();
    }
    static Phase4AssetTools(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Temp/Phase4/assets.request";
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(request))return;
        string command;try{command=File.ReadAllText(request).Trim();File.Delete(request);}catch(IOException){return;}
        var report=new Report{command=command};
        try
        {
            if(command=="clean-defaults")
            {
                if(!File.Exists("Backups/Phase4/backup-verification.json"))throw new Exception("Verified backup required");
                const string scriptPath="Assets/Script/Game/Libro.cs";
                string guid=AssetDatabase.AssetPathToGUID(scriptPath);
                var importer=AssetImporter.GetAtPath(scriptPath) as MonoImporter;
                if(importer==null)throw new Exception("Libro MonoImporter missing");
                UnityEngine.Object preserved=importer.GetDefaultReference("generador");
                importer.SetDefaultReferences(new[]{"generador"},new[]{preserved});
                importer.SaveAndReimport();
                if(AssetDatabase.AssetPathToGUID(scriptPath)!=guid)throw new Exception("Script GUID changed");
                report.complete=true;
                File.WriteAllText("Backups/Phase4/assets-clean-defaults.json",JsonUtility.ToJson(report,true));
                return;
            }
            if(command!="audit" && command!="retire" && command!="verify")throw new Exception("Unknown operation");
            if(!File.Exists("Backups/Phase4/backup-verification.json"))throw new Exception("Verified backup required");
            foreach(string path in Targets) if(File.Exists(path))report.dependencies.Add(new Dependency{asset=path,dependencies=AssetDatabase.GetDependencies(path,true)});
            SceneSetup[] setup=EditorSceneManager.GetSceneManagerSetup();
            foreach(SceneSetup item in setup)
                if(item.isLoaded && SceneManager.GetSceneByPath(item.path).isDirty)throw new Exception("Existing unsaved scene: "+item.path);
            if(command=="retire")
            {
                GameObject book=PrefabUtility.LoadPrefabContents(BookPath);
                try{ClearBookReferences(book);PrefabUtility.SaveAsPrefabAsset(book,BookPath);}
                finally{PrefabUtility.UnloadPrefabContents(book);}
                GameObject presentation=PrefabUtility.LoadPrefabContents(PresentationPath);
                try
                {
                    foreach(ParallaxBackground parallax in presentation.GetComponentsInChildren<ParallaxBackground>(true))parallax.enabled=false;
                    foreach(SpriteRenderer sprite in presentation.GetComponentsInChildren<SpriteRenderer>(true))sprite.gameObject.SetActive(false);
                    PrefabUtility.SaveAsPrefabAsset(presentation,PresentationPath);
                }
                finally{PrefabUtility.UnloadPrefabContents(presentation);}
            }
            try
            {
                foreach(string path in ScenePaths)
                {
                    Scene scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid() || !scene.isLoaded;
                    if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                    var record=new SceneRecord{path=path,dirty=scene.isDirty};
                    if(command=="retire")
                    {
                        var removals=new HashSet<GameObject>();
                        foreach(GameObject root in scene.GetRootGameObjects())
                        {
                            ClearBookReferences(root);
                            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                                if(PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && Array.IndexOf(Targets,PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject))>=0)
                                    removals.Add(t.gameObject);
                        }
                        foreach(GameObject removal in removals){UnityEngine.Object.DestroyImmediate(removal);record.retiredInstances++;}
                        if(path==ScenePaths[0])SimplifyPresentation(scene);
                        EditorSceneManager.MarkSceneDirty(scene);
                        if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed: "+path);
                    }
                    Inspect(scene,record);report.scenes.Add(record);
                    if(opened)EditorSceneManager.CloseScene(scene,true);
                }
            }
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
            if(command=="retire")
            {
                foreach(string path in new[]{BookPath,ScenePaths[0],ScenePaths[1]})
                {
                    string text=File.ReadAllText(path);
                    foreach(string target in Targets)
                        if(text.Contains(AssetDatabase.AssetPathToGUID(target)))throw new Exception("Retirement blocked by remaining reference: "+path+" -> "+target);
                }
                foreach(SceneRecord scene in report.scenes)
                    if(scene.missingScripts!=0 || scene.missingPrefabs!=0)throw new Exception("Invalid references after migration: "+scene.path);
                foreach(string path in Targets)
                {
                    if(!AssetDatabase.DeleteAsset(path))throw new Exception("Asset retirement failed: "+path);
                    report.retired.Add(path);
                }
            }
            GameObject finalBook=PrefabUtility.LoadPrefabContents(BookPath);
            try
            {
                foreach(Transform t in finalBook.GetComponentsInChildren<Transform>(true))
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)!=0)throw new Exception("Missing script in preserved Libro prefab");
            }
            finally{PrefabUtility.UnloadPrefabContents(finalBook);}
            report.complete=true;
        }
        catch(Exception e){report.error=e.ToString();}
        File.WriteAllText("Backups/Phase4/assets-"+command+".json",JsonUtility.ToJson(report,true));
        Debug.Log("Phase4 asset operation "+command+": complete="+report.complete+", error="+report.error);
    }
    static void ClearBookReferences(GameObject root)
    {
        foreach(Libro book in root.GetComponentsInChildren<Libro>(true))
        {
            var serialized=new SerializedObject(book);
            foreach(string field in new[]{"CoinElement","Enemigo","JumpForce"})
            {var property=serialized.FindProperty(field);if(property!=null)property.objectReferenceValue=null;}
            var fixedSeed=serialized.FindProperty("semillaMundoFija");if(fixedSeed!=null)fixedSeed.boolValue=false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if(PrefabUtility.IsPartOfPrefabInstance(book))PrefabUtility.RecordPrefabInstancePropertyModifications(book);
        }
    }
    static void SimplifyPresentation(Scene scene)
    {
        foreach(GameObject root in scene.GetRootGameObjects())
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="Canvas GUI")
                {
                    Transform pause=null;
                    foreach(UnityEngine.UI.Button button in t.GetComponentsInChildren<UnityEngine.UI.Button>(true))if(button.name=="ButtonPause")pause=button.transform;
                    if(pause==null)throw new Exception("Verified Pause button missing; HUD migration aborted");
                    foreach(Transform child in t)child.gameObject.SetActive(pause==child || pause.IsChildOf(child));
                }
                var scrolling=t.GetComponent<ScrollingBackground>();
                if(scrolling!=null){scrolling.enabled=false;if(scrolling.bgRend!=null)scrolling.bgRend.enabled=false;}
                if(t.name=="BackgroundSound")
                {var audio=t.GetComponent<AudioSource>();if(audio!=null){audio.Stop();audio.playOnAwake=false;audio.enabled=false;}}
            }
    }
    static void Inspect(Scene scene,SceneRecord record)
    {
        foreach(GameObject root in scene.GetRootGameObjects())
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                record.missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if(PrefabUtility.GetPrefabInstanceStatus(t.gameObject)==PrefabInstanceStatus.MissingAsset)record.missingPrefabs++;
                string prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                if(PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && Array.IndexOf(Targets,prefab)>=0)record.targets.Add(t.name+" -> "+prefab);
                if(t.name=="Canvas GUI")foreach(Transform child in t)record.hud.Add(child.name+" active="+child.gameObject.activeSelf);
                var audio=t.GetComponent<AudioSource>();if(audio!=null)record.audio.Add(t.name+" enabled="+audio.enabled+" playOnAwake="+audio.playOnAwake);
                var book=t.GetComponent<Libro>();
                if(book!=null)
                {
                    var serialized=new SerializedObject(book);
                    foreach(string field in new[]{"Elemento","Jugador","MapaDeCasillas","Casilla","countDownText","Enemys","SoundBookEffect"})
                    {
                        var property=serialized.FindProperty(field);var value=property==null?null:property.objectReferenceValue;
                        record.bookReferences.Add(field+"="+(value==null?"null":value.name)+" asset="+(value!=null && EditorUtility.IsPersistent(value)));
                    }
                }
            }
    }
}
