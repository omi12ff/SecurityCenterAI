# Dispositivos, vinculación y base de análisis

Revisión: **2026-10-21**. Bloque implementado y comprobado localmente.

Esta etapa prepara la identidad del equipo y el modelo de resultados. El bloque
posterior añade un [agente Windows de consola](WINDOWS_AGENT.md) con diez
comprobaciones locales. El [bloque de ingesta](ANALYSIS_INGESTION.md) permite
enviarlas manualmente y calcula puntos confirmados y cobertura. Un dispositivo
registrado o un heartbeat no demuestra
que la PC esté protegida ni que se haya ejecutado un análisis.

## Recorrido de la cuenta

La web ofrece `/devices` para consultar y administrar los dispositivos propios.
Crear un dispositivo y emitir códigos exige correo verificado, comprobado en
la base de datos. El límite es un dispositivo no revocado por cuenta, incluido
uno pendiente de vincular. Revocar ese dispositivo libera el lugar para otro.

La vinculación utiliza un código aleatorio de un uso, válido durante diez
minutos. La web muestra el código al propietario; la base conserva su hash.
Compartirlo permite a su portador vincular un cliente: se debe proteger como
una credencial y evitar incluirlo en URLs, capturas o registros compartidos.

Regenerar el código invalida los códigos anteriores. Si el dispositivo ya tiene
una credencial válida, esta continúa funcionando hasta canjear el código nuevo.
En ese canje se revoca la credencial anterior y se entrega la nueva de forma
atómica. Regenerar un código no equivale a revocar inmediatamente al cliente
que ya estaba vinculado; para eso se utiliza la revocación del dispositivo.

## Credencial de dispositivo

El canje devuelve una credencial opaca propia con vigencia de 90 días. La base
guarda su hash y comprueba su vigencia y revocación en las solicitudes del cliente.
Este secreto se utiliza con el esquema de autenticación `Device`, separado del
Bearer de la cuenta. No concede acceso al dashboard, perfil ni administración
de dispositivos de la cuenta.

El cliente autenticado puede enviar heartbeat o análisis. Heartbeat únicamente
actualiza el último contacto; no evalúa Windows ni crea análisis. El envío
explícito de resultados usa un endpoint separado. El nombre asignado y los contactos de este
protocolo no constituyen una verificación de identidad física del hardware.

La consola Windows protege su credencial con DPAPI CurrentUser fuera del
repositorio y utiliza esos endpoints. Sus comprobaciones se ejecutan localmente;
el envío requiere `scan -Send` o `send` y está separado del heartbeat. La instalación como producto, distribución
firmada y actualización segura siguen pendientes; consulta su
[guía de uso y límites](WINDOWS_AGENT.md).

## Contrato HTTP

| Método y ruta | Credencial y datos | Resultado |
| --- | --- | --- |
| `GET /api/devices` | Bearer de usuario. | Hasta 100 dispositivos propios, incluidos los revocados. |
| `GET /api/devices/{id}` | Bearer del propietario. | Dispositivo y hasta 20 análisis recientes con hasta 100 hallazgos por análisis. Un UUID ajeno no expone datos. |
| `POST /api/devices` | Bearer y `{ name }`; correo verificado. | `201` con dispositivo, código de un uso y vencimiento. |
| `POST /api/devices/{id}/pairing-code` | Bearer del propietario; correo verificado. | Código nuevo; invalida códigos previos. |
| `POST /api/devices/{id}/revoke` | Bearer del propietario. | `204`; revoca dispositivo, códigos y credenciales. |
| `POST /api/agent/pair` | `{ pairingCode, agentVersion }`; sin sesión web requerida. | `201` con `deviceId`, `deviceToken` y `expiresAtUtc`. El código autoriza el canje. |
| `POST /api/agent/heartbeat` | `Authorization: Device <credencial>` y `{ agentVersion }`. | `204` al registrar contacto; no admite Bearer de cuenta. |
| `POST /api/agent/analyses` | `Authorization: Device <credencial>` e informe del esquema 2. | `201` al guardar o `200` para reintento idéntico; valida diez reglas y calcula puntos confirmados/cobertura. |

