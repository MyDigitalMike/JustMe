using System;
using UnityEngine;
using UnityEngine.InputSystem;

// New Input System reader preserving the project's existing virtual-input API and axis tuning.
public static class GameInput
{
    [Serializable] public sealed class Axis
    {
        public string name, negative, positive, altNegative, altPositive;
        public float gravity, dead, sensitivity;
        public bool snap, invert;
        public int type, axis, joyNum;
        [NonSerialized] public InputAction action;
        [NonSerialized] public float value;
    }
    [Serializable] public sealed class Configuration { public Axis[] axes; }
    static Axis[] axes;
    static int frame = -1;
    static bool focused = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        if (axes != null) foreach (var axis in axes) axis.action?.Dispose();
        axes = null; frame = -1; focused = true;
        Application.focusChanged -= FocusChanged; Application.focusChanged += FocusChanged;
    }
    static void FocusChanged(bool focused)
    {
        GameInput.focused = focused;
        if (!focused && axes != null) foreach (var axis in axes) axis.value = 0;
        frame = -1;
    }
    static void Ensure()
    {
        if (axes != null) return;
        var configuration = Resources.Load<TextAsset>("GameInputBindings");
        if (configuration == null) throw new InvalidOperationException("Missing migrated input bindings.");
        axes = JsonUtility.FromJson<Configuration>(configuration.text).axes;
        foreach (var axis in axes)
        {
            if (axis.type != 0) continue;
            axis.action = new InputAction(axis.name, InputActionType.Value, expectedControlType: "Axis");
            if (!string.IsNullOrEmpty(axis.negative) || !string.IsNullOrEmpty(axis.altNegative))
            {
                var composite = axis.action.AddCompositeBinding("1DAxis");
                foreach (string key in new[] {axis.negative, axis.altNegative}) if (!string.IsNullOrEmpty(key)) composite.With("Negative", Binding(key));
                foreach (string key in new[] {axis.positive, axis.altPositive}) if (!string.IsNullOrEmpty(key)) composite.With("Positive", Binding(key));
            }
            else foreach (string key in new[] {axis.positive, axis.altPositive}) if (!string.IsNullOrEmpty(key)) axis.action.AddBinding(Binding(key));
            axis.action.Enable();
        }
    }
    static string Binding(string key)
    {
        if (key.StartsWith("joystick button ", StringComparison.Ordinal))
        {
            int button = int.Parse(key.Substring(16));
            string[] controls = {"buttonSouth","buttonEast","buttonWest","buttonNorth","leftShoulder","rightShoulder","select","start","leftStickPress","rightStickPress"};
            if (button >= controls.Length) throw new InvalidOperationException("Unsupported old joystick button: " + key);
            return "<Gamepad>/" + controls[button];
        }
        if (key.StartsWith("mouse ", StringComparison.Ordinal))
            return "<Mouse>/" + new[] {"leftButton","rightButton","middleButton"}[int.Parse(key.Substring(6))];
        switch (key)
        {
            case "left": key="leftArrow"; break; case "right": key="rightArrow"; break;
            case "up": key="upArrow"; break; case "down": key="downArrow"; break;
            case "left ctrl": key="leftCtrl"; break; case "right ctrl": key="rightCtrl"; break;
            case "left shift": key="leftShift"; break; case "right shift": key="rightShift"; break;
            case "left alt": key="leftAlt"; break; case "right alt": key="rightAlt"; break;
            case "return": key="enter"; break; case "enter": key="numpadEnter"; break;
            case "page up": key="pageUp"; break; case "page down": key="pageDown"; break;
        }
        return "<Keyboard>/" + key;
    }
    public static float Smooth(float current, float target, float sensitivity, float gravity, bool snap, float dt)
    {
        if (snap && target != 0 && Mathf.Sign(current) != Mathf.Sign(target)) current = 0;
        return Mathf.MoveTowards(current, target, (target == 0 ? gravity : sensitivity) * dt);
    }
    static float Raw(Axis axis)
    {
        if (!focused) return 0;
        float value = 0;
        if (axis.type == 0) value = axis.action.ReadValue<float>();
        else if (axis.type == 1 && Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            value = (axis.axis == 0 ? delta.x : axis.axis == 1 ? delta.y : Mouse.current.scroll.ReadValue().y / 120f) * axis.sensitivity;
        }
        else if (axis.type == 2)
        {
            for (int i=0;i<Gamepad.all.Count;i++)
            {
                if (axis.joyNum != 0 && axis.joyNum != i+1) continue;
                var pad = Gamepad.all[i];
                Vector2 stick = axis.axis < 2 ? pad.leftStick.ReadUnprocessedValue() : pad.rightStick.ReadUnprocessedValue();
                float sample;
                switch (axis.axis)
                {
                    case 0: sample=stick.x; break; case 1: sample=-stick.y; break;
                    case 2: sample=pad.leftTrigger.ReadValue()-pad.rightTrigger.ReadValue(); break;
                    case 3: sample=stick.x; break; case 4: sample=-stick.y; break;
                    case 5: sample=pad.dpad.ReadValue().x; break; case 6: sample=pad.dpad.ReadValue().y; break;
                    default: sample=0; break;
                }
                if (Mathf.Abs(sample) > Mathf.Abs(value)) value = sample;
            }
            if (Mathf.Abs(value) < axis.dead) value=0;
        }
        return axis.invert ? -value : value;
    }
    static void Tick()
    {
        Ensure(); if (frame == Time.frameCount) return; frame=Time.frameCount;
        foreach (var axis in axes)
        {
            float raw=Raw(axis);
            axis.value=axis.type==0?Smooth(axis.value,raw,axis.sensitivity,axis.gravity,axis.snap,Time.unscaledDeltaTime):raw;
        }
    }
    public static float GetAxis(string name) { Tick();return ReadAxis(name,false); }
    public static float GetAxisRaw(string name) { Ensure();return ReadAxis(name,true); }
    static float ReadAxis(string name,bool raw)
    {
        float result=0;foreach(var axis in axes) if(axis.name==name){float value=raw?Raw(axis):axis.value;if(Mathf.Abs(value)>Mathf.Abs(result))result=value;}return result;
    }
    public static bool GetButton(string name){if(!focused)return false;Ensure();foreach(var a in axes)if(a.name==name&&a.action!=null&&a.action.IsPressed())return true;return false;}
    public static bool GetButtonDown(string name){if(!focused)return false;Ensure();foreach(var a in axes)if(a.name==name&&a.action!=null&&a.action.WasPressedThisFrame())return true;return false;}
    public static bool GetButtonUp(string name){if(!focused)return false;Ensure();foreach(var a in axes)if(a.name==name&&a.action!=null&&a.action.WasReleasedThisFrame())return true;return false;}
    static KeyControlWrapper KeyFor(KeyCode code)
    {
        string name=code==KeyCode.LeftControl?"LeftCtrl":code==KeyCode.RightControl?"RightCtrl":code.ToString();
        return new KeyControlWrapper(Keyboard.current,Enum.TryParse<Key>(name,true,out var key)?key:Key.None);
    }
    readonly struct KeyControlWrapper
    {
        readonly Keyboard keyboard; readonly Key key;
        public KeyControlWrapper(Keyboard keyboard,Key key){this.keyboard=keyboard;this.key=key;}
        public bool Held => keyboard!=null&&key!=Key.None&&keyboard[key].isPressed;
        public bool Down => keyboard!=null&&key!=Key.None&&keyboard[key].wasPressedThisFrame;
    }
    public static bool GetKey(KeyCode code)=>focused&&KeyFor(code).Held;
    public static bool GetKeyDown(KeyCode code)=>focused&&KeyFor(code).Down;
    public static Vector3 mousePosition=>Mouse.current!=null?(Vector3)Mouse.current.position.ReadValue():Vector3.zero;
    public static bool GetMouseButtonDown(int button)=>Mouse.current!=null&&(button==0?Mouse.current.leftButton:button==1?Mouse.current.rightButton:Mouse.current.middleButton).wasPressedThisFrame;
    public static Vector3 acceleration
    {
        get {var sensor=Accelerometer.current;if(sensor==null)return Vector3.zero;if(!sensor.enabled)InputSystem.EnableDevice(sensor);return sensor.acceleration.ReadValue();}
    }
}
