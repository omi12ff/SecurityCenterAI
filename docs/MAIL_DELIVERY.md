# Transporte de correo y fallos temporales

Implementado el 2026-10-24 sobre la cola existente en memoria. No se eligió un
proveedor externo ni se cambió el capturador local Mailpit. Un mensaje aceptado
por SMTP no prueba entrega a una bandeja final o lectura por su destinatario.

La cola transporta verificación, recuperación, aviso de contraseña cambiada,
aviso de recuperación completada, confirmaciones de dirección actual/nueva y
aviso de correo cambiado. Los avisos posteriores no llevan enlaces o secretos.
El [cambio de correo](ACCOUNT_EMAIL_CHANGE.md) requiere dos confirmaciones y
finalización autenticada; aceptar un mensaje en cola no completa ese proceso.

## Reintentos y límites

El worker envía un mensaje a la vez. Conserva como máximo cien mensajes en la
cola y uno en proceso. Reintenta errores de transporte, timeout y respuestas SMTP
temporales 421/450/451/452. Un rechazo permanente, por ejemplo 550, se descarta
sin reintento y el worker sigue con el siguiente mensaje. Errores inesperados se
tratan como definitivos; no se imprime su mensaje o excepción.

| Opción de `Email` | Predeterminado | Rango |
| --- | --- | --- |
| `DeliveryAttempts` | 3 intentos totales | 1–5 |
| `RetryDelaySeconds` | 1 segundo multiplicado por el intento fallido | 1–30 |
| `QueueLifetimeMinutes` | 5 minutos desde la entrada a la cola | 1–30 |

Variables de proceso equivalentes: `Email__DeliveryAttempts`,
`Email__RetryDelaySeconds` y `Email__QueueLifetimeMinutes`. Reiniciar la API aplica
cambios. No se modifica `.env` ni se activan servicios con estas opciones.

El transporte mantiene su timeout de diez segundos por intento. Antes de cada
intento se comprueba la antigüedad en cola; material demasiado antiguo se descarta.
El límite de espera no demuestra vigencia de un token: esta depende de la base y
su configuración. La cancelación de la API interrumpe el envío/espera sin continuar
reintentando. Una cola llena rechaza la entrada inmediatamente.

Los reintentos pueden duplicar mensajes si SMTP aceptó un envío cuya respuesta
se perdió. Los enlaces siguen siendo de un solo uso; un enlace reemplazado puede
haber quedado inválido cuando llegue el mensaje. La aplicación no afirma
semántica de entrega exactamente una vez.

## Diagnóstico local

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\status.ps1
```

Además de liveness y PostgreSQL, abre una conexión únicamente a `localhost:1025`,
comprueba saludo SMTP y `NOOP`, y cierra con `QUIT`. No envía correo, credenciales
o contenido, ni imprime la respuesta del servidor. `-SmtpPort` cambia solo el
puerto de loopback. Un fallo produce estado fijo y salida 1. Esto no comprueba
SMTP de producción, autenticación, TLS, entrega, cola o DNS. Readiness HTTP sigue
dependiendo solo de PostgreSQL.

Los logs mantienen códigos cerrados: 1100 para rechazo de cola, 1101 para entrega
descartada/agotada y 1102 para reintento pendiente con número de intento. No incluyen
destinatario, token, cuerpo, servidor, credenciales o excepción SMTP.

## Pendiente antes de operación externa

La cola sigue siendo volátil: reiniciar, terminar o fallar el proceso puede perder
mensajes. No hay outbox, cola durable, recuperación posterior a reinicio, estado
de entrega por usuario o alerta automática. La aceptación por la cola no revierte
ni garantiza el resultado de una operación de cuenta ya confirmada. Se necesitan
custodia de material sensible, conservación y estrategia de recuperación antes
de hacerla durable. Los reintentos actuales no completan ese trabajo.

Pruebas de fallos temporales/permanentes, límites, cancelación, caducidad y
exclusión de secretos en el [changelog](../CHANGELOG.md).
