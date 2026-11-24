using UnityEngine;

// Background-only, bounded object pool. Seeded cells cover movement in both axes.
[DefaultExecutionOrder(1200)]
public sealed class WorldBackdropLayers : MonoBehaviour
{
    public Sprite[] sprites;
    public Material materialTemplate;
    public bool visible = true;
    const float CellWidth = 24, CellHeight = 14;
    static readonly float[] Speeds = { .18f, .42f, .70f };
    static readonly int[] Orders = { -60, -20, -10 };
    sealed class Cell
    {
        public SpriteRenderer renderer;
        public MaterialPropertyBlock properties;
        public int x = int.MinValue, y;
        public bool present;
    }
    Cell[] cells;
    Camera target;
    WorldLighting lighting;
    int seed, sortingLayer, terrainOrder;

    public void Configure(int worldSeed, int layerID, int order)
    {
        seed = worldSeed; sortingLayer = layerID; terrainOrder = order;
        if (cells != null) foreach (var cell in cells) cell.x = int.MinValue;
    }
    void Start()
    {
        target = Camera.main; lighting = GetComponent<WorldLighting>();
        if (target == null || materialTemplate == null || sprites == null || sprites.Length != 3) { enabled = false; return; }
        cells = new Cell[27];
        for (int i = 0; i < cells.Length; i++)
        {
            int depth = i / 9;
            var child = new GameObject("Distant architecture " + depth + "/" + i % 9, typeof(SpriteRenderer));
            child.transform.SetParent(transform, false);
            var renderer = child.GetComponent<SpriteRenderer>(); renderer.sprite = sprites[depth]; renderer.sharedMaterial = materialTemplate;
            var block = new MaterialPropertyBlock(); Rect rect = sprites[depth].textureRect; Texture2D texture = sprites[depth].texture;
            block.SetTexture("_MainTex", texture);
            block.SetVector("_UvRect", new Vector4(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height));
            cells[i] = new Cell { renderer = renderer, properties = block };
        }
        Refresh();
    }
    void Update() { if (Debug.isDebugBuild && GameInput.GetKeyDown(KeyCode.F9)) visible = !visible; }
    void LateUpdate() { Refresh(); }
    public void Refresh()
    {
        if (cells == null || target == null) return;
        Vector3 camera = target.transform.position;
        bool masked = lighting != null && !lighting.DebugFullVisibility && lighting.nearTotalDarkness;
        float radius = masked ? (lighting.player != null ? lighting.visibilityRadius : 0) : -1;
        Vector3 maskCenter = masked && lighting.player != null ? lighting.player.position + Vector3.up * .1f : Vector3.zero;
        for (int i = 0; i < cells.Length; i++)
        {
            int depth = i / 9; float speed = Speeds[depth]; var cell = cells[i];
            int x = Mathf.FloorToInt(camera.x * speed / CellWidth) + i % 3 - 1;
            int y = Mathf.FloorToInt(camera.y * speed / CellHeight) + i % 9 / 3 - 1;
            uint hash = RuinedHouseTheme.Hash(seed + depth * 137, x, y);
            if (cell.x != x || cell.y != y)
            {
                cell.x = x; cell.y = y; cell.present = hash % 4 != 0;
                cell.renderer.flipX = (hash & 4) != 0;
                float scale = .85f + ((hash >> 4) & 15) / 15f * .3f;
                cell.renderer.transform.localScale = Vector3.one * (CellHeight * scale / sprites[depth].bounds.size.y);
                float tint = depth == 0 ? .58f : depth == 1 ? .76f : .9f;
                cell.renderer.color = new Color(tint * .92f, tint * .97f, tint, 1);
                cell.renderer.sortingLayerID = sortingLayer; cell.renderer.sortingOrder = terrainOrder + Orders[depth];
            }
            float jitterX = ((hash >> 10) & 15) / 15f * 4 - 2;
            float jitterY = ((hash >> 14) & 15) / 15f * 2 - 1;
            cell.renderer.transform.position = new Vector3((x + .5f) * CellWidth + jitterX + camera.x * (1 - speed),
                (y + .5f) * CellHeight + jitterY + camera.y * (1 - speed), 5 + depth);
            cell.renderer.enabled = visible && cell.present;
            cell.properties.SetVector("_MaskCenter", maskCenter); cell.properties.SetFloat("_MaskRadius", radius);
            cell.renderer.SetPropertyBlock(cell.properties);
        }
    }
    void OnDisable() { if (cells != null) foreach (var cell in cells) if (cell.renderer != null) cell.renderer.enabled = false; }
    void OnDestroy() { if (cells != null) foreach (var cell in cells) if (cell.renderer != null) Destroy(cell.renderer.gameObject); }
}
