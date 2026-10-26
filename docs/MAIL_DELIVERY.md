# Cola cifrada y transporte de correo

Actualizado el **2026-10-25**. PostgreSQL conserva los mensajes pendientes y sus
intentos; reiniciar la API no vacía la cola. El capturador sigue siendo Mailpit
local. No se eligió ni configuró un proveedor externo. Aceptación por SMTP no
prueba llegada a una bandeja final o lectura por el destinatario.

## Operaciones y confirmación

Verificación, recuperación, cambio de contraseña, recuperación completada y
confirmaciones/avisos de cambio de correo usan la misma cola. Los avisos de
cambio completado no llevan token o enlace.

El productor prepara el registro cifrado en el mismo DbContext que la operación
de cuenta. `SaveChanges` y la transacción existente confirman ambos juntos. No
hay un segundo envío después del commit que pueda perderse durante un reinicio.
Si preparar o guardar la cola falla, la operación no se confirma parcialmente.
Una respuesta de éxito significa operación y cola confirmadas; no entrega SMTP.
SMTP caído no impide guardar una operación cuando PostgreSQL y las claves funcionan.

Un worker consulta cada dos segundos cuando no hay trabajo. En PostgreSQL toma
una fila con `FOR UPDATE SKIP LOCKED`, guarda una reserva de dos minutos y confirma
antes de llamar a SMTP. No mantiene una transacción durante la red. Otras
instancias con las mismas claves pueden tomar otras filas, pero no la reserva
vigente. Tras un cierre interrumpido se recupera al vencer la reserva.

## Datos y claves

`account_email_outbox` es la tabla número dieciséis del modelo. Sus columnas
están en español: id de correo/usuario, contenido cifrado, creación, vencimiento,
próximo intento, intentos, reserva/vencimiento y versión de concurrencia.
El destinatario, token y propósito están dentro del contenido cifrado, no en
columnas legibles. Es material recuperable para entregar, distinto de los hashes
de un uso existentes. Cada contenido queda ligado criptográficamente a su usuario
y fila: copiarlo a otra identidad no permite recuperarlo.

ASP.NET Core Data Protection conserva su anillo de claves en:

```text
%LOCALAPPDATA%\SecurityCenterAI\Api\EmailKeys
```

La carpeta admite únicamente usuario Windows actual y SYSTEM, rechaza uniones y
usa DPAPI CurrentUser para proteger las claves en disco. No está en el repositorio,
OneDrive, `.env`, JWT o el backup de PostgreSQL. No borrar esa carpeta para
actualizar o reiniciar. Un administrador de la base sin esas claves no obtiene el
contenido; comprometer la identidad/proceso de la API tiene un alcance mayor.

La implementación actual de protección persistente admite **Windows con perfil
local**. No usa claves efímeras silenciosamente en un despliegue Linux/contenedor.
Otra identidad, máquina o alojamiento requieren acordar/configurar custodia,
protección y acceso a claves antes de desplegar. Testing usa claves efímeras
explícitas; una prueba separada reabre un anillo DPAPI real después de recrear la API.

## Vigencia, reintentos y limpieza

| Opción de `Email` | Predeterminado | Rango |
| --- | --- | --- |
| `DeliveryAttempts` | 3 intentos totales | 1–5 |
| `RetryDelaySeconds` | 1 segundo multiplicado por el intento fallido | 1–30 |
| `QueueLifetimeMinutes` | 60 minutos desde la creación | 1–1440 |

Opciones de proceso: `Email__DeliveryAttempts`, `Email__RetryDelaySeconds` y
`Email__QueueLifetimeMinutes`. Los cambios se aplican al reiniciar. La vigencia
ya guardada de una fila no se extiende al cambiar la opción.

El intento se contabiliza antes de SMTP y sobrevive a fallos del proceso. Se
reintentan timeout y respuestas temporales 421/450/451/452. Un rechazo permanente,
error inesperado o agotamiento retira la fila. SMTP tiene diez segundos por intento.
La cancelación conserva la fila reservada para su recuperación posterior; puede
haber consumido un intento aunque no se conozca si SMTP aceptó el mensaje.

Antes de enviar se revalida la cuenta y la reserva. Enlaces requieren el token
actual sin consumir ni vencer, destinatario correcto y estado de verificación o
solicitud de cambio todavía pendiente. Cancelaciones, reemplazos y confirmaciones
pueden hacer innecesario un correo. El worker retira material inválido, vencido
o indescifrable sin entregarlo. El límite de la cola no extiende la vigencia de tokens.

Éxito, descarte o vencimiento eliminan físicamente la fila, sin historial de
cuerpos, destinatarios o fallos. Borrar una cuenta hace cascada. Si la API permanece
apagada no hay limpieza por reloj: el worker la realiza al volver a funcionar.
Una operación ya en SMTP puede terminar después de borrar la cuenta; no permite
retirar correos ya aceptados. Mailpit y otras copias tienen custodia independiente.

No se garantiza entrega exactamente una vez: SMTP puede aceptar y perderse su
respuesta, o fallar la confirmación posterior en PostgreSQL. Un reintento podría
duplicar el mensaje. Tampoco hay confirmaciones de lectura o bandeja final.
La cola reemplaza el límite anterior de cien objetos en memoria por persistencia
con tamaño de contenido, vigencia e intentos acotados; capacidad y alertas de una
operación pública siguen pendientes.

## Migración, exportaciones y recuperación

`20261025143028_DurableAccountEmailOutbox` crea la tabla e índices; no transforma
cuentas o registros anteriores. Down exige cola vacía y toma un bloqueo para
impedir eliminar correos pendientes. Producción debe avanzar mediante migraciones
nuevas; no hacer downgrade para resolver una entrega fallida.

La exportación de cuenta no incluye el contenido cifrado ni secretos de entrega.
El backup administrativo sí incluye la tabla, con su cifrado, pero no el anillo
de claves. Una restauración real debe retirar correos pendientes y tokens
restaurados antes de habilitar SMTP. [Procedimiento](RECOVERY_RUNBOOK.md).

## Diagnóstico y pruebas

`scripts/status.ps1` conserva el NOOP SMTP local y readiness de PostgreSQL; no
envía correos ni diagnostica entrega final. Logs 1101/1102 usan estados fijos y
número de intento, sin destinatarios, tokens, cuerpos, claves o excepción SMTP.
Un fallo de consulta/confirmación del worker reintenta tras cinco segundos.

El [diagnóstico de cola](EMAIL_QUEUE_DIAGNOSTICS.md) agrega a `status.ps1` los
contadores y fechas de pendientes, con avisos por volumen/antigüedad y salidas
0/3/1. También admite JSON con `email-queue-status.ps1`. No selecciona payloads,
descifra, inicia worker o envía mensajes; PostgreSQL se consulta en READ ONLY.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-email-outbox.ps1
```

El helper crea y retira una base ficticia del espacio audit, sin apuntar la API
personal a ella. Prueba atomicidad, reservas concurrentes, cascada, exportación,
restart de API/anillo DPAPI, rechazo de downgrade y diagnóstico de solo lectura,
incluidos fallos y sondeos HTTP/SMTP ficticios. Se incluye en
`verify.ps1 -IncludePostgres`. Pruebas unitarias cubren límites, cancelación,
reintentos, cifrado ligado a identidad y enlaces obsoletos. [Resultados](../CHANGELOG.md).

Referencias de diseño: [transacciones EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions),
[configuración de Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-8.0)
y [cifrado de claves con DPAPI](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-8.0).
