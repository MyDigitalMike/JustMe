# JustMe — Migración gradual propuesta

1. Unificar el estado de suelo. `Player.FixedUpdate` y `CheckGround` escriben actualmente `Grounded` desde mecanismos distintos. Mantener el campo público y sus referencias, pero elegir una única autoridad basada en contactos de soporte. Probar bordes, pendientes y aterrizajes antes de cambiarlo.

2. Separar muestreo de entrada y física. `Player` mezcla entrada en `FixedUpdate`, impulso de salto en `Update` y dos ramas de plataforma duplicadas. La rama móvil contiene `moveInput` sin declarar y APIs antiguas. Conservar los controles existentes y el doble salto actual. Corregir esa rama con una prueba específica de teclado y controles táctiles, sin cambiar de sistema de entrada.

3. Separar algoritmos y posicionamiento. Los autómatas de `Metodos` buscan al jugador y al libro y reposicionan sus transformaciones dentro del bucle de cada celda vacía. Mantener inicialmente el resultado de la matriz y las firmas públicas. Extraer después el posicionamiento para ejecutarlo una vez por generación. `GenerarMapaAleatorio` también modifica el estado global de `UnityEngine.Random`, por lo que conviene aislar o restaurar ese estado en una revisión separada.

4. Reducir responsabilidades de `Libro`. Hoy reúne generación, creación de enemigos y objetos, temporizador, UI, cambios de escena y persistencia. Conservarlo inicialmente como orquestador y extraer una responsabilidad por cambio. El temporizador y la presentación de contadores son los primeros candidatos. No hace falta añadir un framework ni un contenedor de dependencias.

5. Revisar respuestas del jugador. `EnemyKnocBack` establece `Movement = false`, pero el movimiento horizontal no consulta ese valor. `OnBecameInvisible` teletransporta al personaje a una posición fija. Evaluar estos comportamientos por separado para distinguir retroceso, reaparición y problemas visuales.

6. Mantener el formato de guardado actual durante la limpieza de integraciones. `SaveSystem` utiliza `Application.persistentDataPath/book.bk`. La modernización de `BinaryFormatter` requiere un plan específico de compatibilidad con partidas existentes.

## Estructura de carpetas propuesta para una fase posterior

- `Assets/Script/Gameplay/Player`, `Enemies` e `Interactions` para comportamiento del juego.
- `Assets/Script/Generation` para algoritmos y adaptación al Tilemap.
- `Assets/Script/UI` y `Assets/Script/Persistence` para presentación y guardado.
- `Assets/Script/Editor` para herramientas del Editor, conservando la separación especial de Unity.

Mover cada grupo a través del Editor, con sus `.meta`, en lotes pequeños y con una comprobación de referencias por lote. Evitar renombrar clases y campos serializados en el mismo cambio que mueve archivos. Comprobar también recursos cargados por ruta antes de mover fonts, prefabs o carpetas `Resources`. La fase actual no realiza esta reorganización.

## Siguiente mejora de respuesta recomendada

Medir la diferencia entre la velocidad horizontal configurada y la efectiva al caminar. En la prueba aislada, el jugador configurado a velocidad 1 recorrió aproximadamente 6,08 unidades en ocho segundos. Revisar el material de los pies y la fricción antes de aumentar la velocidad. Un material de menor fricción puede mejorar la uniformidad y reducir el agarre contra paredes, pero también cambia la resistencia al deslizamiento. No se ha aplicado ese ajuste.
