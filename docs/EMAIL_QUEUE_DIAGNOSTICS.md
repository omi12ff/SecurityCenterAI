# Diagnóstico local de la cola de correo

Implementado el **2026-10-25**. Permite observar pendientes y acumulación sin
descifrar mensajes, modificar reservas o iniciar el worker. Es una herramienta
del operador local con acceso a `.env`, no una ruta HTTP o panel público.

## Uso

Desde la raíz, en otra terminal:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\status.ps1
```

Conserva los sondeos HTTP y SMTP NOOP y agrega la cola. Por defecto compila el
diagnóstico en `StatusTools`, separado de la API. No reinicia servicios ni aplica
migraciones. Para repetir con ese binario ya compilado, agregar `-NoBuild`.

Para obtener solamente el resumen JSON, incluso con la API detenida:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\email-queue-status.ps1 -NoBuild
```

Ambos leen PostgreSQL local de `.env` mediante el helper existente. La conexión
solo se pasa al proceso hijo por entorno; nunca por argumentos o salida. La
configuración de intentos se lee desde los archivos del proyecto API y su entorno
de proceso, con predeterminado 3. Debe coincidir con la del worker observado.
`-Port` cambia el sondeo HTTP, no la conexión de la cola. `-SmtpPort` cambia solo
el sondeo SMTP. La fuente del resumen sigue siendo PostgreSQL local de `.env`.

## Qué significa cada contador

El total se divide en cinco estados excluyentes, con este orden de evaluación:

| Estado | Criterio |
| --- | --- |
| Reservados | Reserva presente con vencimiento futuro; puede estar en el último intento o terminando SMTP. No prueba que el worker esté vivo. |
| Vencidos | Sin reserva vigente y con vigencia de cola terminada. Pendientes de retirada por el worker. |
| Sin intentos | Sin reserva vigente, no vencidos y con el límite configurado agotado. |
| Listos | No reservados/vencidos/agotados y próximo intento habilitado. |
| En espera | Igual que listos, pero la fecha del próximo intento aún es futura. |

La reserva que vence exactamente en la fecha de consulta deja de estar vigente.
El contador adicional de pendientes con intentos previos es un subconjunto de
listos/en espera; no se suma otra vez al total. Un intento registrado no demuestra
rechazo SMTP: una interrupción también puede consumirlo.

Se informa creación y antigüedad del más antiguo, próximo intento de mensajes
sin reserva y próximo vencimiento de reserva. Cola vacía devuelve fechas/edad
`null`, sin inventar un envío. Fechas en UTC. Un intento ya habilitado se muestra
como «habilitado ahora»; no es un compromiso de ejecución o entrega.

## Avisos y códigos de salida

Valores iniciales locales: aviso a partir de **100 pendientes** o **10 minutos**
de antigüedad. No son límites de capacidad ni políticas de producción aprobadas.
También avisa por vencidos o intentos agotados pendientes de retirada.

En `status.ps1` se ajustan con `-QueueWarningCount` y `-QueueWarningAgeMinutes`.
En el helper JSON se usan `-WarningCount` y `-WarningAgeMinutes`. Rangos 1–1000000
mensajes y 1–10080 minutos; comparación inclusiva. No cambian límites, vigencia,
intentos o comportamiento del worker, ni se guardan como preferencias.

| Salida | Significado |
| --- | --- |
| 0 | Comprobaciones solicitadas disponibles y cola sin avisos. |
| 3 | Consulta disponible, con avisos de cola. |
| 1 | Alguna comprobación no disponible o no verificable; tiene prioridad sobre 3. |

El binario CLI también distingue argumentos inválidos (2) y cancelación/tiempo
agotado (130). Los helpers muestran un error fijo sin datos internos y salida 1.
Esquema ausente, permisos, configuración o conexión fallida no se presentan como
cola vacía. Repetir la consulta permite observar evolución; una sola instantánea
no demuestra atasco, entrega SMTP o progreso del worker.

## Datos y límites de la consulta

Una consulta agregada selecciona únicamente contadores y fechas. Se ejecuta en
transacción `READ ONLY`, con conexión/comando acotados, espera de locks de un
segundo y timeout SQL de cinco segundos. No selecciona contenido cifrado,
destinatarios, tokens, ids de cuenta/mensaje o claves. No guarda métricas, archivos
de diagnóstico, alertas o historial; la terminal puede conservar la salida.

El CLI sale antes de configurar el host web: no requiere JWT, SMTP, claves DPAPI
o un worker activo. No borra vencidos ni reintenta envíos. Un snapshot puede
cambiar inmediatamente por actividad normal. Preparación de alertas externas,
capacidad y operación pública conservan sus pendientes.

## Verificación

`scripts/test-email-outbox.ps1` incluye los casos de cola y diagnóstico en una
base ficticia, con clases serializadas para evitar interferencia. Comprueba
partición y límites, invariancia de filas, salida sin secretos, estados vacíos,
conexión rechazada, esquema ausente y locks. Prueba los scripts con HTTP/SMTP
ficticios: solo admite NOOP/QUIT y verifica prioridad de salidas 0/3/1.
`-DatabaseName` en helpers solo admite ese espacio de bases ficticias; la ejecución
habitual usa la base local de `.env`. [Resultados](../CHANGELOG.md).
