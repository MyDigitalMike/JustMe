using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckGround : MonoBehaviour
{
    private Player player;
    void Start()
    {
        player = GetComponentInParent<Player>();
    }
    private void OnCollisionStay2D(Collision2D col)
    {
        if(col.gameObject.tag == "Ground")
        {
            player.Grounded = true;
        }
    }
    private void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.tag == "Ground")
        {
            player.Grounded = false;
        }
    }
}
