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

    void LateUpdate()
    {
        if (player == null) return;
        float depth = Mathf.Clamp01((surfaceHeight - player.position.y) / Mathf.Max(0.1f, transitionDepth));
        if (ambient != null) ambient.intensity = Mathf.Lerp(surfaceAmbient, caveAmbient, depth);
        if (playerLight != null)
        {
            playerLight.transform.position = player.position + Vector3.up * 0.3f;
            playerLight.intensity = Mathf.Lerp(0.15f, 0.7f, depth);
        }
    }
}
