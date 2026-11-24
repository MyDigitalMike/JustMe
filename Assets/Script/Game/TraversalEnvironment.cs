using UnityEngine;

// Scene-local designated traversal regions. No per-cell trigger objects or global searches.
public class TraversalEnvironment : MonoBehaviour
{
    public struct LedgeDecoration { public Vector3 Corner; public int Inward; public float Width; }
    public LedgeDecoration[] ledgeDecorations = new LedgeDecoration[0];
    int artworkSortingLayer, artworkSortingOrder;
    readonly System.Collections.Generic.List<GameObject> markers = new System.Collections.Generic.List<GameObject>();
    Sprite markerSprite;
    Sprite ropeSprite, hookSprite, stoneSprite, alternateStoneSprite;
    public Bounds[] climbableAreas = new Bounds[0];
    public Bounds[] voidAreas = new Bounds[0];
    public bool bottomVoidEnabled;
    public float bottomVoidY;

    public void ConfigureArtwork(int sortingLayer, int sortingOrder)
    { artworkSortingLayer = sortingLayer; artworkSortingOrder = sortingOrder; }

    public void DrawDesignatedSurfaces(Material litMaterial, Vector3[] warningPositions)
    {
        Material artworkMaterial = Resources.Load<Material>("HeroTraversal/Artwork");
        if (artworkMaterial != null) litMaterial = artworkMaterial;
        foreach (GameObject marker in markers) { marker.SetActive(false); Destroy(marker); }
        markers.Clear();
        if (markerSprite == null)
        {
            var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, Color.white); texture.Apply(); texture.filterMode = FilterMode.Point;
            markerSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
        }
        if (ropeSprite == null)
            foreach (Sprite sprite in Resources.LoadAll<Sprite>("HeroTraversal/Props"))
            {
                if (sprite.name == "Rope") ropeSprite = sprite;
                else if (sprite.name == "Hook") hookSprite = sprite;
                else if (sprite.name == "Cornice") stoneSprite = sprite;
                else if (sprite.name == "CorniceAlt") alternateStoneSprite = sprite;
            }
        foreach (Bounds area in climbableAreas)
        {
            if (ropeSprite != null)
            {
                Artwork("Braided climbing rope", ropeSprite, area.center, new Vector2(.065f, area.size.y), litMaterial, true);
                if (hookSprite != null) Artwork("Weathered rope anchor", hookSprite,
                    new Vector3(area.center.x, area.max.y - .08f, area.center.z), new Vector2(.19f, .4f), litMaterial, false);
            }
            else Marker("Designated rope", area.center, new Vector2(.045f, area.size.y), new Color(.7f, .5f, .3f), litMaterial);
        }
        for (int i = 0; i < ledgeDecorations.Length; i++)
        {
            Sprite stone = i % 2 == 0 || alternateStoneSprite == null ? stoneSprite : alternateStoneSprite;
            LedgeDecoration ledge = ledgeDecorations[i];
            // Sprite pivot is its upper outside edge, registered to a verified physical corner.
            if (stone != null)
            {
                GameObject lip = Artwork("Weathered stone edge", stone, ledge.Corner,
                    new Vector2(ledge.Width, .15f), litMaterial, false);
                Vector3 scale = lip.transform.localScale; scale.x *= ledge.Inward; lip.transform.localScale = scale;
                lip.GetComponent<SpriteRenderer>().sortingOrder = artworkSortingOrder + 2;
            }
        }
    }

    GameObject Artwork(string label, Sprite sprite, Vector3 position, Vector2 size, Material material, bool tiled)
    {
        var obj = new GameObject(label, typeof(SpriteRenderer)); obj.transform.SetParent(transform, false);
        obj.transform.position = position;
        var renderer = obj.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
        renderer.sharedMaterial = material; renderer.sortingLayerID = artworkSortingLayer; renderer.sortingOrder = artworkSortingOrder + 1;
        Vector2 natural = sprite.bounds.size;
        if (tiled)
        {
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = new Vector2(natural.x, size.y);
            obj.transform.localScale = new Vector3(size.x / natural.x / Mathf.Abs(transform.lossyScale.x), 1 / Mathf.Abs(transform.lossyScale.y), 1);
        }
        else obj.transform.localScale = new Vector3(size.x / natural.x / Mathf.Abs(transform.lossyScale.x), size.y / natural.y / Mathf.Abs(transform.lossyScale.y), 1);
        markers.Add(obj);
        return obj;
    }

    void Marker(string label, Vector3 position, Vector2 size, Color color, Material material)
    {
        var marker = new GameObject(label, typeof(SpriteRenderer)); marker.transform.SetParent(transform, false);
        marker.transform.position = position;
        marker.transform.localScale = new Vector3(size.x / Mathf.Abs(transform.lossyScale.x), size.y / Mathf.Abs(transform.lossyScale.y), 1);
        var renderer = marker.GetComponent<SpriteRenderer>(); renderer.sprite = markerSprite; renderer.color = color;
        renderer.sharedMaterial = material; renderer.sortingOrder = 1; markers.Add(marker);
    }

    void OnDestroy()
    {
        if (markerSprite != null) { Destroy(markerSprite.texture); Destroy(markerSprite); }
    }

    public bool CanClimb(Vector2 position)
    {
        foreach (Bounds area in climbableAreas) if (area.Contains(new Vector3(position.x, position.y, area.center.z))) return true;
        return false;
    }

    public bool IsLethal(Vector2 from, Vector2 to)
    {
        if (bottomVoidEnabled && Mathf.Min(from.y, to.y) < bottomVoidY) return true;
        Vector2 delta = to - from;
        foreach (Bounds area in voidAreas)
        {
            Vector3 origin = new Vector3(from.x, from.y, area.center.z);
            if (area.Contains(origin) || area.Contains(new Vector3(to.x, to.y, area.center.z))) return true;
            if (delta.sqrMagnitude > 0 && area.IntersectRay(new Ray(origin, delta.normalized), out float distance) && distance <= delta.magnitude) return true;
        }
        return false;
    }
}
