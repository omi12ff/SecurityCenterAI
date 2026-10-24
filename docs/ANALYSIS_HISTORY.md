# Exportar y eliminar un análisis

Revisión: **22 de octubre de 2026**. Este bloque permite controlar un resultado
individual del historial. No elimina la cuenta, el dispositivo o las copias que
ya estén fuera de la aplicación.

## Usarlo desde la web

El dashboard ahora enlaza directamente a las guías desde su tabla y sus prioridades.
En el historial del equipo, el informe reciente empieza abierto y los anteriores
se despliegan por fecha. Un enlace `#analysis-<id>` abre el informe indicado.
[Navegación y límites del historial](DASHBOARD_PRIORITIES.md).

1. Inicia sesión y abre **Dispositivos → Ver equipo e historial**.
2. En el análisis elegido, abre **Entender este análisis**.
3. Al pie aparece **Control de este análisis**.
4. Para guardar una copia, pulsa **Exportar análisis (JSON)**. El navegador
   descargará un archivo del análisis, sin cambiar los registros guardados.
5. Para borrarlo, marca **Confirmo que quiero eliminar este análisis** y pulsa
   **Eliminar análisis**. La confirmación es obligatoria. Tras completarse,
   volverás al historial con un aviso de eliminación.

La eliminación es permanente en los datos activos de la aplicación. Desaparecen
el análisis, sus resultados/evidencias y todo el feedback ligado a ese análisis.
Los otros análisis y el acceso del dispositivo permanecen. El dashboard y las
comparaciones posteriores consultan los registros restantes; una selección
guardada que incluya el análisis eliminado ya no estará disponible.

Los dispositivos revocados conservan estos controles para su propietario. Una
credencial del agente no sirve para exportar o borrar: se necesita la sesión
de la cuenta dueña.

## Qué contiene el archivo JSON

La exportación tiene `exportVersion: 1` e incluye:

| Campo | Contenido |
| --- | --- |
| `exportedAtUtc` | Momento UTC de generación por el servidor. |
| `device` | Identificador y nombre actual del dispositivo. |
| `analysis` | Identificador del análisis, puntos y cobertura disponibles, fecha de recepción, fechas de comprobación, fuente, versiones, identificador de informe, conteos y todos los resultados guardados. |
| `analysis.findings` | Reglas y versiones, títulos, estados/severidades, evidencia, recomendaciones y fecha de detección. |
| `feedback` | Valoración, comentario opcional, versión de guía y fecha de actualización de la valoración propia; `null` si no existe. |

Es una copia del análisis almacenado, sin recalcular puntos ni completar datos
legacy ausentes. No queda limitada a los veinte análisis o cien resultados que
puede mostrar una vista del historial. Tampoco exporta toda la cuenta o todos
los dispositivos.

El DTO de exportación no agrega correo, nombre de cuenta, claves, cookies,
tokens o hashes internos. Las evidencias y el comentario se conservan como
texto: podrían incluir datos que se hayan escrito allí. Revisa el archivo antes
de compartirlo. El JSON descargado está en texto claro; no es una copia cifrada
ni tiene un plazo de borrado administrado por la aplicación.

El nombre de descarga usa el UUID del análisis:
`securitycenterai-analysis-<identificador>.json`. La respuesta se envía como
adjunto JSON con `no-store`; esto no controla la conservación de la descarga,
copias manuales, herramientas del usuario o backups.

## Qué se conserva al eliminar

Para impedir que un reintento del agente vuelva a crear el resultado, la base
guarda exclusivamente esta marca cuando el análisis tenía un `reportId`:

| Tabla | Columnas físicas | Finalidad |
| --- | --- | --- |
| `deleted_analysis_reports` | `id_dispositivo`, `id_informe_agente`, `fecha_eliminacion_utc` | Identificar el informe eliminado por dispositivo y rechazar su reenvío. |

No se conservan en esa marca el hash del informe, puntos, cobertura, evidencia,
comentario, nombre de equipo o correo. El identificador de dispositivo sigue
vinculándola con su propietario mediante la tabla de dispositivos: no es una
marca anónima ni un registro completo de auditoría.

La clave es el par dispositivo/informe. La marca no tiene purga automática y
permanece mientras exista el registro del dispositivo. El borrado físico del
dispositivo la elimina por cascada; **revocar acceso no borra el dispositivo**
ni esa marca. No hay una función de eliminación completa de cuenta/dispositivo
incluida en este bloque.

