# Alcance y procedencia del checkpoint

Grafo semántico de UN documento de contexto, extraído por el host actual. No se analizaron Assets, C#, imágenes, caches ni todo el repositorio. Las relaciones EXTRACTED reflejan decisiones explícitas; referencias no son llamadas de código. Los estados distinguen implementado, aprobado e ideas pendientes. No se implementó parallax, obstáculos ni personaje anime en esta actualización.

Coste adicional de API/otros modelos: 0 llamadas. Los contadores 0 del informe estándar se refieren a esas llamadas adicionales; el consumo del host no está disponible y no se afirma que sea cero.

# Graph Report - source  (2026-11-23)

## Corpus Check
- Corpus is ~1,016 words - fits in a single context window. You may not need a graph.

## Summary
- 19 nodes · 24 edges · 4 communities (0 shown, 4 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## God Nodes (most connected - your core abstractions)
1. `Materiales de tiles aceptados` - 6 edges
2. `Objetos domésticos: integración por perfeccionar` - 6 edges
3. `Pausa y afinación de la idea por el usuario` - 5 edges
4. `Propuesta visual aprobada: referencia principal` - 4 edges
5. `Parallax de casa y ruinas: pendiente` - 4 edges
6. `Primera integración modular verificada` - 4 edges
7. `JustMe: casa y mundo interior` - 3 edges
8. `Muebles como mini-obstáculos seleccionados` - 2 edges
9. `Preservar física y mundo procedural` - 2 edges
10. `Personaje anime: pendiente después del entorno` - 2 edges

## Surprising Connections (you probably didn't know these)
- `JustMe: casa y mundo interior` --references--> `Propuesta visual aprobada: referencia principal`  [EXTRACTED]
  VISUAL_CHECKPOINT.md → VISUAL_CHECKPOINT.md  _Bridges community 2 → community 0_
- `Propuesta visual aprobada: referencia principal` --references--> `Materiales de tiles aceptados`  [EXTRACTED]
  VISUAL_CHECKPOINT.md → VISUAL_CHECKPOINT.md  _Bridges community 0 → community 1_
- `Primera integración modular verificada` --references--> `Materiales de tiles aceptados`  [EXTRACTED]
  VISUAL_CHECKPOINT.md → VISUAL_CHECKPOINT.md  _Bridges community 1 → community 3_
- `Primera integración modular verificada` --references--> `Objetos domésticos: integración por perfeccionar`  [EXTRACTED]
  VISUAL_CHECKPOINT.md → VISUAL_CHECKPOINT.md  _Bridges community 2 → community 3_
- `Pausa y afinación de la idea por el usuario` --references--> `Primera integración modular verificada`  [EXTRACTED]
  VISUAL_CHECKPOINT.md → VISUAL_CHECKPOINT.md  _Bridges community 3 → community 0_

## Communities (4 total, 4 thin omitted)

## Knowledge Gaps
- **4 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Materiales de tiles aceptados` connect `Materiales aceptados y terreno` to `Profundidad y referencia visual`, `Pendientes y continuidad del proyecto`?**
  _High betweenness centrality (0.418) - this node is a cross-community bridge._
- **Why does `Objetos domésticos: integración por perfeccionar` connect `Objetos y función en la casa` to `Profundidad y referencia visual`, `Pendientes y continuidad del proyecto`?**
  _High betweenness centrality (0.271) - this node is a cross-community bridge._
- **Why does `Primera integración modular verificada` connect `Pendientes y continuidad del proyecto` to `Profundidad y referencia visual`, `Materiales aceptados y terreno`, `Objetos y función en la casa`?**
  _High betweenness centrality (0.271) - this node is a cross-community bridge._