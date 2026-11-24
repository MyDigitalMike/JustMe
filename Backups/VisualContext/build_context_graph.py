"""Build only the explicitly requested design-memory graph with installed Graphify.

Semantic statements below were extracted by the current host from the checkpoint.
This does not extract C#, open an image, call another model, or scan the project.
"""
import hashlib
import json
from pathlib import Path

from graphify.analyze import god_nodes, surprising_connections, suggest_questions
from graphify.build import build_from_json
from graphify.cluster import cluster, score_all
from graphify.detect import detect, save_manifest
from graphify.export import to_json, to_html
from graphify.report import generate

project = Path(__file__).resolve().parents[2]
root = project / "docs/context/source"
document = root / "VISUAL_CHECKPOINT.md"
out = project / "docs/context/graphify-out"
out.mkdir(parents=True, exist_ok=True)
detection = detect(root, gitignore=False, cache_root=out)
files = [Path(p).resolve() for values in detection["files"].values() for p in values]
assert files == [document.resolve()], "Scope must remain exactly one checkpoint document."
text = document.read_text(encoding="utf-8")
source = document.as_posix()
prefix = "docs_context_source_visual_checkpoint_"

# id, label, exact source section, status, extracted meaning / rationale
entries = [
    ("justme", "JustMe: casa y mundo interior", "Referencia visual aprobada", "dirección aceptada", "Casa descuidada y fragmentada que sugiere pérdida de identidad mediante espacio, objetos y diario, sin exposición directa."),
    ("concept", "Propuesta visual aprobada: referencia principal", "Referencia visual aprobada", "referencia aprobada", "Backups/HouseRuins/approved-concept.png es el objetivo; la captura provisional del juego no lo sustituye."),
    ("materials", "Materiales de tiles aceptados", "Materiales aceptados para los tiles", "aceptados", "Conservar materiales coherentes; Assets/Art/HouseRuins/HouseMaterials.png y Resources/RuinedHouseTheme.asset."),
    ("stone", "Piedra envejecida", "Materiales aceptados para los tiles", "aceptado para tile", "Piedra del concepto aprobada expresamente como terreno."),
    ("timber", "Madera envejecida", "Materiales aceptados para los tiles", "aceptado para tile", "Madera del concepto aprobada expresamente como terreno."),
    ("wallpaper", "Papel tapiz", "Materiales aceptados para los tiles", "aceptado para tile", "Papel floral envejecido aprobado expresamente."),
    ("peeled", "Papel tapiz rasgado y yeso", "Materiales aceptados para los tiles", "aceptado para tile", "Papel desprendido y yeso/exposición del muro aprobados como material."),
    ("props", "Objetos domésticos: integración por perfeccionar", "Muebles y objetos de la casa — integración pendiente", "pendiente", "Los sprites gustan; su colocación actual como tiles resulta extraña. Conservarlos como objetos separados, sin cambios hoy."),
    ("scenery", "Objetos como entorno narrativo", "Muebles y objetos de la casa — integración pendiente", "idea futura", "Componer objetos deliberadamente como parte del escenario; evitar repetición como textura."),
    ("obstacles", "Muebles como mini-obstáculos seleccionados", "Muebles y objetos de la casa — integración pendiente", "idea futura, sin colliders nuevos", "Decidir función y dimensiones antes de añadir física; preservar recorridos y movimientos calibrados."),
    ("parallax", "Parallax de casa y ruinas: pendiente", "Parallax de la casa en ruinas — prioridad futura", "prioridad futura", "Varios fondos a diferentes velocidades para aproximarse al concepto. No implementar ahora."),
    ("layers", "Fondos lejanos, medios y cercanos", "Parallax de la casa en ruinas — prioridad futura", "composición por definir", "Separar profundidad visual de terreno físico. Cantidad, factores y arte definitivos no decididos."),
    ("darkness", "Parallax y oscuridad local: decisión abierta", "Parallax de la casa en ruinas — prioridad futura", "por decidir", "Conciliar profundidad visible y oscuridad. Conservar F8 y comportamiento actual mientras se decide."),
    ("implementation", "Primera integración modular verificada", "Estado implementado y verificado", "implementado, provisional", "64 tiles y ocho sprites; piel sin colliders sobre el RuleTile original. Unity 6000.6.8f1, tres semillas y niveles 1/2 verificados; detalles en Backups/HouseRuins/verification.md."),
    ("physics", "Preservar física y mundo procedural", "Estado implementado y verificado", "restricción vigente", "No alterar Player, generador, colisiones, escenas ni GUID para disimular problemas visuales. Los futuros obstáculos requieren validación."),
    ("anime", "Personaje anime: pendiente después del entorno", "Personaje anime — pendiente posterior", "pendiente", "Referencia anime del concepto, sin rediseño realizado. Preparar un set coherente; no repetir titileo por intercalar familias ni encogimiento del agachado."),
    ("diary", "Diario personal y relato ambiental", "Diario y relato — pendientes", "ideas narrativas futuras", "Diario gastado, nombre borrado y páginas encajables; progresión actual 3, +1/nivel, máximo 12. Narrativa final no implementada."),
    ("deferred", "Pausa y afinación de la idea por el usuario", "Cómo retomar con poco contexto", "detener implementación", "Guardar lo realizado. Al retomar afinar parallax y función de muebles; hoy solo persistir contexto."),
    ("selection", "Herramientas según necesidad y poco contexto", "Cómo retomar con poco contexto", "estrategia vigente", "Archivos conocidos: directo; símbolos: Serena; importación/compilación: Unity; Graphify para contexto/dependencias significativas. Sin nuevas auditorías o índices amplios."),
]
nodes = []
for key, label, heading, status, summary in entries:
    assert ("## " + heading) in text, heading
    nodes.append(dict(id=prefix+key, label=label, file_type="rationale", source_file=source,
                      source_location="## "+heading, status=status, rationale=summary,
                      _origin="semantic", captured_at="2026-11-23", author="Usuario de JustMe"))

