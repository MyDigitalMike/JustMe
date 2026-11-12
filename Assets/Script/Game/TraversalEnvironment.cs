using UnityEngine;

// Scene-local designated traversal regions. No per-cell trigger objects or global searches.
public class TraversalEnvironment : MonoBehaviour
{
    readonly System.Collections.Generic.List<GameObject> markers = new System.Collections.Generic.List<GameObject>();
    Sprite markerSprite;
    public Bounds[] climbableAreas = new Bounds[0];
    public Bounds[] voidAreas = new Bounds[0];
    public bool bottomVoidEnabled;
    public float bottomVoidY;

    public void DrawDesignatedSurfaces(Material litMaterial, Vector3[] warningPositions)
    {
        foreach (GameObject marker in markers) { marker.SetActive(false); Destroy(marker); }
        markers.Clear();
        if (markerSprite == null)
        {
            var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, Color.white); texture.Apply(); texture.filterMode = FilterMode.Point;
            markerSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
        }
        foreach (Bounds area in climbableAreas) Marker("Designated rope", area.center, new Vector2(.045f, area.size.y), new Color(.7f, .5f, .3f), litMaterial);
        foreach (Vector3 cue in warningPositions) Marker("Void edge warning", cue, new Vector2(.3f, .06f), new Color(.95f, .18f, .12f), litMaterial);
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
