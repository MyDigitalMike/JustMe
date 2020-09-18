using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JumpingHard : MonoBehaviour

{
    public float Fuerza = 0;
    public float Angulo = 0;
    public AreaEffector2D FuerzaJump = new AreaEffector2D();
    public void AlterFuerza()
    {
        FuerzaJump.forceMagnitude = Fuerza;
        FuerzaJump.forceAngle = Angulo;
    }
}
