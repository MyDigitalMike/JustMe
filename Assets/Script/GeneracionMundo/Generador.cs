using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
public enum Algoritmo
{
    PerlinNoise,
    PerlinNoiseSuavisado,
    RandomWalk,
    RandomWalkSuavizado,
    PerlinNoiseCueva,
    RandomWalkCueva,
    TunelDireccional,
    MapaAleatorio,
    AutomataCelularMoore,
    AutomataCelularVonNeumann
}
public class Generador : MonoBehaviour
{
    #region Headers
    [Header("Referencias")]
    public Tilemap MapaDeCasillas;
    public TileBase Casilla;
    [Header("Dimensiones")]
    public int Ancho = 60;
    public int Alto = 34;
    [Header("Semilla Generadora")]
    public bool SemillaAleatoria = true;
    public float Semilla = 0;
    [Header("Algoritmo")]
    public Algoritmo algoritmo = Algoritmo.PerlinNoise;
    [Header("Perlin Noise Suavisado")]
    public int Intervalo = 2;
    [Header("Random Walk Suavizado")]
    public int MinimoAnchoSeccion = 4;
    [Header("Cuevas")]
    public bool LosBordesSonMuros = true;
    [Header("Perlin Noise Cueva")]
    public float Modificador = 0.1f;
    public float OffSetX = 0;
    public float OffSetY = 0;
    [Header("Random Walk Cueva")]
    [Range(0, 1)]
    public float PorcentajeEliminar;
    public bool MovimientoEnDiagonal;
    [Header("Tunel Direccional")]
    public int AnchoMaximo = 4;
    public int AnchoMinimo = 1;
    public int DesplazamientoMaximo = 2;
    [Range(0, 1)]
    public float Aspereza = 0.75f;
    [Range(0, 1)]
    public float Desplazamiento = 0.75f;
    [Header("Atómata Celular")]
    [Range(0, 1)]
    public float PorcentajeDeRelleno = 0.45f;
    public int TotalDePasadas = 3;
    public GameObject Jugador;
    public GameObject Libro;
    public GameObject Enemy;
    [Header("Personaje Settings")]
    public int XPersonaje = 1;
    public int YPersonaje = 1;
    [Header("Libro Settings")]
    public float XLibro = 1;
    public float YLibro = 1;
    [Header("Enemigo Settings")]
    public float XEnemigo = 1;
    public float YEnemigo = 1;
    #endregion
    #region Parametros De Generación y Limpieza;
    #region Generar Mapa
    public void GenerarMapa()
    {
        //Limpiamoz el mapa de casillas
        MapaDeCasillas.ClearAllTiles();
        //Se Crea El Array bidimencional del mapa
        int[,] Mapa = null;
        //Generamos una semilla nueva de manera aleatoria
        if (SemillaAleatoria)
        {
            Semilla = Random.Range(0f, 1000f);
        }
        switch (algoritmo)
        {
            case Algoritmo.PerlinNoise:
                Mapa = Metodos.GenerarArray(Ancho, Alto, true);
                Mapa = Metodos.PerlinNoise(Mapa, Semilla);
                break;
            case Algoritmo.PerlinNoiseSuavisado:
                Mapa = Metodos.GenerarArray(Ancho, Alto, true);
                Mapa = Metodos.PerlinNoiseSuavizado(Mapa, Semilla, Intervalo);
                break;
            case Algoritmo.RandomWalk:
                Mapa = Metodos.GenerarArray(Ancho, Alto, true);
                Mapa = Metodos.RandomWalk(Mapa, Semilla);
                break;
            case Algoritmo.RandomWalkSuavizado:
                Mapa = Metodos.GenerarArray(Ancho, Alto, true);
                Mapa = Metodos.RandomWalkSuavizado(Mapa, Semilla, MinimoAnchoSeccion);
                break;
            case Algoritmo.PerlinNoiseCueva:
                Mapa = Metodos.GenerarArray(Ancho, Alto, false);
                Mapa = Metodos.PerlinNoiseCuevas(Mapa, Modificador, LosBordesSonMuros, OffSetX, OffSetY, Semilla);
                break;
            case Algoritmo.RandomWalkCueva:
                Mapa = Metodos.GenerarArray(Ancho, Alto, false);
                Mapa = Metodos.RandomWalkCuevas(Mapa, Semilla, PorcentajeEliminar, LosBordesSonMuros);
                break;
            case Algoritmo.TunelDireccional:
                Mapa = Metodos.GenerarArray(Ancho, Alto, false);
                Mapa = Metodos.TunelDireccional(Mapa, Semilla, AnchoMinimo, AnchoMaximo, Aspereza, DesplazamientoMaximo, Desplazamiento);
                break;
            case Algoritmo.MapaAleatorio:
                Mapa = Metodos.GenerarMapaAleatorio(Ancho, Alto, Semilla, PorcentajeDeRelleno, LosBordesSonMuros);
                break;
            case Algoritmo.AutomataCelularMoore:
                Mapa = Metodos.GenerarMapaAleatorio(Ancho, Alto, Semilla, PorcentajeDeRelleno, LosBordesSonMuros);
                Mapa = Metodos.AutomataCelularMoore(Mapa, TotalDePasadas, LosBordesSonMuros, Jugador, XPersonaje, YPersonaje, Libro, XLibro, YLibro);
                break;
            case Algoritmo.AutomataCelularVonNeumann:
                Mapa = Metodos.GenerarMapaAleatorio(Ancho, Alto, Semilla, PorcentajeDeRelleno, LosBordesSonMuros);
                Mapa = Metodos.AutomataCelularVonNeumann(Mapa, TotalDePasadas, LosBordesSonMuros, Jugador, XPersonaje, YPersonaje, Libro, XLibro, YLibro);
                break;
        }
        Metodos.GenerarMapa(Mapa, MapaDeCasillas, Casilla);
        //int[,] Mapa = Metodos.GenerarArray(Ancho, Alto, false);
        //Metodos.GenerarMapa(Mapa, MapaDeCasillas, Casilla);
    }
    #endregion
    #region Limpiar Mapa
    public void LimpiarMapa()
    {
        MapaDeCasillas.ClearAllTiles();
    }
    #endregion
    #endregion
}
