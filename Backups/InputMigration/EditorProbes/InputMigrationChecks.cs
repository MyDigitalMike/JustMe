using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityStandardAssets.CrossPlatformInput;

[InitializeOnLoad]
public static class InputMigrationChecks
{
    [Serializable]public class Report{public string error;public bool newBackend,legacyBackend;public List<string> checks=new List<string>();}
    static InputMigrationChecks(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string path="Temp/InputMigration/check";
        if(EditorApplication.isCompiling)return;
        if(flow!=null){TickFlow();return;}
        if(!File.Exists(path))return;
        string action;try{action=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        if(action=="test")Test();else if(action=="live")Live();else if(action=="flow")StartFlow();else if(action=="creditescape")StartFlow(100);else if(action=="scenes")Scenes();
    }
    static void Assert(Report report,bool passed,string name){if(!passed)throw new Exception(name);report.checks.Add(name);}
    static void Test()
    {
        var report=new Report();Keyboard keyboard=null;Gamepad pad=null;
        try
        {
            if(!EditorApplication.isPlaying)throw new Exception("Requires Play Mode.");
#if ENABLE_INPUT_SYSTEM
            report.newBackend=true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            report.legacyBackend=true;
#endif
            keyboard=InputSystem.AddDevice<Keyboard>();pad=InputSystem.AddDevice<Gamepad>();
            GameInput.GetAxisRaw("Horizontal");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();
            Assert(report,GameInput.GetAxisRaw("Horizontal")==0,"Neutral input");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A));InputSystem.Update();
            Assert(report,GameInput.GetAxisRaw("Horizontal")==-1,"A horizontal negative");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow,Key.UpArrow));InputSystem.Update();
            Assert(report,GameInput.GetAxisRaw("Horizontal")==1&&GameInput.GetAxisRaw("Vertical")==1,"Arrow axes");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));InputSystem.Update();
            Assert(report,GameInput.GetButtonDown("Jump")&&GameInput.GetButton("Jump"),"Space jump press and hold");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            Assert(report,GameInput.GetButtonUp("Jump")&&!GameInput.GetButton("Jump"),"Jump release");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift,Key.C,Key.Escape,Key.F8));InputSystem.Update();
            Assert(report,GameInput.GetKeyDown(KeyCode.W)&&GameInput.GetAxisRaw("Vertical")==1,"W jump key and vertical axis");
            Assert(report,GameInput.GetKey(KeyCode.LeftShift)&&GameInput.GetKey(KeyCode.C),"Sprint and crouch held");
            Assert(report,GameInput.GetKeyDown(KeyCode.Escape)&&GameInput.GetKeyDown(KeyCode.F8),"Pause and lighting keys");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=new Vector2(.6f,.8f)});InputSystem.Update();
            Assert(report,Mathf.Abs(GameInput.GetAxisRaw("Horizontal")-.6f)<.001f&&Mathf.Abs(GameInput.GetAxisRaw("Vertical")-.8f)<.001f,"Gamepad raw axes preserve scale and inversion");
            InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=new Vector2(.1f,.1f)});InputSystem.Update();
            Assert(report,GameInput.GetAxisRaw("Horizontal")==0&&GameInput.GetAxisRaw("Vertical")==0,"Legacy joystick dead zone 0.19");
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.North));InputSystem.Update();
            Assert(report,GameInput.GetButtonDown("Jump"),"Legacy joystick button 3 jump");
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadRight));InputSystem.Update();
            Assert(report,GameInput.GetAxisRaw("Debug Horizontal")==1,"Debug dpad axis remains bounded");
            float smoothed=0;for(int i=0;i<15;i++)smoothed=GameInput.Smooth(smoothed,1,3,3,true,.02f);
            Assert(report,Mathf.Abs(smoothed-.9f)<.0001f,"Keyboard sensitivity 3 per second");
            for(int i=0;i<15;i++)smoothed=GameInput.Smooth(smoothed,0,3,3,true,.02f);
            Assert(report,smoothed<.0001f,"Keyboard gravity returns to zero");
            Assert(report,Mathf.Abs(GameInput.Smooth(.8f,-1,3,3,true,.02f)+.06f)<.0001f,"Opposite direction snap");
            typeof(GameInput).GetMethod("FocusChanged",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{false});
            Assert(report,GameInput.GetAxisRaw("Debug Horizontal")==0&&!GameInput.GetKey(KeyCode.C),"Focus loss neutralizes input");
            typeof(GameInput).GetMethod("FocusChanged",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{true});
            var module=UnityEngine.Object.FindAnyObjectByType<InputSystemUIInputModule>();
            Assert(report,module!=null&&module.point!=null&&module.submit!=null&&module.actionsAsset!=null,"Live UI has persisted action references");
            Touch(report);
        }
        catch(Exception e){report.error=e.ToString();}
        finally
        {
            if(keyboard!=null){InputSystem.ResetDevice(keyboard);InputSystem.RemoveDevice(keyboard);}
            if(pad!=null){InputSystem.ResetDevice(pad);InputSystem.RemoveDevice(pad);}
            typeof(GameInput).GetMethod("FocusChanged",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{true});
        }
        File.WriteAllText("Backups/InputMigration/native-input-checks.json",JsonUtility.ToJson(report,true));
    }
    static void Touch(Report report)
    {
        var obj=new GameObject("Temporary touchpad",typeof(RectTransform));obj.SetActive(false);
        CrossPlatformInputManager.SwitchActiveInputMethod(CrossPlatformInputManager.ActiveInputMethod.Touch);
        try
        {
            var touch=obj.AddComponent<TouchPad>();touch.horizontalAxisName="MigrationTouchX";touch.verticalAxisName="MigrationTouchY";
            obj.SetActive(true);var system=UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            var pointer=new PointerEventData(system){pointerId=20,position=new Vector2(30,0)};
            touch.OnInitializePotentialDrag(pointer);touch.OnPointerDown(pointer);
            Assert(report,!pointer.useDragThreshold&&CrossPlatformInputManager.GetAxis("MigrationTouchX")>0,"Stationary absolute touch press");
            var other=new PointerEventData(system){pointerId=21,position=new Vector2(-30,0)};touch.OnPointerDown(other);touch.OnPointerUp(other);
            Assert(report,CrossPlatformInputManager.GetAxis("MigrationTouchX")>0,"Second pointer cannot steal touchpad");
            touch.OnPointerUp(pointer);
            Assert(report,CrossPlatformInputManager.GetAxis("MigrationTouchX")==0,"Touch release neutralizes axes");
        }
        finally{UnityEngine.Object.DestroyImmediate(obj);CrossPlatformInputManager.SwitchActiveInputMethod(CrossPlatformInputManager.ActiveInputMethod.Hardware);}
    }
    static void Live()
    {
        var lighting=UnityEngine.Object.FindAnyObjectByType<WorldLighting>();
        var player=UnityEngine.Object.FindAnyObjectByType<Player>();
        var modules=UnityEngine.Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None);
        File.WriteAllText("Backups/InputMigration/live-state.json",JsonUtility.ToJson(new LiveState{
            scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,playing=EditorApplication.isPlaying,
            paused=PauseMenu.EnPausa,fullVisibility=lighting!=null&&lighting.DebugFullVisibility,
            state=player!=null?player.State.ToString():"none",modules=modules.Length,
            legacyModules=UnityEngine.Object.FindObjectsByType<StandaloneInputModule>(FindObjectsSortMode.None).Length},true));
    }
    [Serializable]class LiveState{public string scene,state;public bool playing,paused,fullVisibility;public int modules,legacyModules;}
    static Report flow;static Keyboard flowKeyboard;static int step;static double nextStep;static Player flowPlayer;static float jumpStamina;
    static void Send(params Key[] keys){InputSystem.QueueStateEvent(flowKeyboard,new KeyboardState(keys));}
    static void StartFlow(int initialStep=0)
    {
        flow=new Report();step=initialStep;nextStep=EditorApplication.timeSinceStartup+.2;
#if ENABLE_INPUT_SYSTEM
        flow.newBackend=true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        flow.legacyBackend=true;
#endif
        if(initialStep==100&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Creditos")UnityEngine.SceneManagement.SceneManager.LoadScene("Creditos");
        try{if(!EditorApplication.isPlaying)throw new Exception("Requires Play");flowPlayer=UnityEngine.Object.FindAnyObjectByType<Player>();flowKeyboard=InputSystem.AddDevice<Keyboard>();Send();}
        catch(Exception e){EndFlow(e);}
    }
    static void EndFlow(Exception error=null)
    {
        if(error!=null)flow.error=error.ToString();
        if(flowKeyboard!=null){InputSystem.ResetDevice(flowKeyboard);InputSystem.RemoveDevice(flowKeyboard);flowKeyboard=null;}
        File.WriteAllText("Backups/InputMigration/live-input-flow.json",JsonUtility.ToJson(flow,true));flow=null;
    }
    static void TickFlow()
    {
        if(EditorApplication.timeSinceStartup<nextStep)return;nextStep=EditorApplication.timeSinceStartup+.12;
        try
        {
            switch(step++)
            {
                case 100: Assert(flow,UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Creditos","Starts in Credits");Send(Key.Escape);nextStep+=.4;break;
                case 101: Assert(flow,UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MainMenu","Escape returns Credits to MainMenu");Send();break;
                case 102: EndFlow();break;
                case 0: Assert(flow,!PauseMenu.EnPausa,"Starts resumed");Send(Key.Escape);break;
                case 1: Assert(flow,PauseMenu.EnPausa&&Time.timeScale==0,"Escape pauses live Update");Send();break;
                case 2: Send(Key.F8);break;
                case 3: Assert(flow,!UnityEngine.Object.FindAnyObjectByType<WorldLighting>().DebugFullVisibility,"F8 disables visibility while paused");Send();break;
                case 4: Send(Key.F8);break;
                case 5: Assert(flow,UnityEngine.Object.FindAnyObjectByType<WorldLighting>().DebugFullVisibility,"F8 restores visibility while paused");Send();break;
                case 6: Send(Key.Escape);break;
                case 7: Assert(flow,!PauseMenu.EnPausa&&Time.timeScale==1,"Escape resumes live Update");Send();break;
                case 8: Send(Key.C);break;
                case 9: Assert(flow,flowPlayer.IsCrouched,"C crouches actual Player");Send();break;
                case 10: Assert(flow,!flowPlayer.IsCrouched,"C release restores standing");Send(Key.D);nextStep+=.25;break;
                case 11: Assert(flow,flowPlayer.GetComponent<Rigidbody2D>().linearVelocity.x>0,"D moves actual Player");Send(Key.D,Key.LeftShift);nextStep+=.15;break;
                case 12: Assert(flow,flowPlayer.State==TraversalState.Sprinting&&flowPlayer.Stamina.Current<100,"Shift sprints and spends stamina");Send();nextStep+=.6;break;
                case 13: Assert(flow,flowPlayer.Grounded,"Grounded before jump");Send(Key.Space);break;
                case 14: Assert(flow,!flowPlayer.Grounded&&flowPlayer.GetComponent<Rigidbody2D>().linearVelocity.y>0,"Space jumps actual Player");Send();jumpStamina=flowPlayer.Stamina.Current;nextStep+=.02;break;
                case 15: Send(Key.Space);break;
                case 16: Assert(flow,flowPlayer.Stamina.Current<=jumpStamina-flowPlayer.staminaSettings.DoubleJumpCost+.1f,"Second Space spends double jump stamina (before="+jumpStamina+", after="+flowPlayer.Stamina.Current+", grounded="+flowPlayer.Grounded+", vy="+flowPlayer.GetComponent<Rigidbody2D>().linearVelocity.y+", cooldown="+flowPlayer.jumpCooldown+")");Send();break;
                default: EndFlow();break;
            }
        }
        catch(Exception e){EndFlow(e);}
    }
    static void Scenes()
    {
        var report=new Report();var setup=UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
        try
        {
            if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
            foreach(string name in new[]{"MainMenu","JustMe","GameOver","Creditos","JustMeLevelCreator"})
            {
                var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");
                int modules=0;
                foreach(var root in scene.GetRootGameObjects())
                {
                    foreach(var module in root.GetComponentsInChildren<InputSystemUIInputModule>(true))
                    {Assert(report,module.actionsAsset!=null&&module.point!=null&&module.leftClick!=null&&module.move!=null&&module.submit!=null&&module.cancel!=null,name+" persisted UI references");modules++;}
                    Assert(report,root.GetComponentsInChildren<StandaloneInputModule>(true).Length==0,name+" no legacy UI module: "+root.name);
                }
                Assert(report,modules==1,name+" exactly one input module");
            }
            File.WriteAllText("Backups/InputMigration/editor-log-path.txt",Application.consoleLogPath);
        }
        catch(Exception e){report.error=e.ToString();}
        finally{UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup);}
        File.WriteAllText("Backups/InputMigration/persisted-scenes.json",JsonUtility.ToJson(report,true));
    }
}
