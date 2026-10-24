# Descargar los datos de la cuenta

Revisión: **24 de octubre de 2026**.

## Uso desde la web

1. Inicia sesión y abre **Mi cuenta**.
2. Busca **Descargar datos de mi cuenta**.
3. Pulsa **Exportar mi cuenta (JSON)** y guarda el archivo descargado.

El archivo contiene el perfil, los dispositivos y el historial de la cuenta
autenticada, con sus valoraciones y marcas mínimas de informes eliminados.
También incluye toda la actividad privada de seguridad registrada desde su incorporación.
Si hay un cambio de correo pendiente, incluye dirección nueva, fechas y banderas
en `pendingEmailChange`, sin material de envío o hashes. Si no hay solicitud es
null; se conserva versión 1 con ampliación aditiva.
Incluye equipos revocados y análisis antiguos sin dispositivo. Se exportan
todos los registros de esas categorías, sin los límites de presentación del
historial: veinte análisis o cien resultados por análisis.

La descarga mantiene la cuenta, sus sesiones, el acceso de los equipos y sus
informes. No hace falta ejecutar el agente ni generar otro análisis.

## Contenido del JSON

`exportVersion: 1` identifica este formato, separado de la exportación de un
análisis individual. `exportedAtUtc` registra la fecha UTC de generación.

| Campo | Contenido |
| --- | --- |
| `profile` | Identificador, nombre, correo, fecha de creación y estado de verificación del correo. Se exporta el estado guardado; no se inventa una fecha de verificación. |
| `securityActivity` | Todas las entradas propias de actividad, con UUID de evento, acción y fecha UTC, sin límite de 50. |
| `devices` | Todos los equipos propios, incluidos los revocados: nombre, estado, fechas, plataforma, versión declarada y vencimiento de credencial disponible. |
| `analyses` | Cada análisis con su `deviceId`, o `null` para registros antiguos sin equipo; el resultado original completo y su feedback propio, o `null`. |
| `analyses[].analysis.findings` | Todos los resultados guardados con reglas, versiones, estados, evidencia, recomendaciones y fechas originales. |
| `deletedReports` | Identificadores de dispositivo e informe y fecha de eliminación de las marcas mínimas existentes. No contienen la evidencia ni la valoración eliminadas. |

`linkMessageUsage` agrega día UTC/cantidad del último uso de enlaces/mensajes,
o null si nunca se usó. Excluye versión interna y no contiene entradas, dominios
o resultados del detector. Campo aditivo del formato 1: respuestas anteriores
siguen siendo legibles. [Alcance del módulo](LINK_MESSAGE_CHECKS.md).

Los puntos y la cobertura conservan sus valores almacenados. No se recalculan
los resultados ni se completan datos antiguos ausentes. El nombre del equipo
refleja el nombre actual guardado.

Esta exportación abarca los datos de perfil y contenido del producto descritos
arriba. Excluye las tablas internas de autenticación: sesiones, credenciales de
renovación, tokens de correo, códigos de vinculación y credenciales del agente.
Tampoco agrega hashes de contraseña o informe, secretos ni versiones técnicas
de concurrencia. El [inventario de sesiones activas](ACCOUNT_SESSIONS.md) es una consulta
autenticada independiente; no modifica este formato de exportación.
El perfil incluye deliberadamente el correo y nombre de cuenta,
a diferencia de la exportación de un análisis individual.

El JSON es una copia en texto claro y puede revelar datos de seguridad del
equipo y texto escrito en evidencias o comentarios. Revísalo antes de compartirlo
y conserva la descarga donde puedas controlar su acceso. No es un backup
operativo: no incluye un mecanismo de importación o restauración de la aplicación.

## Autorización y consistencia

`GET /api/account/export` exige Bearer de una sesión activa de cuenta. El
propietario se obtiene de la sesión autenticada; la ruta no recibe un UUID de
cuenta para seleccionar otro usuario. Una credencial `Device` no da acceso.

La API construye DTO con campos explícitos y consulta sin tracking. En
PostgreSQL, una transacción `RepeatableRead` conserva una misma instantánea al
leer perfil, dispositivos, análisis, valoraciones y marcas. Si un análisis se
elimina durante una descarga, esta representa el estado de esa instantánea;
una descarga iniciada después del borrado refleja los datos restantes.

La respuesta es un adjunto JSON con `no-store` y `nosniff`. La ruta web
`/account/export` intermedia desde el servidor con la cookie `HttpOnly` y entrega
el archivo sin exponer el Bearer al JavaScript del navegador. `/account` y sus
rutas usan `no-referrer` y `no-store`.

Existe un límite específico `AccountExport`, predeterminado en **tres solicitudes
por minuto y dirección IP**, además del límite global. Comparte la ventana
`RateLimits:WindowSeconds`; los permisos pueden configurarse mediante
`RateLimits:AccountExportPermitLimit`. En caso de `429`, espera antes de volver a
solicitar la descarga. Las llamadas intermediadas por Next.js comparten su IP.

## Alcance y pendientes

La copia agrega `securityActivity` con toda la [actividad privada](SECURITY_ACTIVITY.md)
de la cuenta: UUID de entrada, código de acción y fecha UTC. No aplica el límite
visual de 50 eventos. Es un campo aditivo del formato 1; respuestas anteriores
sin el campo siguen siendo compatibles con el parser web. No incorpora secretos,
sesiones, direcciones IP ni identificadores del contenido que se eliminó.

El bloque no modifica el esquema ni los registros de PostgreSQL. La validación
realizada se detalla en [CHANGELOG.md](../CHANGELOG.md).

La cuenta puede eliminarse con contraseña actual y confirmación desde Mi cuenta;
[alcance del borrado](ACCOUNT_DELETION.md). La conservación automática del contenido y la operación
de backups siguen pendientes; existe [mantenimiento técnico opcional](RETENTION_MAINTENANCE.md)
de autenticación. Las descargas y copias compartidas requieren custodia y borrado
propios. Revocar acceso o borrar un análisis no retira las copias ya descargadas.

La implementación prepara la respuesta en memoria. Antes de operar con historiales
muy grandes deben evaluarse generación en segundo plano, tamaño, tiempos y acceso
a descargas temporales. No hay trabajos de exportación ni archivos de exportación
persistidos por el servidor.

También están disponibles la [exportación y eliminación de un análisis](ANALYSIS_HISTORY.md)
y el [inventario de privacidad](../PRIVACY_NOTES.md).
