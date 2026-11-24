using UnityEngine;

// Isolated finite-room prototype. Not attached to production scenes or the generator.
[DefaultExecutionOrder(1000)]
public sealed class ParallaxDepthDemo : MonoBehaviour
{
    public Camera targetCamera;
    public SpriteRenderer[] layers;
    public WorldFog fog;
    [Tooltip("Apparent displacement relative to camera motion; terrain is 1.")]
    public float[] apparentSpeeds = { .05f, .18f, .42f, .70f };
    [SerializeField] bool depthEnabled = true;
    Vector3 cameraOrigin;
    Vector3[] origins;

    public bool DepthEnabled
    {
        get { return depthEnabled; }
        set { if (depthEnabled == value) return; Rebase(); depthEnabled = value; }
    }

    void Start() { Rebase(); }
    void Update() { if (GameInput.GetKeyDown(KeyCode.F9)) DepthEnabled = !DepthEnabled; }
    void LateUpdate() { RefreshLayers(); }

    public void Rebase()
    {
        if (targetCamera == null || layers == null || apparentSpeeds == null || layers.Length != apparentSpeeds.Length) return;
        cameraOrigin = targetCamera.transform.position;
        if (origins == null || origins.Length != layers.Length) origins = new Vector3[layers.Length];
        for (int i = 0; i < layers.Length; i++) if (layers[i] != null) origins[i] = layers[i].transform.position;
    }

    public void RefreshLayers()
    {
        if (targetCamera == null || layers == null || origins == null || apparentSpeeds.Length != layers.Length) return;
        Vector3 delta = targetCamera.transform.position - cameraOrigin; delta.z = 0;
        for (int i = 0; i < layers.Length; i++)
            if (layers[i] != null)
                layers[i].transform.position = origins[i] + delta * (depthEnabled ? 1f - apparentSpeeds[i] : 0f);
    }

    void OnGUI()
    {
        var previous = GUI.matrix;
        float scale = Mathf.Clamp(Screen.width / 1280f, .7f, 1.3f);
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        GUI.Box(new Rect(10, 10, 610, 62), GUIContent.none);
        GUI.Label(new Rect(22, 16, 590, 22), "PRUEBA DE PROFUNDIDAD   |   A/D o flechas: caminar   |   Espacio: saltar");
        if (GUI.Button(new Rect(22, 41, 270, 24), depthEnabled ? "Parallax ACTIVADO  [F9]" : "Sin parallax  [F9]")) DepthEnabled = !DepthEnabled;
        if (fog != null && GUI.Button(new Rect(307, 41, 250, 24), fog.visible ? "Niebla ACTIVADA  [F10]" : "Niebla desactivada  [F10]")) fog.visible = !fog.visible;
        GUI.matrix = previous;
    }
}
