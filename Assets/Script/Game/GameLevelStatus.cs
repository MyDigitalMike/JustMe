using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameLevelStatus : MonoBehaviour
{
    public int CantidadStatusNiveles;
    public static int CantidadNiveles2 = 1;
    public static GameLevelStatus gameLevelStatus;
    void Awake()
    {
        if(gameLevelStatus == null){
            gameLevelStatus = this;
            DontDestroyOnLoad(gameObject);
        }
        else if(gameLevelStatus != this){
            Destroy(gameObject);
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
