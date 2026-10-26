# Estado de dependencias y diagnóstico local

Implementado el 2026-10-23, ampliado el 2026-10-25 con diagnóstico de cola de correo.
No activa un servicio externo, métricas persistentes, alarmas o consultas al agente.

## Consultar el estado

Desde la raíz, en otra terminal mientras la API sigue funcionando:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\status.ps1
```

Admite `-Port 5179` para consultar una instancia existente en ese puerto. Solo
consulta localhost, sin iniciar servicios ni modificar datos. Compila el CLI en
`StatusTools` por defecto; `-NoBuild` reutiliza ese binario. Devuelve 0 si HTTP,
SMTP y cola pasan; 3 si la cola requiere revisión y 1 si alguna comprobación falla
o no se puede completar. La salida 1 tiene prioridad sobre avisos de cola.

| Ruta pública | Respuesta | Qué comprueba |
| --- | --- | --- |
| `GET /health` | 200, `status: healthy` | La API puede atender HTTP, sin depender de PostgreSQL. |
| `GET /health/ready` | 200, `status: ready`, `postgresql: available` | Apertura de conexión y ejecución de `SELECT 1` con la configuración de la API. |
| `GET /health/ready` con fallo | 503, `status: not_ready`, `postgresql: unavailable` | No se completó la consulta. No publica motivo, servidor o credenciales. |

Se solicita cancelación a los tres segundos y se configuran tiempos de conexión
y comando de tres segundos. La cancelación/limpieza del proveedor puede añadir
tiempo; no es una garantía estricta de tres segundos de extremo a extremo.
Las respuestas 200/503 llevan `no-store` y `nosniff`. Readiness admite 30 consultas
por minuto e IP (`RateLimits:ReadinessPermitLimit`), además del límite global.
El exceso devuelve 429 sin ejecutar la consulta; los límites son de cada proceso.

Si liveness funciona y readiness falla, revisá PostgreSQL y la configuración local.
No implica que haya que reiniciar una API que sigue viva. `scripts/dev.ps1`, al
encontrar una instancia existente, también informa si PostgreSQL responde.
Para cargar código nuevo sigue siendo necesario `-Restart`.

Readiness HTTP no comprueba migraciones, todas las tablas/permisos, disco, SMTP o entrega de
correo, web, agente, backups o recuperación. Un 200 es una observación puntual.
No se envían mensajes de prueba desde readiness.

`scripts/status.ps1` también comprueba saludo SMTP y `NOOP` en localhost:1025,
sin envío. El fallo devuelve salida 1. No prueba entrega, autenticación o TLS
de producción. [Transporte, reintentos y límites](MAIL_DELIVERY.md).

## Diagnóstico de la cola

El [diagnóstico de correo](EMAIL_QUEUE_DIAGNOSTICS.md) agrega contadores de
listos/en espera/reservados/vencidos/agotados, antigüedad y próximos intentos.
Consulta agregada de solo lectura, sin payloads, claves, envío o ruta HTTP nueva.
Usa la base local de `.env` aunque se elija otro `-Port` HTTP. Umbrales iniciales
de 100 pendientes/10 minutos configurables por comando; no son límites del worker.

## Logs de la API

Cada solicitud recibe un UUID creado por el servidor en `X-Request-Id`; se ignora
el id entrante. Una excepción no controlada produce un problema genérico con
ese `requestId`, sin su mensaje o stack. El id permite localizar el registro.

El JSON contiene método de una lista cerrada, plantilla de ruta, estado, duración
en milisegundos, id generado y código cerrado de fallo. `/api/devices/<UUID>`
se registra como `api/devices/{id:guid}`; rutas desconocidas como `unmatched` y
métodos desconocidos como `OTHER`. El tiempo del registro está en UTC.

No incluye valores de ruta, query, IP, nombre/correo de usuario, cuerpos, cookies,
cabeceras, tokens, conexiones, evidencias, scopes, mensajes o stacks de excepción.
Una desconexión detectada se registra como 499. Si una respuesta ya iniciada
falla, se aborta la conexión y se conserva el estado HTTP enviado junto al fallo.

| Evento | Id | Alcance |
| --- | --- | --- |
| `request_completed` | 1000 | Respuesta terminada, incluso errores controlados 4xx/503. |
| `request_failed` | 1001 | Excepción manejada o cancelación. |
| `email_queue_rejected` | 1100 | Código reservado de la cola anterior; la cola durable ahora participa de la transacción de cuenta. |
| `email_delivery_failed` | 1101 | Entrega descartada/agotada o fallo de consulta/confirmación de la cola persistente. |
| `email_delivery_retry` | 1102 | Reintento SMTP pendiente, solo número de intento; sin destinatario o excepción. |

Se conservan mensajes de mantenimiento técnico con estado y conteos agregados.
Una lista cerrada admite solo estas categorías revisadas. Se deshabilitan logs
generales de ASP.NET Core, EF Core, Npgsql y otras categorías, incluidos SQL y
errores, aunque `Logging:LogLevel` pida Trace. La consola JSON excluye scopes.
Antes de añadir otra categoría o campo hay que revisar su contenido.

Esta protección cubre los logs de la API iniciada. Comandos administrativos,
scripts, fallos anteriores a construir la aplicación, Next.js, Docker, PostgreSQL,
SMTP, proxies, dumps o instrumentación adicional tienen alcance independiente.

## Conservación y verificación

La aplicación no crea archivos de logs ni una tabla de solicitudes. El terminal
o hosting puede conservar la salida; acceso, rotación, conservación, métricas,
alertas y respuesta a incidentes siguen pendientes. No se asocia el id de petición
a la cuenta ni se añade a su exportación. La [actividad privada](SECURITY_ACTIVITY.md)
conserva su alcance independiente. El correo tiene cola cifrada persistente y
recuperación tras reinicio, con sus [límites de entrega](MAIL_DELIVERY.md).
El nuevo diagnóstico no persiste métricas ni sustituye alertas o respuesta operativa.

Seis pruebas nuevas cubren disponibilidad/fallo, independencia de liveness,
cancelación, límites, conexión rechazada en loopback ajeno a la base personal,
correlación y exclusión de secretos ficticios en respuesta/logs aun con niveles
verbosos. Regresión y comprobación con PostgreSQL local en el [changelog](../CHANGELOG.md).

Referencia oficial: [health checks de ASP.NET Core 8](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-8.0).
