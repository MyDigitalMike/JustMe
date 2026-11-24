using UnityEngine;

// Two camera-covering background planes: no colliders, texture tiling or terrain changes.
[DefaultExecutionOrder(1100)]
public sealed class WorldFog : MonoBehaviour
{
    public Material materialTemplate;
    public int sortingLayerID;
    public int sortingOrder = -50;
    [Range(0, 1)] public float density = .65f;
    public Color fogColor = new Color(.5f, .62f, .72f, 1);
    public bool visible = true;
    public float AnimationTime { get; private set; }
    Camera sceneCamera;
    WorldLighting lighting;
    Mesh quad;
    MeshRenderer[] planes;
    MaterialPropertyBlock properties;

    void Start()
    {
        sceneCamera = Camera.main; lighting = GetComponent<WorldLighting>();
        if (sceneCamera == null || materialTemplate == null) { enabled = false; return; }
        quad = new Mesh { name = "Scene fog quad" };
        quad.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(.5f, .5f), new Vector3(-.5f, .5f) };
        quad.triangles = new[] { 0, 1, 2, 0, 2, 3 }; quad.RecalculateBounds();
        properties = new MaterialPropertyBlock(); planes = new MeshRenderer[2];
        for (int i = 0; i < planes.Length; i++)
        {
            var plane = new GameObject("Atmospheric mist " + i, typeof(MeshFilter), typeof(MeshRenderer));
            plane.transform.SetParent(transform, false); plane.GetComponent<MeshFilter>().sharedMesh = quad;
            planes[i] = plane.GetComponent<MeshRenderer>(); planes[i].sharedMaterial = materialTemplate;
            planes[i].sortingLayerID = sortingLayerID; planes[i].sortingOrder = sortingOrder + i * 20;
        }
        Refresh();
    }
    void Update() { if (Debug.isDebugBuild && GameInput.GetKeyDown(KeyCode.F10)) visible = !visible; }
    void LateUpdate() { AnimationTime += Time.deltaTime; Refresh(); }

    public void Refresh()
    {
        if (planes == null || sceneCamera == null) return;
        float height = sceneCamera.orthographicSize * 2 + 1;
        float width = height * sceneCamera.aspect + 1;
        Vector3 cameraPosition = sceneCamera.transform.position;
        bool masked = lighting != null && !lighting.DebugFullVisibility && lighting.nearTotalDarkness;
        float radius = masked ? (lighting.player != null ? lighting.visibilityRadius : 0) : -1;
        Vector3 center = masked && lighting.player != null ? lighting.player.position + Vector3.up * .1f : Vector3.zero;
        for (int i = 0; i < planes.Length; i++)
        {
            var plane = planes[i]; plane.enabled = visible && density > 0;
            plane.transform.position = new Vector3(cameraPosition.x, cameraPosition.y, 3 + i);
            plane.transform.localScale = new Vector3(width, height, 1);
            float speed = i == 0 ? .07f : .24f;
            properties.SetColor("_FogColor", fogColor);
            properties.SetFloat("_Density", density * (i == 0 ? .65f : .4f));
            properties.SetFloat("_FogTime", AnimationTime);
            properties.SetVector("_CameraOffset", new Vector4(cameraPosition.x * (1 - speed), cameraPosition.y * (1 - speed), 0, 0));
            properties.SetVector("_Flow", i == 0 ? new Vector4(1, .035f, .008f, 0) : new Vector4(1.6f, -.026f, .005f, 17.3f));
            properties.SetVector("_MaskCenter", center); properties.SetFloat("_MaskRadius", radius);
            plane.SetPropertyBlock(properties);
        }
    }
    void OnDisable() { if (planes != null) foreach (var plane in planes) if (plane != null) plane.enabled = false; }
    void OnDestroy() { if (quad != null) Destroy(quad); }
}
