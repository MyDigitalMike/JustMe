# Variedad espacial: ampliación aprobada por el usuario

La corrección parcial separó techo y salto, pero conservó seis esqueletos y bancos de escalones largos. El alcance confirmado incluye todo el mundo: cámaras regulares e irregulares, conectores de longitud desigual, cambios de altura intermedios y caminos falsos con regreso.

Se conserva el grafo dirigido y sus acciones medidas. Añadir un algoritmo externo o tallar ruido libre perdería la garantía de recorrido; barajar únicamente obstáculos ya resultó insuficiente. Se amplía el planificador existente con una gramática de secciones y cámaras, sin dependencias ni cambios al personaje.

1. Prueba de regresión: interrumpir bancos verticales con cámaras; ramales variables fuera del inicio; aceptar ambas formas de cámara.
2. Generar secciones de exploración para Random; mantener los valores serializados de los perfiles existentes.
3. Generalizar la página del ramal y su itinerario de regreso, conservando una sola historia de stamina.
4. Validar modelo, geometría rasterizada y recorridos físicos representativos en el Editor instalado; revisar visualmente los mapas.
5. Retirar la instrumentación temporal y actualizar el checkpoint con resultados observados.

Límites: no se alteran Player, stamina, escenas, prefabs, paquetes, guardado ni iluminación de prueba. El terreno conserva dimensiones acotadas y rechaza recorridos inválidos.

El límite de almacenamiento pasa de 384 a 512 celdas por eje para alojar cámaras intermedias más anchas sin forzar su recorte; margen vertical de 22 celdas para sus techos. Se conserva un límite finito y rechazo de candidatos inválidos.