Si un análisis accesible del historial es anterior al protocolo y no tiene
`reportId`, se elimina sin crear esta marca. Los análisis sin relación a un
dispositivo quedan fuera de estas rutas de historial por dispositivo.

## Reenviar desde el agente después de borrar

La API responde **410** cuando una credencial válida intenta enviar un informe
cuyo mismo `reportId` fue eliminado para ese dispositivo. Esto se aplica aunque
el contenido se modifique o haya un reintento concurrente; no restaura el análisis.

El agente **0.4.1-preview.1** interpreta esa respuesta y pide realizar un nuevo
`scan`. Conserva su credencial y el informe local, y no reintenta automáticamente.
No hace falta volver a vincular el equipo por ese motivo.

Desde el paquete portátil puedes generar y enviar un resultado nuevo de forma
explícita:

```powershell
.\SecurityCenterAI.Agent.exe scan --send
```

Desde el repositorio:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\agent.ps1 -Action scan -Send
```

Un escaneo nuevo obtiene otro identificador y otras fechas. No edites el JSON
anterior para eludir el rechazo ni esperes que `send` del mismo informe lo recupere.
El envío normal conserva sus límites de tiempo, tamaño y autorización. Un
dispositivo revocado sigue requiriendo resolver su acceso antes de enviar.

## Contrato y límites del backend

| Método y ruta | Acceso y respuesta |
| --- | --- |
| `GET /api/devices/{deviceId}/analyses/{analysisId}/export` | Bearer de la cuenta propietaria; adjunto `application/json` del análisis completo o `404` si no está disponible en esa cuenta/dispositivo. |
| `DELETE /api/devices/{deviceId}/analyses/{analysisId}` | Bearer del propietario y cuerpo JSON `{ "confirmed": true }`; `204` al eliminar o `404` si ya no está disponible. |

La solicitud DELETE exige confirmación verdadera, rechaza campos adicionales
y admite hasta **1 KiB** con `Content-Type: application/json`. Sin confirmación
válida devuelve `400`; cuerpo excesivo, `413`; un tipo de contenido ausente o no
admitido, `415`. Una segunda eliminación no repite la operación: devuelve `404`.
Las operaciones comparten los límites `DeviceAccount` y globales ya existentes.
Ante un conflicto concurrente persistente puede responder `409` y permitir
volver a intentar la eliminación.

La transacción de borrado agrupa eliminación de análisis, resultados y feedback
con la marca mínima. Las versiones de concurrencia del dispositivo y usuario
coordinan el borrado con la ingesta; un reintento debe observar la marca antes
de volver a aceptar el identificador. No se debilita la revocación de credenciales.

La migración `20261022194730_PrivateAnalysisHistory` agrega únicamente la tabla
de marcas, con sus tres columnas en español y clave foránea al dispositivo.
La aplicación y las comprobaciones reales de este bloque se registran en
[CHANGELOG.md](../CHANGELOG.md); esta descripción no acredita por sí sola una
migración aplicada.

## Alcance de privacidad

Eliminar desde la web no cambia Windows ni borra el informe del agente, los
JSON descargados, capturas, archivos compartidos o backups. La exportación no
incluye un mecanismo de importación/restauración. El borrado de un resultado
tampoco revoca al equipo, cierra sesiones o elimina la cuenta.

Para descargar el perfil y todo el historial propio está disponible la
[exportación de datos de cuenta](ACCOUNT_EXPORT.md) desde Mi cuenta.
El [borrado de cuenta](ACCOUNT_DELETION.md) requiere una confirmación independiente.
La [actividad privada](SECURITY_ACTIVITY.md) registra el tipo y fecha del borrado
sin identificadores del análisis, dispositivo o reporte ni su contenido.
El [mantenimiento técnico opcional](RETENTION_MAINTENANCE.md) conserva informes y marcas mínimas.
Siguen pendientes conservación automática del contenido, operación
y limpieza de backups, auditoría y políticas de producción.
El inventario y sus límites están en [PRIVACY_NOTES.md](../PRIVACY_NOTES.md), y
la ingesta e idempotencia normal en [ANALYSIS_INGESTION.md](ANALYSIS_INGESTION.md).
