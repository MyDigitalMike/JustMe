# Corrección del aviso de Input Manager — propuesta lista para aprobación

Estado: diagnóstico y respaldo terminados; no se instalaron paquetes ni se cambiaron entradas, escenas o ajustes. AGENTS.md exige aprobación explícita antes de cambiar el pipeline de entrada.

## Causa confirmada

Proyecto Unity 6000.6.8f1; activeInputHandler=0 y com.unity.inputsystem ausente de Packages/manifest.json. Player utiliza CrossPlatformInputManager y lecturas UnityEngine.Input; PauseMenu, WorldLighting (F8), Creditos, Loader y AspectUtility también usan la API antigua. El backend StandaloneInput de Standard Assets llama a Input.GetAxis/GetButton y mousePosition. TouchPad/TiltInput conservados usan touch/acceleration. MobileControlRig puede crear un StandaloneInputModule. Cinco escenas contienen el módulo antiguo: MainMenu, JustMe, GameOver, Creditos y JustMeLevelCreator.

Los ejes de teclado actuales tienen gravity/sensitivity=3 y dead=0.001. No sustituirlos por entradas digitales instantáneas sin preservar y comprobar su respuesta. Conservar también las variantes de mando declaradas en InputManager.asset. W ya participa en vertical y activa salto; no cambiar esa asignación accidentalmente.

## Alternativas

1. Mantener Input Manager: conserva comportamiento pero mantiene el aviso; no resuelve la deprecación.
2. Both: permite una transición comprobable entre sistemas. Es una etapa temporal; mantenerlo no completa la retirada del backend antiguo.
3. Migrar lectores y UI, y terminar en Input System Package (New): solución recomendada. Requiere paquete oficial, cambios enfocados en lectores y módulos de UI, Active Input Handling y reinicio normal del Editor. No cambia movimiento, física ni arte.

## Alcance aprobado que se solicita

- Instalar únicamente com.unity.inputsystem mediante Package Manager, con versión compatible ofrecida para el Editor actual; comprobar versión y sus docs antes de codificar. No actualizar otros paquetes ni importar ejemplos, Pipeline o plugins.
- Añadir una capa pequeña de lectura/Input Actions dentro de Assets/Script/Input. Preservar bindings, respuesta/suavizado de ejes y puente de controles táctiles; no sustituir Player ni su integración Update/FixedUpdate.
- Adaptar lectores de Player.cs, PauseMenu.cs, WorldLighting.cs, Creditos.cs, Loader.cs y AspectUtility.cs. Ajustar solo las llamadas heredadas que puedan ejecutarse en StandaloneInput, TouchPad, TiltInput y MobileControlRig, conservando sus clases, campos serializados, APIs y licencias. No eliminar recursos heredados.
- Sustituir StandaloneInputModule por InputSystemUIInputModule mediante Editor en las cinco escenas inventariadas, preservando EventSystem, botones, callbacks, navegación y GUIDs. Verificar qué escenas están en Build Settings; conservar también la de creación de niveles.
- Usar Both solamente si facilita la validación intermedia y terminar en New only. Conservar InputManager.asset como recuperación; no borrar ejes ni regenerar metas.
- Respaldar/conservar cualquier cambio abierto antes del reinicio requerido. No matar procesos, cerrar sin guardar, tocar ACL ni instalar un segundo Editor.

## Respaldo preparado

Originals contiene 34 archivos verificados por SHA-256: configuración de entrada y paquetes, scripts afectados, cinco escenas y los metadatos existentes correspondientes. Inventario: before.json. Este respaldo refleja los archivos en disco; el estado no guardado del Editor debe preservarse antes de reiniciarlo.

## Verificación tras aprobar

1. Recompilar en Unity 6000.6.8f1. Comprobar paquete/version resuelta y backend activo real después del reinicio, y observar si desapareció el aviso original; no darlo por corregido por cambiar un valor en un archivo.
2. Pruebas nativas de entradas equivalentes, incluyendo suavizado, pulsación mantenida/bordes de salto, liberación y dispositivos ausentes. Mantener los parámetros físicos y métodos de simulación idénticos.
3. Play Mode: A/D/flechas, Space/W y doble salto, Shift, C/Ctrl, escalada/agarre/liberación, Escape, F8 también en pausa. Probar foco perdido/reanudado sin teclas pegadas. Verificar los controles de mando/táctiles conservados con dispositivos disponibles o eventos simulados; distinguir simulación de prueba en dispositivo real.
4. Menús MainMenu, Pause, GameOver y Creditos con ratón/teclado; comprobar módulos sin duplicación de eventos. Verificar JustMeLevelCreator sin cambiar sus funciones.
5. Regresión física de recorrido representativo una vez, cuando corresponda; conservar generación, diario, tiles y Animator. Retirar herramientas temporales después de salir de Play y registrar resultados y cambios exactos.

## Referencias consultadas

- Unity Input System, Installation: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.17/manual/Installation.html — Both/New, defines y reinicio. Documentación de migración consultada, no versión elegida para instalar.
- UI support: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.17/manual/UISupport.html — sustitución del módulo uGUI.
- Context7 /unity-technologies/inputsystem, fuentes oficiales del repositorio: Installation.md, understand-ui-compatibility.md, corresponding-old-new-api.md. Confirmar docs de la versión efectivamente resuelta al implementar.

Aprobación recibida: el usuario respondió Sí a la migración, instalación oficial, adaptación de controles/UI y reinicio con respaldo. Paquete resuelto por UPM: com.unity.inputsystem 1.20.1. Las cinco escenas se migraron con Editor; se conserva InputManager.asset como configuración original y fuente de bindings. El único asmdef nuevo aísla el lector compartido para que Standard Assets (firstpass) pueda referenciarlo sin reestructurar las asambleas existentes.
