# Exterior abierto y pozos

Alcance autorizado: quitar la caja física exterior y permitir caídas mortales al vacío, manteniendo cámaras, escaladas y el recorrido del diario.

Se conserva una capa local de roca alrededor de las cámaras y todos los soportes calculados por el generador. Se elimina el relleno alejado de los espacios transitables, incluidas las filas/columnas de borde. Los pozos mortales se prolongan hacia abajo hasta el exterior o hasta encontrar soporte de otra ruta protegida. Sus avisos y detección mortal existentes permanecen; la detección inferior ya maneja caídas fuera del mapa sin collider de frontera.

Esto mantiene el generador y los colliders actuales. Vaciar solo la última fila/columna dejaría la pared interior de relleno; sustituir el terreno por plataformas abiertas cambiaría todas las cámaras. No se añaden dependencias ni se modifica Player.

Verificación: prueba de bordes/pozos y colección completa; regresión de topologías; reproducción física representativa y caída real a GameOver. Retirar las sondas temporales con copia y meta verificadas.
