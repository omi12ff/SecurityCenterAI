# Prioridades del dashboard e historial compacto

Implementado localmente el 2026-10-24 para la primera versión.

## Consultar el último informe

En **Resumen**, los puntos y la cobertura conservan los valores guardados. Debajo,
**Por dónde empezar** separa dos grupos del último informe del agente:

- **Para revisar:** hasta tres comprobaciones con estado `attention`, ordenadas
  por severidad del catálogo y, en empates, por regla e identificador.
- **Sin determinar:** hasta tres con estado `unknown`, ordenadas por regla e
  identificador. No se ordenan o presentan como riesgos por su severidad.

Cada enlace abre la guía del análisis en la comprobación elegida. Si hay más de
tres, se muestra el total y un acceso al informe completo. Los criterios correctos
o no aplicables no se convierten en prioridades. Un informe sin criterios
aplicables indica que no permite evaluar el equipo; diez desconocidos conservan
puntaje nulo. No se recalculan puntos, cobertura, severidades o evidencia.

Se muestra equipo y fecha del informe. Cuando la lista de dispositivos confirma
que está revocado, se indica su carácter histórico. El nombre es el actual del
equipo; no se incorpora una segunda instantánea de nombres. **Actualizar resumen**
vuelve a consultar el servidor. El detalle de puntos/cobertura/conteos queda en
un bloque desplegable para mantener visibles las prioridades.

Los registros anteriores al agente conservan su puntaje, sin inventar estados,
cobertura o prioridades. Si falla la lectura adicional del detalle, se mantiene
el resumen con un aviso de indisponibilidad: no se interpreta como cero problemas.
Una sesión rechazada por la API sigue el flujo de renovación.

## Recorrer el historial

La tabla de Resumen muestra hasta cinco registros del usuario: equipo o registro
anterior, fecha guardada, puntos, cobertura y cantidades para revisar/sin
determinar. Distingue equipos revocados y enlaza directamente a cada guía.
Indica cuántos se muestran respecto al total, sin presentarlos como todo el historial.

**Ver historial por equipo** lleva a Dispositivos. Cada equipo conserva su límite
actual de veinte informes y cien hallazgos por informe. El más reciente empieza
desplegado y los anteriores se abren por fecha con el control nativo `details`.
Los enlaces `#analysis-<id>` y cambios de hash abren el informe correspondiente;
comparación, exportación y borrado conservan sus rutas y controles existentes.
Para obtener todo el contenido guardado sigue disponible la exportación de cuenta.

## Implementación y datos

La web reutiliza `/api/dashboard`, `/api/devices` y la explicación autorizada del
análisis exacto. No depende de que el informe siga entre los últimos veinte de un
equipo para cargar sus prioridades. Verifica que identificador e informe coincidan
con el resumen antes de derivar el panel. Si no coinciden, muestra indisponibilidad.
La lista de dispositivos y la guía se consultan en paralelo; el fallo de nombres
no elimina el historial. La API continúa autorizando cada lectura por propietario.

No hay cambios de API, tablas, migraciones, archivos del agente o recopilación de
datos. La proyección de prioridades incluye solo id, regla, título y severidad;
no copia evidencias, recomendaciones, feedback o campos adicionales al panel.
Las guías y los resultados completos conservan sus controles anteriores.

La evidencia de pruebas está en [CHANGELOG.md](../CHANGELOG.md): unidades de
orden/estados/minimización, E2E con PostgreSQL y cuentas ficticias, revisión de
360/1440 px, navegación a comprobaciones, historial antiguo y acceso revocado.
Los E2E de este bloque respetan `Retry-After` con un único reintento acotado ante
429; no cambian los límites de la API. Las capturas opcionales de cuentas ficticias
usan `E2E_CAPTURE_PRIORITIES=1` y quedan en `test-results`, excluido de Git.
