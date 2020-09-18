using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Metodos
{
    #region GenerarArray
    /// <summary>
    /// Genera un array de tipo bidimencional.
    /// </summary>
    /// <param name="Ancho">El Ancho asignado para la generación del mapa 2D, es de tipo entero</param>
    /// <param name="Alto">El Alto asignado para la generación del mapa 2D, es de tipo entero</param>
    /// <param name="Vacio">Verdadero(true) si se quere generar un mapa vacio, falso(false) para tener un mapa completo</param>
    /// <returns>Devueve el mapa 2D generado segun los parametos establecidos</returns>
    public static int[,] GenerarArray(int Ancho, int Alto, bool Vacio)
    {
        int[,] Mapa = new int[Ancho, Alto];
        for (int x = 0; x < Ancho; x++)
        {
            for (int y = 0; y < Alto; y++)
            {
                Mapa[x, y] = Vacio ? 0 : 1;
            }
        }
        return Mapa;
    }
    #endregion
    #region Generar Mapa
    /// <summary>
    /// Se genera el mapa con las casillas con la información porporcionada en "Mapa"
    /// </summary>
    /// <param name="Mapa">Información para la generación del mapa de casillas. 1 = Casillas 2 = No hay casillas</param>
    /// <param name="MapaDeCasillas">Referencia al mapa de casillas dónde se generarán las casillas</param>
    /// <param name="Casilla">Casilla la cual se pintara en el mapa de casillas</param>
    public static void GenerarMapa(int[,] Mapa, Tilemap MapaDeCasillas, TileBase Casilla)
    {
        //Se limpia el mapa de casillas para empezar con uno vacio
        MapaDeCasillas.ClearAllTiles();
        for (int x = 0; x <= Mapa.GetUpperBound(0); x++)
        {
            for (int y = 0; y <= Mapa.GetUpperBound(1); y++)
            {
                // 1 Hay suelo, 0 vacio
                if (Mapa[x, y] == 1)
                {
                    MapaDeCasillas.SetTile(new Vector3Int(x, y, 0), Casilla);
                }
            }
        }

    }
    #endregion
    #region PerlinNoise
    /// <summary>
    /// Genera el terreno con PerlinNoise
    /// </summary>
    /// <param name="Mapa">Array modificado dónde se guarda el genero generado</param>
    /// <param name="Semilla">La semilla que se usa para generar el terreono del mapa</param>
    /// <returns>Se retorna el array modificado con el terreno que se generó</returns>
    public static int[,] PerlinNoise(int[,] Mapa, float Semilla)
    {
        //La altura del punto actual en x 
        int nuevoPunto;
        //Mathf.PerlinNosise Valores entre 0 y 1  Se le resta el punto para que el valor final este eltre -0.5 y 0.5
        float puntoReduccion = 0.5f;
        //Crear Perlin Noise
        for (int x = 0; x <= Mapa.GetUpperBound(0); x++)
        {
            nuevoPunto = Mathf.FloorToInt((Mathf.PerlinNoise(x, Semilla) - puntoReduccion) * Mapa.GetUpperBound(1));
            nuevoPunto += (Mapa.GetUpperBound(1) / 2);
            for (int y = nuevoPunto; y >= 0; y--)
            {
                Mapa[x, y] = 1;
            }
        }
        return Mapa;
    }
    #endregion
    #region PerlinNoiseSuavisado
    /// <summary>
    /// Modifica el algoritmo PerlinNoise a un modo suavizado
    /// </summary>
    /// <param name="Mapa">Modificación al mapa</param>
    /// <param name="Semilla">La Semilla PerlinNoise</param>
    /// <param name="Intervalo">El Intervalo en el que grabaremos la altura</param>
    /// <returns>El mapa ya modificado</returns>
    public static int[,] PerlinNoiseSuavizado(int[,] Mapa, float Semilla, int Intervalo)
    {
        if (Intervalo > 1)
        {
            //Utilizadas en el proceso de suavizado
            Vector2Int posicionActual, posicionAnterior;
            //Los puntos correspendientes para el suavizado de cada eje
            List<int> ruidoX = new List<int>();
            List<int> ruidoY = new List<int>();
            int nuevoPunto, puntos;
            //Generación del ruido
            for (int x = 0; x <= Mapa.GetUpperBound(0); x += Intervalo)
            {
                nuevoPunto = Mathf.FloorToInt(Mathf.PerlinNoise(x, Semilla) * Mapa.GetUpperBound(1));
                ruidoY.Add(nuevoPunto);
                ruidoX.Add(x);
            }
            puntos = ruidoY.Count;
            //Se verifica una pocion anterior disponible
            for (int i = 1; i < puntos; i++)
            {
                //Se Obtine la posición actual
                posicionActual = new Vector2Int(ruidoX[i], ruidoY[i]);
                //Se obtiene la posición anterior
                posicionAnterior = new Vector2Int(ruidoX[i - 1], ruidoY[i - 1]);
                Vector2 diferencia = posicionActual - posicionAnterior;
                //Se almacena la altura actual
                float cambioEnAltura = diferencia.y / Intervalo;
                //Guardamos la altura actual
                float alturaActual = posicionAnterior.y;
                //Se genera los bloques dentro del intervalo anterior a el actual
                for (int x = posicionAnterior.x; x <= posicionActual.x && x <= Mapa.GetUpperBound(0); x++)
                {
                    //Empezamos desde la altura actual
                    for (int y = Mathf.FloorToInt(alturaActual); y >= 0; y--)
                    {
                        Mapa[x, y] = 1;
                    }
                    alturaActual += cambioEnAltura;
                }
            }
        }
        else
        {
            Mapa = PerlinNoise(Mapa, Semilla);
        }
        return Mapa;
    }
    #endregion
    #region RandomWalk
    /// <summary>
    /// Generación del algoritmo RandomWalk
    /// </summary>
    /// <param name="Mapa">Mapa a Modificar</param>
    /// <param name="Semilla">Semilla que se utiliza</param>
    /// <returns>El mapa con las modificaciones</returns>
    public static int[,] RandomWalk(int[,] Mapa, float Semilla)
    {
        //La semilla que genera e random
        Random.InitState(Semilla.GetHashCode());
        //Se define la altura en la cual se empieza la generación
        int ultimaAltura = Random.Range(0, Mapa.GetUpperBound(1));
        //Se recorre el mapa en el ancho x
        for (int x = 0; x <= Mapa.GetUpperBound(0); x++)
        {
            //0 Sube 1 Baja 2  Igual
            int siguienteMovimiento = Random.Range(0, 3);
            //Subimos
            if (siguienteMovimiento == 0 && ultimaAltura < Mapa.GetUpperBound(1))
            {
                ultimaAltura++;
            }
            //Bajamos
            else if (siguienteMovimiento == 1 && ultimaAltura > 0)
            {
                ultimaAltura--;
            }
            //No se cambia la altura 
            //Se llenan las casillas desde la ultimaAltura
            for (int y = ultimaAltura; y >= 0; y--)
            {
                Mapa[x, y] = 1;
            }
        }
        return Mapa;
    }
    #endregion
    #region RandomWalkSuavizado
    /// <summary>
    /// Modificación a el algoritmo de RandomWalk a modo suavizado
    /// </summary>
    /// <param name="Mapa">El mapa a modificar</param>
    /// <param name="Semilla">La semilla de números aleatorios</param>
    /// <param name="MinimoAnchoSeccion">La minima anchura de la sección antes de cambiar la altura</param>
    /// <returns>El mapa generado</returns>
    public static int[,] RandomWalkSuavizado(int[,] Mapa, float Semilla, int MinimoAnchoSeccion)
    {
        //La semilla que genera e random
        Random.InitState(Semilla.GetHashCode());
        //Se define la altura en la cual se empieza la generación
        int ultimaAltura = Random.Range(0, Mapa.GetUpperBound(1));
        //Tiene la cuenta del ancho de la sección actual
        int anchoSeccion = 0;
        //Se recorre el mapa en el ancho x
        for (int x = 0; x <= Mapa.GetUpperBound(0); x++)
        {
            if (anchoSeccion > MinimoAnchoSeccion)
            {
                //0 Sube 1 Baja 2  Igual
                int siguienteMovimiento = Random.Range(0, 3);
                //Subimos
                if (siguienteMovimiento == 0 && ultimaAltura < Mapa.GetUpperBound(1))
                {
                    ultimaAltura++;
                }
                //Bajamos
                else if (siguienteMovimiento == 1 && ultimaAltura > 0)
                {
                    ultimaAltura--;
                }
                //No se cambia la altura 
                //Se usa una mismo bloque para una misma sección de altura
                anchoSeccion = 0;
            }
            //Se a usado otro bloque de la sección actual
            anchoSeccion++;
            //Se llenan las casillas desde la ultimaAltura
            for (int y = ultimaAltura; y >= 0; y--)
            {
                Mapa[x, y] = 1;
            }
        }
        return Mapa;
    }
    #endregion
    #region PerlinNoiseCuevas
    /// <summary>
    /// Generación de cuevas a partir de la modificación del algoritmo perlinNoise
    /// </summary>
    /// <param name="Mapa">El Mapa A generar</param>
    /// <param name="Modificador">El modificador del algoritmo</param>
    /// <param name="LosBordesSonMuros">Si existen muros en el borde del mapa</param>
    /// <param name="OffSetX">Cambio en el desplazamiento del mpa en el eje x</param>
    /// <param name="OffSetY">Cambio en el desplazamiento del mpa en el eje y</param>
    /// <param name="Semilla">Semilla de generación aleatoria del mapa posicionamiento x,y(X =Y = Semilla) en el PerlinNoise</param>
    /// <returns>Retorna el mapa modificado con la cueva generada</returns>
    public static int[,] PerlinNoiseCuevas(int[,] Mapa, float Modificador, bool LosBordesSonMuros, float OffSetX = 0f, float OffSetY = 0f, float Semilla = 0f)
    {
        int NuevoPunto;
        for (int x = 0; x <= Mapa.GetUpperBound(0); x++)
        {
            for (int y = 0; y <= Mapa.GetUpperBound(1); y++)
            {
                if (LosBordesSonMuros && (x == 0 || y == 0 || x == Mapa.GetUpperBound(0) || y == Mapa.GetUpperBound(1)))
                {
                    Mapa[x, y] = 1;
                }
                else
                {
                    NuevoPunto = Mathf.RoundToInt(Mathf.PerlinNoise(x * Modificador + OffSetX + Semilla, y * Modificador + OffSetY + Semilla));
                    Mapa[x, y] = NuevoPunto;
                }
            }
        }
        return Mapa;
    }
    #endregion
    #region RandomWalkCuevas
    /// <summary>
    /// Creando una cueva con el algoritmo RandomWalk
    /// </summary>
    /// <param name="Mapa">El mapa a modificar</param>
    /// <param name="Semilla">La semilla para números aleatorios</param>
    /// <param name="PorcentajeSueloEliminar">El porcentaje de suelo que se eliminara</param>
    /// <param name="LosBordesSonMuros">Se mantienen los bordes con suelo</param>
    /// <param name="MovimientoEnDiagonal">Se permite la generación en diagonal del mapa</param>
    /// <returns>El mapa modificado</returns>
    public static int[,] RandomWalkCuevas(int[,] Mapa, float Semilla, float PorcentajeSueloEliminar, bool LosBordesSonMuros = true, bool MovimientoEnDiagonal = false)
    {
        //La semilla del random
        Random.InitState(Semilla.GetHashCode());
        //Definición de los limites
        int ValorMinimo = 0;
        int ValorMaximoX = Mapa.GetUpperBound(0);
        int ValorMaximoY = Mapa.GetUpperBound(1);
        int Ancho = Mapa.GetUpperBound(0) + 1;
        int Alto = Mapa.GetUpperBound(1) + 1;
        if (LosBordesSonMuros)
        {
            ValorMinimo++;
            ValorMaximoX--;
            ValorMaximoY--;
            Ancho -= 2;
            Alto -= 2;
        }
        //Definición de las pociciones de inicio en X y Y
        int PosicionX = Random.Range(ValorMinimo, ValorMaximoX);
        int PosicionY = Random.Range(ValorMinimo, ValorMaximoY);
        //Cantidad de Casillas A Eliminar
        int CantidadDeCasillassAEliminar = Mathf.FloorToInt(Ancho * Alto * PorcentajeSueloEliminar);
        //Casillas totales eliminadas
        int CasillasEliminadas = 0;
        while (CasillasEliminadas < CantidadDeCasillassAEliminar)
        {
            if (Mapa[PosicionX, PosicionY] == 1)
            {
                Mapa[PosicionX, PosicionY] = 0;
                CasillasEliminadas++;
            }
            if (MovimientoEnDiagonal)
            {
                //Nos podemos mover en diagonal
                int DireccionAleatoriaX = Random.Range(-1, 2);
                int DireccionAleatoriaY = Random.Range(-1, 2);
                PosicionX += DireccionAleatoriaX;
                PosicionY += DireccionAleatoriaY;
            }
            else
            {
                int DireccionAleatoria = Random.Range(0, 4);
                switch (DireccionAleatoria)
                {
                    case 0:
                        //Arriba
                        PosicionY++;
                        break;
                    case 1:
                        //Abajo
                        PosicionY--;
                        break;
                    case 2:
                        //Izquierda
                        PosicionX--;
                        break;
                    case 3:
                        //Derecha
                        PosicionX++;
                        break;
                }
            }
            //Limites para el área
            PosicionX = Mathf.Clamp(PosicionX, ValorMinimo, ValorMaximoX);
            PosicionY = Mathf.Clamp(PosicionY, ValorMinimo, ValorMaximoY);
        }
        return Mapa;
    }
    #endregion
    #region TunelDireccional
    /// <summary>
    /// Crea el tunel en longitud de alto, crea el ancho de arcuerdo a la aspereza para cambiar dicho ancho
    /// </summary>
    /// <param name="Mapa">El array que modificara el mapa</param>
    /// <param name="Semilla">Semilla de números aleatorios</param>
    /// <param name="AnchoMinimo">Ancho minimo del tunel</param>
    /// <param name="AnchoMaximo">Ancho maximo del tunel</param>
    /// <param name="Aspereza">La probalnilidad de que cambie el ancho en cada paso de Y</param>
    /// <param name="DesplazamientoMaximo">Desvación del punto central del tunel</param>
    /// <param name="Desplazamiento">La probabiliad del cambio del punto central del tunel</param>
    /// <returns>Retorna el mapa modificado</returns>
    public static int[,] TunelDireccional(int[,] Mapa, float Semilla, int AnchoMinimo, int AnchoMaximo, float Aspereza, int DesplazamientoMaximo, float Desplazamiento)
    {
        //El Valor mepiza desde su valor negativo hasta el valor positivio
        int AnchoTunel = 1;
        int x = Mapa.GetUpperBound(0) / 2;
        Random.InitState(Semilla.GetHashCode());
        //Recorremos en y para que el tunel sea vertical.
        for (int y = 0; y <= Mapa.GetUpperBound(1); y++)
        {
            //Generamos dicha parte del tunel
            for (int i = -AnchoTunel; i <= AnchoTunel; i++)
            {
                Mapa[x + i, y] = 0;
            }
            //Se cambia el ancho deacuerdo a la aspereza
            if (Random.value < Aspereza)
            {
                //Se obtiene el cambio en el ancho de manera aleatoria
                int CambioEnAncho = Random.Range(-AnchoMaximo, AnchoMaximo);
                AnchoTunel += CambioEnAncho;
                //E túnel no salga del mapa
                AnchoTunel = Mathf.Clamp(AnchoTunel, AnchoMinimo, AnchoMaximo);
            }
            //Comprobación del cambio del punto central del tunel
            if (Random.value > 1 - Desplazamiento)
            {
                //Elejimos aletoriamnete el desplazamiento dek túnel
                int CambioEnX = Random.Range(-DesplazamientoMaximo, DesplazamientoMaximo);
                x += CambioEnX;
                //El túnel no se salga del mapa
                x = Mathf.Clamp(x, AnchoMaximo + 1, Mapa.GetUpperBound(0) - AnchoMaximo);
            }
        }
        return Mapa;
    }
    #endregion
    #region MapaAleatorio
    /// <summary>
    /// Crea la base para las funciones avanzadas de autómatas celulares
    /// Usaremos este mapa en distintas funciones dependiendo del tipo de vecindario que queramos
    /// </summary>
    /// <param name="Ancho">Ancho del mapa</param>
    /// <param name="Alto">Alto del mapa</param>
    /// <param name="Semilla">La semilla de los números aleatorio</param>
    /// <param name="PorcentajeDeRelleno">La cantidad que queremos que se llene el mapa. Valor entre [0,1]</param>
    /// <param name="LosBordesSonMuros">Si los bordes deben mantenerse</param>
    /// <returns>El mapa con el contenido aleatorio generado</returns>
    public static int[,] GenerarMapaAleatorio(int Ancho, int Alto, float Semilla, float PorcentajeDeRelleno, bool LosBordesSonMuros)
    {
        // La semilla de nuestro random
        Random.InitState(Semilla.GetHashCode());

        // Creamos el array
        int[,] Mapa = new int[Ancho, Alto];

        // Recorremos todas las posiciones del mapa
        for (int x = 0; x <= Mapa.GetUpperBound(0); x++)
        {
            for (int y = 0; y <= Mapa.GetUpperBound(1); y++)
            {
                if (LosBordesSonMuros && (x == 0 || x == Mapa.GetUpperBound(0) || y == 0 || y == Mapa.GetUpperBound(1)))
                {
                    // Ponemos suelo si estamos en una posición del borde
                    Mapa[x, y] = 1;

                }
                else
                {
                    // Ponemos suelo si el resultado del random es inferior que el porcentaje de relleno
                    Mapa[x, y] = (Random.value < PorcentajeDeRelleno) ? 1 : 0;

                }
            }
        }

        return Mapa;
    }
    #endregion
    #region CasillasVecinas
    /// <summary>
    /// Calcula el total de losetas vecinas
    /// </summary>
    /// <param name="mapa">El mapa donde comprobar las losetas</param>
    /// <param name="x">La posición en X de la loseta que estamos comprobando</param>
    /// <param name="y">La posición en Y de la loseta que estamos comprobando</param>
    /// <param name="incluirDiagonales">Si hay que tener en cuenta las posiciones vecinas en diagonal</param>
    /// <returns>El total de losetas vecinas con suelo</returns>
    public static int CantidadCasillasVecinas(int[,] Mapa, int x, int y, bool IncluirDiagonales)
    {
        // Lleva la cuenta de losetas vecinas
        int TotalCasillas = 0;

        // Recorrer todas las posiciones vecinas
        for (int vecinoX = x - 1; vecinoX <= x + 1; vecinoX++)
        {
            for (int vecinoY = y - 1; vecinoY <= y + 1; vecinoY++)
            {
                // Comprobamos que estamos dentro del mapa
                if (vecinoX >= 0 && vecinoX <= Mapa.GetUpperBound(0) && vecinoY >= 0 && vecinoY <= Mapa.GetUpperBound(1))
                {
                    // Comprobamos que no estemos en la misma posición x, y que estamos comprobando
                    // y si incluirDiagonales = false:
                    //
                    //   N
                    // N T N
                    //   N
                    // 
                    // Si incluirDiagonales = true:
                    //
                    // N N N
                    // N T N 
                    // N N N
                    //
                    if ((vecinoX != x || vecinoY != y) && (IncluirDiagonales || (vecinoX == x || vecinoY == y)))
                    {
                        TotalCasillas += Mapa[vecinoX, vecinoY];
                    }
                }
            }
        }

        return TotalCasillas;
    }
    #endregion
    #region AutomataCelularMoore
    /// <summary>
    /// Suaviza un mapa usando las reglas de vecindario de Moore
    /// Se tienen en cuenta todas las losetas vecinas (incluídas las diagonales)
    /// </summary>
    /// <param name="mapa">El mapa a suavizar</param>
    /// <param name="totalDePasadas">La cantidad de pasadas que haremos</param>
    /// <param name="losBordesSonMuros">Si se deben mantener los bordes</param>
    /// <param name="Jugador">Jugadpr en la escena</param>
    /// <param name="Libro">Objeto libro en la escena</param>
    /// <returns>El mapa modificado</returns>
    public static int[,] AutomataCelularMoore(int[,] Mapa, int TotalDePasadas, bool LosBordesSonMuros, GameObject Jugador,
        float XPersonaje, float YPersonaje, GameObject Libro, float XLibro, float YLibro)
    {
        for (int i = 0; i < TotalDePasadas; i++)
        {
            for (int x = 0; x <= Mapa.GetUpperBound(0); x++)
            {
                for (int y = 0; y <= Mapa.GetUpperBound(1); y++)
                {
                    // Obtenemos el total de losetas vecinas (Incluyendo las diagonales) 
                    int CasillasVecinas = CantidadCasillasVecinas(Mapa, x, y, true);

                    // Si estamos en un borde y losBordesSonMuros está activado,
                    // ponemos un muro
                    if (LosBordesSonMuros && (x == 0 || x == Mapa.GetUpperBound(0) || y == 0 || y == Mapa.GetUpperBound(1)))
                    {
                        Mapa[x, y] = 1;
                    }
                    // Si tenemos más de 4 vecinos, ponemos suelo.
                    else if (CasillasVecinas > 4)
                    {
                        Mapa[x, y] = 1;
                    }
                    // Si tenemos menos de 4 vecinos, dejamos un hueco.
                    else if (CasillasVecinas < 4)
                    {
                        Mapa[x, y] = 0;
                        #region Instancia de creación del jugador
                        Jugador = GameObject.FindGameObjectWithTag("Jugador");
                        if (Jugador == null)
                        {
                            //Nothing do here
                        }
                        else
                        {
                            Jugador.transform.position = new Vector3(XPersonaje, YPersonaje, 0);
                        }
                        #endregion
                        #region Instancia de creación del Libro
                        Libro = GameObject.FindGameObjectWithTag("Libro");
                        if (Libro == null)
                        {
                            //Nothing do here
                        }
                        else
                        {
                            Libro.transform.position = new Vector3(XLibro, YLibro, 0);
                        }
                        #endregion
                    }
                    // Si tenemos exactamente 4 vecinos, no cambiamos nada
                }
            }
        }
        return Mapa;
    }
    #endregion
    #region AutomataCelularVonNeumann
    /// <summary>
    /// Suaviza un mapa usando las reglas de vecindario de Von Neumann
    /// No se tienen en cuenta las losetas vecinas en diagonal
    /// </summary>
    /// <param name="mapa">El mapa a suavizar</param>
    /// <param name="totalDePasadas">La cantidad de pasadas que haremos</param>
    /// <param name="losBordesSonMuros">Si se deben mantener los bordes</param>
    /// <param name="Jugador">Jugador en la escena</param>
    /// <param name="Libro">Objeto libro en la escena</param>
    /// <returns>El mapa modificado</returns>
    public static int[,] AutomataCelularVonNeumann(int[,] Mapa, int TotalDePasadas,
        bool LosBordesSonMuros, GameObject Jugador,
        float XPersonaje, float YPersonaje, GameObject Libro, float XLibro, float YLibro)
    {
        for (int i = 0; i < TotalDePasadas; i++)
        {
            for (int x = 0; x <= Mapa.GetUpperBound(0); x++)
            {
                for (int y = 0; y <= Mapa.GetUpperBound(1); y++)
                {
                    // Obtenemos el total de losetas vecinas (No incluye las diagonales) 
                    int CasillasVecinas = CantidadCasillasVecinas(Mapa, x, y, false);

                    // Si estamos en un borde y losBordesSonMuros está activado,
                    // ponemos un muro
                    if (LosBordesSonMuros && (x == 0 || x == Mapa.GetUpperBound(0) || y == 0 || y == Mapa.GetUpperBound(1)))
                    {
                        Mapa[x, y] = 1;
                    }
                    // Si tenemos más de 2 vecinos, ponemos suelo.
                    else if (CasillasVecinas > 2)
                    {
                        Mapa[x, y] = 1;
                    }
                    // Si tenemos menos de 2 vecinos, dejamos un hueco.
                    else if (CasillasVecinas < 2)
                    {
                        Mapa[x, y] = 0;
                        #region Instancia de creación del jugador
                        Jugador = GameObject.FindGameObjectWithTag("Jugador");
                        if (Jugador == null)
                        {
                            //Nothing do here
                        }
                        else
                        {
                            Jugador.transform.position = new Vector3(XPersonaje, YPersonaje, 0);
                        }
                        #endregion
                        #region Instancia de creación del Libro
                        Libro = GameObject.FindGameObjectWithTag("Libro");
                        if (Libro == null)
                        {
                            //Nothing do here
                        }
                        else
                        {
                            Libro.transform.position = new Vector3(XLibro, YLibro, 0);
                        }
                        #endregion

                    }
                    // Si tenemos exactamente 2 vecinos, no cambiamos nada
                }
            }
        }
        return Mapa;
    }

    #endregion
}
