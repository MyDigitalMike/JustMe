using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    public  int CantidadNiveles;
    public float currentTime;
    public int CantidadMasNivel;
    public GameData(Libro libro)
    {
        CantidadNiveles = libro.Nivel;
        currentTime = libro.currentTime;
        return;
    }

}
