using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;
using System.Linq;

[InitializeOnLoad]
public static class InputMigrationProbe
{
    static AddRequest install;
    static InputMigrationProbe(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(install!=null&&install.IsCompleted)
        {
            File.WriteAllText("Backups/InputMigration/package-result.txt",install.Status==StatusCode.Success?install.Result.name+"@"+install.Result.version:install.Error.message);install=null;
        }
        const string path="Temp/InputMigration/request";
        if(EditorApplication.isCompiling||!File.Exists(path))return;
        string action;try{action=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        try
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play before migration.");
            if(action=="install")
            {
                for(int i=0;i<SceneManager.sceneCount;i++)
                {var scene=SceneManager.GetSceneAt(i);if(scene.isDirty&&!EditorSceneManager.SaveScene(scene,"Backups/InputMigration/"+scene.name+"-unsaved.unity",true))throw new Exception("Could not preserve unsaved scene.");}
                install=Client.Add("com.unity.inputsystem");
            }
            else if(action=="migrate") Migrate();
            else if(action=="newonly")
            {
                var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
                settings.FindProperty("activeInputHandler").intValue=1;settings.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
                File.WriteAllText("Backups/InputMigration/backend-requested.txt","Input System Package (New), activeInputHandler=1; restart required");
                EditorApplication.OpenProject(Directory.GetCurrentDirectory());
            }
        }
        catch(Exception e){File.WriteAllText("Backups/InputMigration/editor-error.txt",e.ToString());}
    }
    static void Migrate()
    {
        var manager=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);
        var list=manager.FindProperty("m_Axes");var axes=new List<GameInput.Axis>();
        for(int i=0;i<list.arraySize;i++)
        {
            var a=list.GetArrayElementAtIndex(i);
            axes.Add(new GameInput.Axis {name=a.FindPropertyRelative("m_Name").stringValue,
                negative=a.FindPropertyRelative("negativeButton").stringValue,positive=a.FindPropertyRelative("positiveButton").stringValue,
                altNegative=a.FindPropertyRelative("altNegativeButton").stringValue,altPositive=a.FindPropertyRelative("altPositiveButton").stringValue,
                gravity=a.FindPropertyRelative("gravity").floatValue,dead=a.FindPropertyRelative("dead").floatValue,sensitivity=a.FindPropertyRelative("sensitivity").floatValue,
                snap=a.FindPropertyRelative("snap").boolValue,invert=a.FindPropertyRelative("invert").boolValue,
                type=a.FindPropertyRelative("type").intValue,axis=a.FindPropertyRelative("axis").intValue,joyNum=a.FindPropertyRelative("joyNum").intValue});
        }
        File.WriteAllText("Assets/Resources/GameInputBindings.json",JsonUtility.ToJson(new GameInput.Configuration{axes=axes.ToArray()},true));
        AssetDatabase.Refresh();
        const string assetPath="Assets/Resources/GameUIInput.asset";
        var asset=AssetDatabase.LoadAssetAtPath<InputActionAsset>(assetPath);
        if(asset==null)
        {
            using(var defaults=new DefaultInputActions())asset=InputActionAsset.FromJson(defaults.asset.ToJson());
            asset.name="GameUIInput";
            // Preserve existing Submit keyboard bindings (return/keypad enter/space).
            asset.FindAction("UI/Submit",true).AddBinding("<Keyboard>/space");
            AssetDatabase.CreateAsset(asset,assetPath);
        }
        var refs=new Dictionary<string,InputActionReference>();
        foreach(var a in asset.FindActionMap("UI",true).actions)
        {
            var reference=AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<InputActionReference>().FirstOrDefault(r=>r.action==a);
            if(reference==null){reference=InputActionReference.Create(a);reference.name=a.name;AssetDatabase.AddObjectToAsset(reference,asset);}
            refs[a.name]=reference;
        }
        var setup=EditorSceneManager.GetSceneManagerSetup();var results=new List<string>();
        try
        {
            foreach(string name in new[]{"MainMenu","JustMe","GameOver","Creditos","JustMeLevelCreator"})
            {
                string path="Assets/Scenes/"+name+".unity";
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);int count=0;
                foreach(var system in scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<EventSystem>(true)))
                {
                    var old=system.GetComponent<StandaloneInputModule>();
                    var module=system.GetComponent<InputSystemUIInputModule>();
                    if(old!=null)UnityEngine.Object.DestroyImmediate(old);
                    if(module==null)module=system.gameObject.AddComponent<InputSystemUIInputModule>();
                    module.actionsAsset=asset;module.point=refs["Point"];module.leftClick=refs["Click"];module.rightClick=refs["RightClick"];
                    module.middleClick=refs["MiddleClick"];module.scrollWheel=refs["ScrollWheel"];
                    module.move=refs["Navigate"];module.submit=refs["Submit"];module.cancel=refs["Cancel"];
                    EditorUtility.SetDirty(module);count++;
                }
                if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed: "+path);
                results.Add(name+": "+count+" EventSystem modules");
            }
        }
        finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        AssetDatabase.SaveAssets();File.WriteAllLines("Backups/InputMigration/ui-migration.txt",results);
    }
}
