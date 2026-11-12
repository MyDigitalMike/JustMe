using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class CameraFollow : MonoBehaviour
{
    public GameObject Follow;
    public Vector2 MinCamPos, MaxCamPos;
    public float SmoothTime;

    private Vector2 Velocity;
    private CinemachineBrain brain;
    void Awake()
    {
        brain = GetComponent<CinemachineBrain>();
    }
    void LateUpdate()
    {
        if (Follow == null) return;
        // The existing virtual camera owns the final pose whenever it is active.
        if (brain != null && brain.isActiveAndEnabled && brain.ActiveVirtualCamera != null) return;
        float PosX = Mathf.SmoothDamp(transform.position.x,
            Follow.transform.position.x, ref Velocity.x, SmoothTime);
        float PosY = Mathf.SmoothDamp(transform.position.y,
            Follow.transform.position.y, ref Velocity.y, SmoothTime);   
        transform.position = new Vector3(
            Mathf.Clamp(PosX, MinCamPos.x, MaxCamPos.x),
            Mathf.Clamp(PosY, MinCamPos.y, MaxCamPos.y),
            transform.position.z);
    }
}