pairs = [
    ("justme","concept"), ("concept","materials"), ("materials","stone"),
    ("materials","timber"), ("materials","wallpaper"), ("materials","peeled"),
    ("justme","props"), ("props","scenery"), ("props","obstacles"),
    ("obstacles","physics"), ("concept","parallax"), ("parallax","layers"),
    ("parallax","darkness"), ("implementation","materials"),
    ("implementation","props"), ("implementation","physics"),
    ("concept","anime"), ("justme","diary"), ("diary","props"),
    ("deferred","implementation"), ("deferred","parallax"),
    ("deferred","props"), ("deferred","anime"), ("deferred","selection"),
]
lookup = {n["id"]: n for n in nodes}
edges = [dict(source=prefix+a,target=prefix+b,relation="references",confidence="EXTRACTED",
              confidence_score=1.0,weight=1.0,source_file=source,
              source_location=lookup[prefix+a]["source_location"],_origin="semantic") for a,b in pairs]
extraction = dict(nodes=nodes,edges=edges,hyperedges=[],input_tokens=0,output_tokens=0,
                  usage_note="No additional model/API requests. Current host semantic extraction; host token usage unavailable, not claimed to be zero.")
(out / "semantic-extraction.json").write_text(json.dumps(extraction,ensure_ascii=False,indent=2),encoding="utf-8")
graph = build_from_json(extraction, root=root, directed=False)
assert graph.number_of_nodes() == len(nodes)
assert graph.number_of_edges() == len(edges), "No edge may collapse or dangle."
communities = cluster(graph)
cohesion = score_all(graph, communities)
labels = {}
for group, members in communities.items():
    names = set(members)
    if prefix+"materials" in names:
        label = "Materiales aceptados y terreno"
    elif prefix+"parallax" in names:
        label = "Profundidad y referencia visual"
    elif prefix+"props" in names:
        label = "Objetos y función en la casa"
    else:
        label = "Pendientes y continuidad del proyecto"
    labels[group] = label
assert to_json(graph, communities, str(out/"graph.json"),community_labels=labels)
assert to_html(graph, communities, str(out/"graph.html"),community_labels=labels)
gods = god_nodes(graph)
surprises = surprising_connections(graph, communities)
questions = suggest_questions(graph,communities,labels)
report = generate(graph,communities,cohesion,labels,gods,surprises,detection,
                  {"input":0,"output":0},str(root),suggested_questions=questions)
notice = ("# Alcance y procedencia del checkpoint\n\n"
          "Grafo semántico de UN documento de contexto, extraído por el host actual. "
          "No se analizaron Assets, C#, imágenes, caches ni todo el repositorio. "
          "Las relaciones EXTRACTED reflejan decisiones explícitas; referencias no son llamadas de código. "
          "Los estados distinguen implementado, aprobado e ideas pendientes. "
          "No se implementó parallax, obstáculos ni personaje anime en esta actualización.\n\n"
          "Coste adicional de API/otros modelos: 0 llamadas. Los contadores 0 del informe estándar "
          "se refieren a esas llamadas adicionales; el consumo del host no está disponible y no se afirma que sea cero.\n\n")
(out/"GRAPH_REPORT.md").write_text(notice+report,encoding="utf-8")
(out/".graphify_labels.json").write_text(json.dumps(labels,ensure_ascii=False,indent=2),encoding="utf-8")
save_manifest(detection["files"],manifest_path=str(out/"manifest.json"),kind="semantic",root=root,
              scan_corpus={str(document.resolve())})
integrity = dict(source_file=source,source_sha256=hashlib.sha256(document.read_bytes()).hexdigest(),
                 source_files=1,nodes=graph.number_of_nodes(),edges=graph.number_of_edges(),
                 communities=len(communities),dangling_edges=0,collapsed_edges=0,
                 scope="design checkpoint only",extra_model_calls=0,host_tokens="unavailable")
(out/"verification.json").write_text(json.dumps(integrity,indent=2),encoding="utf-8")
print(json.dumps(integrity,ensure_ascii=False))
