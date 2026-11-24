# Ampliación visual del personaje

Se conservan PNG y GUID originales, parámetros y estados del Animator, bindings al SpriteRenderer y geometría física. Hay respaldo verificado de Player, controlador y los tres clips actuales.

La opción seleccionada amplía clips mediante las APIs de AnimationUtility y añade una capa de poses al controlador existente. Una nueva máquina de animación por código duplicaría funciones del Animator; reemplazar todo el controlador dañaría sus referencias. La capa adicional es reversible y solo necesita parámetros visuales derivados de los estados físicos ya existentes.

Las tres hojas generadas se importan en una carpeta hermana. Se registran rectángulos, pivotes, nombres y contornos mediante Sprite Editor Data Provider, comprobando capacidades. Los contornos excluyen partículas sueltas de la malla sin alterar los archivos originales. No se instala Pipeline ni se cambian paquetes: se reutiliza el Editor ya accesible y sus sondas temporales. Documentación leída: el DataProvider.md del paquete 2D Sprite 1.0 instalado y api_reference.md/templates.md de la guía oficial sprite-editor.

Reposo/caminar/correr/salto incorporan fotogramas nuevos; se añaden agacharse, gatear, trepar, colgarse y subir al saliente. Se conserva la duración de los ciclos antiguos. La integración de Player afecta únicamente parámetros del Animator y presentación de agachado; ninguna operación física cambia.

Validación: comprobar las capacidades antes de aplicar; revisar sprites importados y recuentos/clips/referencias; compilar y verificar selección de poses y estabilidad de colliders en Play Mode. Retirar sondas al finalizar. Si la calidad visual de una hoja no es aceptable, conservarla como variante sin sustituir las originales.
