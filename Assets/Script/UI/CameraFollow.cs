using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public GameObject Follow;
    public Vector2 MinCamPos, MaxCamPos;
    public float SmoothTime;

    private Vector2 Velocity;
    void FixedUpdate()
    {
        float PosX = Mathf.SmoothDamp(transform.position.x,
            Follow.transform.position.x, ref Velocity.x, SmoothTime);
        float PosY = Mathf.SmoothDamp(transform.position.x,
            Follow.transform.position.y, ref Velocity.y, SmoothTime);   
        transform.position = new Vector3(
            Mathf.Clamp(PosX, MinCamPos.x, MaxCamPos.x),
            Mathf.Clamp(PosY, MinCamPos.y, MaxCamPos.y),
            transform.position.z);
    }
}
