# Envío de análisis y puntos confirmados

Revisión: **2026-10-21**. Esta guía describe el bloque de ingesta manual de
las diez reglas de Windows. La evidencia de comprobación y sus límites se
registran en [CHANGELOG.md](../CHANGELOG.md).

## Probar el recorrido

Deja el backend y la web activos en sus terminales. Ejecuta el agente en una
tercera terminal desde la raíz del repositorio. Si ya está vinculado, no
necesitas otro código: `status` muestra los metadatos locales, mientras que
`heartbeat` consulta la autorización remota. Si la credencial venció, se revocó
o restableciste la contraseña, genera un código nuevo en **Mis dispositivos**
y vuelve a vincular con `pair -Replace`.

Para comprobar Windows y guardar un informe sin enviarlo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\agent.ps1 -Action scan
```

Para ejecutar las comprobaciones y enviar su resultado explícitamente:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\agent.ps1 -Action scan -Send
```

Para enviar o reintentar el último informe compatible ya guardado:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\agent.ps1 -Action send
```

Actualiza [Mis dispositivos](http://localhost:3000/devices), entra en
**Ver equipo e historial** y consulta los resultados del dispositivo. El
dashboard conserva el resumen de los análisis propios. Un envío no inicia un
análisis nuevo en el servidor: guarda las observaciones que produjo el agente.

`scan` sigue funcionando sin API ni cuenta. `scan -Send` y `send` requieren la
credencial de dispositivo y la API donde se vinculó. No hay envío automático,
servicio, tarea programada, IA ni cambios de configuración de Windows.

## Qué sale del equipo

El envío contiene versión del esquema e informe, identificador del reporte,
versión declarada del agente, fechas UTC y diez resultados. Cada resultado
incluye únicamente identificador y versión de regla, estado, evidencia
seleccionada y fecha de observación. No se envían el token dentro del informe,
la contraseña de cuenta, correo, nombre del usuario o PC, archivos, salida
bruta de cmdlets, seriales ni claves de recuperación. La credencial se utiliza
en el encabezado `Authorization: Device …`.

El JSON local puede conservar los textos de ayuda del agente, pero la API usa
su propio catálogo para títulos, severidad, recomendaciones y pesos. No acepta
un puntaje calculado por el cliente como resultado del análisis. La evidencia
sigue siendo una declaración del cliente: la autenticación no demuestra que
se ejecutó un binario intacto o que Windows respondió esos datos. No hay
atestación del hardware ni verificación remota de los cmdlets.

## Cómo leer los resultados

Las diez reglas tienen versión 1. Sus criterios y limitaciones están en
[la guía del agente](WINDOWS_AGENT.md#diez-comprobaciones-y-sus-límites).

| Estado | Interpretación | Efecto en el cálculo |
| --- | --- | --- |
| `passed` / Correcto | Se observó el criterio limitado de la regla. | Suma su peso como puntos confirmados. |
| `attention` / Revisar | La observación requiere revisión. | No suma puntos y cuenta como resultado conocido. |
| `unknown` / Desconocido | No fue posible determinarlo, por ejemplo por permisos. | No suma puntos; permanece en el peso aplicable, sin considerarse un riesgo comprobado. |
| `notApplicable` / No aplica | La función se declaró explícitamente no aplicable. | Se excluye del peso y número de reglas aplicables. |

Los **puntos confirmados** son el porcentaje del peso aplicable que se observó
como correcto. No son una probabilidad de estar protegido ni una certificación
del PC. El resultado debe leerse junto con la **cobertura**: cantidad de reglas
con resultado conocido (`passed` o `attention`) respecto de las aplicables.
La cobertura cuenta reglas, no pesos. Los desconocidos no se convierten en
aprobaciones ni en riesgos demostrados.

| Regla | Peso |
| --- | ---: |
| Defender antivirus | 15 |
| Defender en tiempo real | 15 |
| Antigüedad de firmas | 10 |
| Perfiles efectivos de firewall | 15 |
| Secure Boot | 10 |
| TPM preparado | 5 |
| BitLocker del volumen del sistema | 10 |
| Servicio Windows Update | 5 |
| UAC | 10 |
| SMB1 del servidor | 5 |

La suma inicial es 100. Si hay reglas no aplicables se ajusta el denominador.
El puntaje y el porcentaje de cobertura se guardan como enteros truncados.
Si todas son desconocidas o no aplicables, el puntaje es `null`: no se presenta
un 0 ni un 100 artificial. Un resultado con desconocidos se muestra como
parcial. Los análisis antiguos sin estos metadatos se conservan como legacy;
no se les inventan cobertura, estados ni versiones de regla.

Ejemplo: siete reglas correctas y tres desconocidas pueden producir cobertura
7/10 y menos de 100 puntos confirmados, aunque ninguna requiera revisión. La
falta de permisos es información pendiente, no prueba de una protección
desactivada. El servicio Windows Update sigue sin acreditar parches instalados.

## Contrato y reintentos

`POST /api/agent/analyses` requiere exclusivamente la credencial `Device`.
Un Bearer de cuenta no autoriza el envío. La cuenta y dispositivo del análisis
se obtienen de la credencial, no de identificadores enviados por el cliente.
La consulta posterior requiere Bearer del propietario y continúa filtrando
por cuenta y dispositivo.

El reporte nuevo usa esquema **2** y un UUID `reportId` estable. Dentro de los
límites de validez del informe, enviar otra
vez el mismo reporte al mismo dispositivo devuelve el análisis existente,
sin duplicar historial ni resultados. Reutilizar ese identificador con otro
contenido produce conflicto. El hash canónico compara el contenido relevante;
no es firma, cifrado ni prueba del origen físico del reporte. El mismo informe
reintentado debe conservar su identificador, contenido y fechas.

Los JSON del esquema 1 conservan su utilidad local, pero no se convierten
automáticamente en reportes nuevos: vuelve a ejecutar `scan` para producir el
esquema 2. El archivo y la evidencia pueden contener estados de seguridad;
revísalos antes de compartirlos. No hay retención ni purga automática de los
archivos locales o de los registros remotos.

La API limita tamaño, antigüedad y estructura del reporte, y exige exactamente
las diez reglas conocidas sin duplicados y en la versión admitida. Rechaza
estados, versiones y campos no admitidos; una fecha de recepción del servidor
se conserva separada de las fechas declaradas por el cliente. Estos límites
reducen datos inválidos y abuso, sin verificar la veracidad de las observaciones.

El cuerpo admite hasta **64 KiB** y la política predeterminada permite **6
envíos por minuto** por IP, además de los límites globales. Las fechas deben
tener offset UTC cero: se admiten reportes de hasta **7 días**, duración de
escaneo de hasta **30 minutos** y tolerancia de reloj futuro de **5 minutos**.
Cada observación debe pertenecer al intervalo del escaneo, con cinco segundos
de margen. Un informe demasiado antiguo requiere un escaneo nuevo. Los límites
de solicitudes están en memoria y no constituyen una cuota distribuida.

El primer envío devuelve `201`; el reintento idéntico no eliminado, `200` con
`alreadyReceived=true`. La respuesta incluye `analysisId`, `reportId`, puntaje
opcional, cobertura porcentual, los cuatro conteos, `scoringVersion=1` y fecha
de recepción. Datos inválidos producen `400`, falta de autorización `401`,
conflicto de identificador `409`, exceso de cuerpo `413` y límite de solicitudes
`429`.

Si el propietario eliminó ese reporte del historial, una marca mínima de
dispositivo/reporte/fecha impide recrearlo y la API devuelve `410` para el mismo
identificador. El agente `0.4.1-preview.1` pide un escaneo nuevo, conserva
credencial e informe local y no reintenta automáticamente. No edites el reporte
eliminado para cambiar su identidad. [Exportación y borrado](ANALYSIS_HISTORY.md).

Si no hay respuesta por un fallo de red, el análisis puede haberse guardado.
Reintenta con `send`, sin ejecutar otro `scan` solo para recuperar la respuesta.
Si la API devuelve `401`, comprueba la vinculación; si devuelve `429`, espera
antes de reintentar. No borres la base ni sustituyas la credencial para reparar
un problema de red.

## Datos e historial

Cada análisis guarda el identificador y huella del reporte, versión del esquema
y agente, fechas declaradas y de recepción, versión de cálculo, conteos de
estados, cobertura y puntaje opcional. Los diez resultados guardan estado,
evidencia, regla/version y los textos del catálogo como instantáneas. Así el
historial no cambia al editar el catálogo en una versión futura.

La migración amplía tablas existentes sin borrar cuentas, dispositivos o
análisis anteriores. `Score` pasa a admitir `null`; los puntajes previos
conservan su valor. Las columnas nuevas siguen los nombres en español del
proyecto. Consulta el inventario en [PRIVACY_NOTES.md](../PRIVACY_NOTES.md).

La consola de Windows, API y web completan este recorrido manual acotado.
La distribución firmada, compatibilidad por edición, remediación, ejecución
automática, interpretación por IA y controles de producción siguen pendientes
en [ROADMAP.md](../ROADMAP.md).