Los nombres tienen de 2 a 100 caracteres y la versión declarada de cliente de
1 a 64, sin caracteres de control. El canje inválido, vencido o reutilizado no
entrega una credencial. Las respuestas con código o token contienen secretos:
no se utilizan URLs para transportarlos ni deben guardarse en logs.

Crear o emitir código sin correo verificado devuelve `403`; ocupar un lugar
ya utilizado o intentar revincular un registro revocado devuelve `409`.
Un dispositivo ajeno devuelve `404`. Una credencial Device vencida o revocada
no permite heartbeat ni enviar análisis; revocar no borra su historial y un registro revocado no
se reactiva: se crea otro dentro del límite de cuenta.

Los límites predeterminados por IP son 8 canjes/minuto y 60 solicitudes/minuto
para administración de dispositivos o heartbeat, y 6 envíos de análisis/minuto,
además del límite global de
120/minuto. Están en memoria y Next.js comparte su IP entre usuarios. Las
consultas acotadas no implementan una paginación del historial completo.

## Estados y recuperación

| Situación | Significado |
| --- | --- |
| `pending` | Dispositivo no revocado que no tiene una credencial vigente; necesita vinculación. |
| `linked` | Tiene una credencial de dispositivo vigente. No significa que esté online ni que haya sido analizado. |
| `revoked` | Se revocó el dispositivo; sus códigos y credenciales ya no permiten operar. |
| `pairedAtUtc` | Fecha histórica de vinculación, que puede conservarse aunque la credencial haya vencido o sido revocada. |

El restablecimiento de contraseña revoca las sesiones web, las credenciales de
dispositivo y los códigos pendientes. No elimina el dispositivo ni su historial:
el lugar de la cuenta sigue ocupado hasta revocarlo o volver a vincularlo con
un código nuevo. «Cerrar todas las sesiones» actúa sobre sesiones web y no revoca
la credencial independiente del dispositivo.

## Análisis, reglas y hallazgos

La relación de `SecurityAnalysis` con dispositivo es opcional para conservar
los análisis previos como datos legacy. Las reglas se identifican
por id y versión, y hallazgos relacionados con sus análisis y reglas. Evidencia
y recomendación se conservan como instantáneas del resultado, en lugar de
reconstruir el pasado desde un texto de regla que podría cambiar.

La [ingesta del agente](ANALYSIS_INGESTION.md) usa el catálogo versionado del
servidor para guardar diez resultados con sus estados explícitos, calcular
puntos confirmados y conservar cobertura, captura y recepción. El propietario
consulta esos resultados desde la web. Los datos sintéticos de las pruebas no
describen el estado de una computadora; un cliente autenticado tampoco acredita
por sí mismo la veracidad de sus observaciones.

La clave de regla es `(id, version)`, con versión positiva y una referencia
compuesta desde el hallazgo. La base restringe su eliminación cuando hay
hallazgos asociados. Las severidades permitidas son `informational`, `low`,
`medium`, `high` y `critical`; evidencia y recomendación admiten hasta 4000
caracteres cada una. No hay un endpoint para editar o ejecutar esas reglas.

## Seguridad y privacidad

La autorización debe comprobar el propietario y el tipo de credencial en cada
operación. Códigos y credenciales requieren consumo, caducidad y revocación
coordinados con las actualizaciones concurrentes. Las consultas de un UUID ajeno
no deben exponer sus datos. Los límites de cuenta y rate limiting también
pertenecen al backend, aunque la web deshabilite una acción.

Se revisa el diseño con las guías oficiales de OWASP sobre
[autorización](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html),
[seguridad REST](https://cheatsheetseries.owasp.org/cheatsheets/REST_Security_Cheat_Sheet.html)
y [autorización de operaciones](https://cheatsheetseries.owasp.org/cheatsheets/Transaction_Authorization_Cheat_Sheet.html).
Son referencias de diseño, no una certificación ni una implementación de OAuth.

El inventario de campos, hashes y retención se documenta en
[PRIVACY_NOTES.md](../PRIVACY_NOTES.md). Las comprobaciones efectivas del bloque
se registran en [CHANGELOG.md](../CHANGELOG.md); los resultados de etapas
anteriores no sustituyen su nueva validación.
