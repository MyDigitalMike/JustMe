# Casa en ruinas — integración de tiles, 2026-11-23

El usuario aprobó el concepto de casa cotidiana que se deshace hacia cavernas y vacío, y pidió integrar primero los tiles. El rediseño anime del personaje queda como siguiente trabajo. Esta entrega integra el arte modular en la generación existente; no reproduce de manera literal la composición pintada del concepto.

## Cambios

- Assets/Art/HouseRuins/HouseMaterials.png: hoja original de piedra, madera, papel floral y yeso. 64 sprites/materiales y 64 Tile assets, ninguno con collider.
- Assets/Art/HouseRuins/HouseProps.png: ocho sprites domésticos; se utilizan cama, puerta tapiada, ventana, marco vacío, rincón con lámpara/sillón/planta, estante e hiedra. Las ménsulas quedan disponibles sin colocarse como plataformas falsas.
- Assets/Resources/RuinedHouseTheme.asset y Assets/Script/GeneracionMundo/RuinedHouseTheme.cs: configuración y selección determinista por semilla de materiales; permite volver al aspecto anterior desactivando applyToGeneratedWorlds.
- Assets/Script/GeneracionMundo/RuinedHousePresentation.cs: piel visual del terreno, paredes de fondo y muebles sin física. Habitaciones de alturas distintas sobre suelos comprobados, solapamiento evitado y máximo 36 habitaciones. La decoración anterior se desactiva inmediatamente al reconstruir. El origen usa el anclaje real del Tilemap.
- Assets/Script/GeneracionMundo/WorldTilemapRenderer.cs: activa la presentación después de aplicar el terreno físico original. La escritura del RuleTile SueloAvanzado y su generación de colliders son las mismas que antes.
- Assets/Script/Game/WorldLighting.cs: color del vacío azul muy oscuro. F8 y la iluminación local conservan su comportamiento.

Las imágenes se generaron con la herramienta integrada ImageGen usando el concepto aprobado como referencia; prompts completos en art-prompts.md. Sprite Editor Data Provider realizó el recorte, pivotes y contornos de render tras verificar capacidades. Los píxeles PNG no se editaron por código. No se instalaron paquetes, Pipeline, CLI ni dependencias. No se modificaron escenas, prefabs, Player, generador de geometría, diario ni recursos originales.

## Evidencia observada

Unity 6000.6.8f1 compiló y entró en Play Mode. native-regression.json compara semillas 2, 11 y 42 con doce trozos: 1909/2233/2419 celdas sólidas, 21/22/23 habitaciones y 40/38/44 objetos. Las baldosas físicas son idénticas al bucle original, al igual que todos los puntos de los 9/11/7 contornos CompositeCollider2D. La piel coincide con las celdas y posiciones del terreno; los objetos no tienen colliders. Geometría y posiciones de fragmentos permanecen intactas; materiales, paredes y muebles se repiten de forma determinista para la misma semilla. La reconstrucción retira el arte anterior.

La primera inspección de Game view reveló muebles elevados: el Tilemap existente usa tileAnchor=(0,0,0), no el centro supuesto. La prueba furniture-alignment-red.json midió 0,24u de separación. La colocación se corrigió usando GetCellCenterLocal; la prueba final no registra separación en ninguna de las tres semillas y Game view confirmó la cama apoyada.

live-play.json y live-next-level.json registran la escena real: nivel 1, semilla -1092877195, tres trozos y 44 objetos; nivel 2, semilla -341995132, cuatro trozos y 48 objetos. Se verificó un único conjunto activo, ningún collider decorativo y retirada del conjunto anterior. El segundo nivel se generó directamente para probar reconstrucción; esto no es un recorrido físico completo ni una prueba nueva de recogida/Goal. F8 se probó en ambos sentidos; live-dark.json y live-full-visibility.json registran el estado previo a la última corrección de anclaje. Los objetos están fuera de la jerarquía que WorldLighting oscurece como fondo. La consola observada mostró cero errores y el aviso UDP de VS/Unity ya existente.

gameplay.png es una captura real del nivel 2 en Unity, no el concepto generado. gameplay-level1.png conserva la captura del nivel anterior. La herramienta temporal se archivó con su .meta y hashes únicamente después de observar la salida de Play Mode. existing-files-verification.json comprueba que los únicos archivos preexistentes cambiados fueron WorldTilemapRenderer.cs y WorldLighting.cs; escenas, prefabs, Player, topología y .meta originales coinciden con el respaldo.

## Límites y uso

Entrar en Play Mode aplica la nueva presentación automáticamente; la escena estática de edición conserva sus assets anteriores. F8 sigue dejando disponible la luz completa de pruebas. El mobiliario está en segundo plano y no es una plataforma ni un objeto interactivo. Esta es una primera integración modular: quedan por afinar composición artística, transiciones/seams de materiales, fondos lejanos y riqueza de habitaciones con prueba manual. No se cambiaron los sprites del personaje en esta entrega.

Referencias oficiales utilizadas: skills Unity sprite-editor/create-tile-palette-or-rule-tiles, su api_reference.md y templates.md, y Documentation~/DataProvider.md del paquete de sprites instalado. No fue necesario añadir RuleTiles/extras: el terreno conserva su RuleTile local ya existente. Revisión independiente focalizada de los cuatro scripts sin bloqueos; sus puntos de integración de anclaje e iluminación se verificaron nativamente.
