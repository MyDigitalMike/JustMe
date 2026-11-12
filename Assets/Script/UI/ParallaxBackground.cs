using System;
using UnityEngine;

// Runs after camera-follow/Cinemachine. Each row has three identical, edge-matching sprites.
[DefaultExecutionOrder(1000)]
public class ParallaxBackground : MonoBehaviour
{
    [Serializable]
    public class Layer
    {
        public SpriteRenderer[] copies;
        public Vector2 factor = new Vector2(0.2f, 0.9f);
        public Vector2 offset;
        [Min(0f)] public float pixelsPerUnit;
        [NonSerialized] public Vector3 origin;
        [NonSerialized] public float width;
    }

    public Camera targetCamera;
    public Layer[] layers;
    private Vector3 cameraOrigin;
    private bool initialized;

    void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) { enabled = false; return; }
        cameraOrigin = targetCamera.transform.position;
        if (layers == null) return;
        foreach (Layer layer in layers)
        {
            if (layer.copies == null || layer.copies.Length != 3 || layer.copies[0] == null) continue;
            layer.width = layer.copies[0].bounds.size.x;
            layer.origin = new Vector3(cameraOrigin.x + layer.offset.x, cameraOrigin.y + layer.offset.y, 5f);
        }
        initialized = true;
        LateUpdate();
    }

    void LateUpdate()
    {
        if (!initialized || targetCamera == null) return;
        Vector3 cameraPosition = targetCamera.transform.position;
        Vector3 delta = cameraPosition - cameraOrigin;
        foreach (Layer layer in layers)
        {
            if (layer.width <= 0f) continue;
            float x = layer.origin.x + delta.x * layer.factor.x;
            float y = layer.origin.y + delta.y * layer.factor.y;
            x += Mathf.Round((cameraPosition.x - x) / layer.width) * layer.width;
            if (layer.pixelsPerUnit > 0f)
            {
                x = Mathf.Round(x * layer.pixelsPerUnit) / layer.pixelsPerUnit;
                y = Mathf.Round(y * layer.pixelsPerUnit) / layer.pixelsPerUnit;
            }
            for (int i = 0; i < 3; i++)
                if (layer.copies[i] != null)
                    layer.copies[i].transform.position = new Vector3(x + (i - 1) * layer.width, y, layer.origin.z);
        }
    }
}
