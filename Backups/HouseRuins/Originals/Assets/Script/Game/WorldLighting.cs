using UnityEngine;
using UnityEngine.Rendering.Universal;

// Scene-local prototype. It never edits pipeline assets or shared materials.
public class WorldLighting : MonoBehaviour
{
    public Light2D ambient;
    public Light2D playerLight;
    public float surfaceHeight = 23f;
    public float transitionDepth = 8f;
    [Range(0f, 1f)] public float caveAmbient = 0.3f;
    [Range(0f, 1f)] public float surfaceAmbient = 0.85f;
    public Transform player;
    public bool nearTotalDarkness = true;
    [Min(.2f)] public float visibilityRadius = 1.1f;
    [Min(0)] public float localIntensity = 1.1f;
    [Range(0, 1)] public float localFalloff = .7f;
    [Range(0, 1)] public float backgroundDarkness = 1f;
    [Min(0)] public float followSharpness = 18f;
    [Header("Temporary manual testing (Editor/development only)")]
    [Tooltip("F8 toggles full terrain lighting, including while paused. Release builds keep normal lighting.")]
    [SerializeField] bool debugFullVisibility = true;
    public bool DebugFullVisibility
    {
        get { return Debug.isDebugBuild && debugFullVisibility; }
        set { debugFullVisibility = value; }
    }
    Renderer[] backgrounds;
    Color[] backgroundColors;
    Material[] originalMaterials, localMaterials;
    bool[] originalVisibility;
    float previousDarkness = -1;
    Camera sceneCamera;
    CameraClearFlags originalClearFlags;
    Color originalCameraColor;
    float originalInnerRadius, originalOuterRadius;
    bool darknessApplied;

    void Start()
    {
        if (playerLight != null) { originalInnerRadius = playerLight.pointLightInnerRadius; originalOuterRadius = playerLight.pointLightOuterRadius; }
        sceneCamera = Camera.main;
        if (sceneCamera != null) { originalClearFlags = sceneCamera.clearFlags; originalCameraColor = sceneCamera.backgroundColor; }
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true)) list.Add(sprite);
        if (Camera.main != null)
            foreach (ScrollingBackground background in Camera.main.GetComponentsInChildren<ScrollingBackground>(true))
                if (background.bgRend != null) list.Add(background.bgRend);
        backgrounds = list.ToArray(); backgroundColors = new Color[backgrounds.Length];
        originalMaterials = new Material[backgrounds.Length]; localMaterials = new Material[backgrounds.Length]; originalVisibility = new bool[backgrounds.Length];
        for (int i = 0; i < backgrounds.Length; i++)
        {
            var sprite = backgrounds[i] as SpriteRenderer;
            backgroundColors[i] = sprite != null ? sprite.color : backgrounds[i].sharedMaterial.HasProperty("_Color") ? backgrounds[i].sharedMaterial.GetColor("_Color") : Color.white;
            originalVisibility[i] = backgrounds[i].enabled;
            if (sprite == null)
            {
                originalMaterials[i] = backgrounds[i].sharedMaterial;
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) { backgrounds[i].enabled = false; continue; }
                Material material = new Material(shader);
                material.SetTexture("_BaseMap", originalMaterials[i].mainTexture);
                material.SetTextureScale("_BaseMap", originalMaterials[i].mainTextureScale);
                material.SetTextureOffset("_BaseMap", originalMaterials[i].mainTextureOffset);
                localMaterials[i] = material;
            }
        }
    }

    void Update()
    {
        if (Debug.isDebugBuild && Input.GetKeyDown(KeyCode.F8)) debugFullVisibility = !debugFullVisibility;
    }

    void LateUpdate()
    {
        if (player == null) return;
        if (DebugFullVisibility)
        {
            if (darknessApplied) RestorePresentation();
            if (ambient != null) ambient.intensity = 1f;
            if (playerLight != null) playerLight.intensity = 0f;
            if (sceneCamera != null) { sceneCamera.clearFlags = CameraClearFlags.SolidColor; sceneCamera.backgroundColor = Color.black; }
            return;
        }
        if (nearTotalDarkness)
        {
            darknessApplied = true;
            if (sceneCamera != null) { sceneCamera.clearFlags = CameraClearFlags.SolidColor; sceneCamera.backgroundColor = Color.black; }
            if (ambient != null) ambient.intensity = 0;
            if (playerLight != null)
            {
                Vector3 target = player.position + Vector3.up * .1f;
                playerLight.transform.position = Vector3.Distance(playerLight.transform.position, target) > visibilityRadius * 2 ? target :
                    Vector3.Lerp(playerLight.transform.position, target, 1 - Mathf.Exp(-followSharpness * Time.deltaTime));
                if (Mathf.Abs(playerLight.pointLightOuterRadius - visibilityRadius) > .001f) playerLight.pointLightOuterRadius = visibilityRadius;
                if (Mathf.Abs(playerLight.pointLightInnerRadius - visibilityRadius * .2f) > .001f) playerLight.pointLightInnerRadius = visibilityRadius * .2f;
                playerLight.intensity = localIntensity; playerLight.falloffIntensity = localFalloff;
            }
            if (backgrounds != null && previousDarkness != backgroundDarkness)
            {
                for (int i = 0; i < backgrounds.Length; i++)
                {
                    if (backgrounds[i] == null) continue;
                    Color color = Color.Lerp(backgroundColors[i], new Color(0, 0, 0, backgroundColors[i].a), backgroundDarkness);
                    var sprite = backgrounds[i] as SpriteRenderer;
                    if (sprite != null) sprite.color = color;
                    else
                    {
                        if (localMaterials[i] != null) { backgrounds[i].sharedMaterial = localMaterials[i]; localMaterials[i].SetColor("_BaseColor", color); }
                        backgrounds[i].enabled = originalVisibility[i] && backgroundDarkness < .999f;
                    }
                }
                previousDarkness = backgroundDarkness;
            }
            return;
        }
        if (darknessApplied) RestorePresentation();
        float depth = Mathf.Clamp01((surfaceHeight - player.position.y) / Mathf.Max(0.1f, transitionDepth));
        if (ambient != null) ambient.intensity = Mathf.Lerp(surfaceAmbient, caveAmbient, depth);
        if (playerLight != null)
        {
            playerLight.transform.position = player.position + Vector3.up * 0.3f;
            playerLight.intensity = Mathf.Lerp(0.15f, 0.7f, depth);
        }
    }

    void RestorePresentation()
    {
        if (sceneCamera != null) { sceneCamera.clearFlags = originalClearFlags; sceneCamera.backgroundColor = originalCameraColor; }
        if (playerLight != null) { playerLight.pointLightInnerRadius = originalInnerRadius; playerLight.pointLightOuterRadius = originalOuterRadius; }
        if (backgrounds != null)
            for (int i = 0; i < backgrounds.Length; i++)
            {
                if (backgrounds[i] == null) continue;
                var sprite = backgrounds[i] as SpriteRenderer;
                if (sprite != null) sprite.color = backgroundColors[i];
                else backgrounds[i].sharedMaterial = originalMaterials[i];
                backgrounds[i].enabled = originalVisibility[i];
            }
        previousDarkness = -1; darknessApplied = false;
    }

    void OnDestroy()
    {
        RestorePresentation();
        if (localMaterials == null) return;
        foreach (Material material in localMaterials) if (material != null) Destroy(material);
    }
}
