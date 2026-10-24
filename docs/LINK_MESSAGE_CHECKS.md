# Revisión básica de enlaces y mensajes

Bloque de producto para la 1.0 gratuita, 2026-10-24. Reglas `local-signals-v1`;
no usa IA externa. Se accede desde **Resumen → Enlaces y mensajes**, en
`/tools/scam-check`, con cuenta y correo verificado.

## Uso e interpretación

Elegí enlace HTTP/HTTPS o mensaje, eliminá información privada, pegá el contenido
y confirmá su envío a Security Center AI. La revisión se realiza en el servidor
de esta aplicación; no se comparte con un proveedor externo. El botón de limpiar
retira el contenido y resultado de la vista. Editar el texto oculta el resultado
anterior para no atribuirlo a una entrada nueva.

El resultado es **Señales para revisar** o **Sin señales evidentes en estas reglas**.
Ninguno confirma fraude o seguridad. Cada observación tiene motivo y una acción
para contrastarla. No hay puntuación de confianza, clasificación por IA o lista
de dominios maliciosos. Texto educativo o una dirección legítima también pueden
activar reglas.

| Entrada | Revisión acotada |
| --- | --- |
| Enlace | HTTP sin TLS, sección de usuario antes de `@`, IP, dominio punycode, puerto no predeterminado y dos acortadores reconocidos (`tinyurl.com`, `t.co`). |
| Mensaje | Expresiones de urgencia, referencias a claves/códigos, premios y pagos; caracteres de formato invisibles y estructura de hasta cinco enlaces HTTP/HTTPS. |

El catálogo de acortadores es limitado, no una cobertura universal ni una lista
de amenazas. Coincide con el dominio exacto, no con fragmentos dentro de otro.
Solo se devuelven dominios/IP normalizados y observaciones fijas; se omiten rutas,
parámetros, credenciales de URL y texto del mensaje.

No abre enlaces, realiza DNS, sigue redirecciones o consulta reputación. No
comprueba contenido remoto, identidad del remitente, certificados, archivos,
imágenes/adjuntos, enlaces sin esquema o engaños fuera de las reglas iniciales.
En mensajes se informa cuántos enlaces se revisaron y omitieron.

## Límite diario y API

**25 consultas combinadas de enlace/mensaje por cuenta y día UTC**, configurable
con `LinkMessageChecks:DailyLimit` (1–1000). Es un límite inicial de desarrollo;
no un plan comercial de producción acordado. El contador persiste en PostgreSQL,
es compartido por sesiones y conserva su valor tras reiniciar la API.
El día siguiente empieza con cero usos efectivos; el primer envío válido sustituye
el día/cantidad anteriores. Se muestra hora de renovación y enlace para recargar.

| Ruta | Contrato |
| --- | --- |
| `GET /api/tools/link-message` | Bearer de cuenta: día UTC, usos, límite, disponibles y próximo inicio de día. No consume cuota. |
| `POST /api/tools/link-message` | Bearer, correo verificado, `kind: link/message`, `content`, `confirmed: true`; 32 KiB. Devuelve reporte, cuota y hora UTC. |

Enlace: hasta 2048 unidades UTF-16. Mensaje: hasta 6000. La validación rechaza
vacíos, tipos desconocidos y falta de confirmación; no consume cuota. 400 entrada
inválida, 401 sesión inválida, 403 correo no verificado, 429 cuota o límite de
solicitudes y 409 conflicto tras reintentos acotados. Respuestas privadas no-store
y nosniff; web con no-referrer y sin query de contenido. Conserva `DeviceAccount`
y límite global por IP además de la cuota persistente de usuario.

POST bloquea la fila del dueño, revalida sesión/correo y actualiza contador y
versión en una transacción. Cuota y propietario no vienen del cuerpo. Las
peticiones simultáneas comparten el límite, incluidas diferentes instancias con
la misma base. Un fallo de escritura no devuelve un resultado consumido. Perder
la respuesta después del commit puede consumir una consulta; reintentar es otra
consulta y no tiene idempotencia. No se amplía a límites distribuidos por IP.

## Datos y custodia

Migración `20261024042930_LinkMessageDailyUsage`: tabla `link_message_usage`, una
fila como máximo por usuario, con `id_usuario`, `dia_utc`, `consultas_realizadas`
y `version_concurrencia`. No cambia columnas de `users`.

No guarda entrada, hash de entrada, reporte o historial de mensajes/enlaces.
Solo conserva el contador del último día usado mientras exista la cuenta, hasta
su sustitución por un uso posterior o borrado de cuenta; no se acumulan filas por
día. La cuota no se reinicia al cambiar correo/contraseña o cerrar sesiones.
No agrega eventos al catálogo de actividad ni una categoría de analítica.

Exportación de cuenta v1 añade `linkMessageUsage` (día/cantidad o null), excluyendo
propietario interno y versión. Borrar cuenta elimina la fila por cascada; backups
cifrados incluyen ese estado y tienen custodia independiente. Downgrade se
detiene si existen contadores para no descartarlos silenciosamente.

El contenido puede permanecer en memoria del navegador, servidor o infraestructura
durante la petición; la aplicación no lo conserva en la base o logs. No pegar
secretos y revisar configuración de proxies/telemetría antes de publicar. Next
omite la ruta del log entrante de desarrollo y la API usa logs mínimos existentes.

## Fuentes y verificación

Orientación general: [FTC](https://consumidor.ftc.gov/articulos/como-reconocer-y-evitar-las-estafas-de-phishing)
e [INCIBE](https://www.incibe.es/aprendeciberseguridad/phishing).
Identificación de acortadores: [TinyURL](https://tinyurl.com/blog/help/)
y [X](https://help.x.com/en/using-twitter/how-to-tweet-a-link.html).
Estas fuentes no validan ni certifican el clasificador de esta aplicación.

Pruebas: reglas, límites/consentimiento, aislamiento, ausencia de conexión al
destino, exportación/cascada, concurrencia y rollback con PostgreSQL, backup
aislado y recorrido web. Resultados y omisiones exactos en el [changelog](../CHANGELOG.md).
