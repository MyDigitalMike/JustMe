# Contexto persistente de JustMe

Punto de entrada: [checkpoint visual](source/VISUAL_CHECKPOINT.md). Incluye decisiones del usuario, límites de lo implementado y trabajo pendiente. La imagen aprobada es Backups/HouseRuins/approved-concept.png; las capturas jugables son provisionales.

Graphify: [grafo](graphify-out/graph.json), [informe](graphify-out/GRAPH_REPORT.md) y [vista](graphify-out/graph.html). Corpus limitado a source/VISUAL_CHECKPOINT.md; no se indexó Assets, Library, C# ni imágenes. Nodos y relaciones son extracción semántica de decisiones explícitas, no descubrimiento de llamadas de código. El documento tiene prioridad si el grafo queda desactualizado; comprobar manifest antes de usarlo.

Para consultar con Graphify utilizar --graph docs/context/graphify-out/graph.json. Actualizar solo este corpus cuando cambie la nota; evitar graphify update . porque escanearía otro alcance y no es necesario para recordar el diseño. No hay hooks, servicios, instalación nueva ni cambios a AGENTS.md.
