using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fondo : MonoBehaviour
{
    public float Speed = 1.5f;
    Vector2 FondoPos;
    void MoveFondo()
    {
        FondoPos += new Vector2(0, Time.deltaTime * Speed);

        GetComponent<Renderer>().material.mainTextureOffset = FondoPos;
    }
}
