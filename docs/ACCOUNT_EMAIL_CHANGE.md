# Cambiar el correo de la cuenta

Implementado el 2026-10-24. En **Mi cuenta → Cambiar correo electrónico** se
puede cambiar la dirección usada para iniciar sesión. Requiere correo actual
verificado, acceso a ambas direcciones y contraseña actual. No sustituye MFA ni
permite recuperar una dirección a la que ya no tenés acceso.

## Uso local

1. Ingresá el correo nuevo dos veces y tu contraseña actual. Confirmá que tenés
   acceso a ambos correos y elegí **Enviar confirmaciones**.
2. Revisá los dos mensajes en [Mailpit local](http://localhost:8025). Desarrollo
   captura mensajes, sin enviarlos a Internet.
3. Abrí cada enlace, marcá la confirmación y elegí **Confirmar dirección**. Abrir
   un enlace sin pulsar el botón no cambia el estado. Confirmar una dirección
   tampoco cambia todavía el perfil.
4. Volvé a Mi cuenta, pulsá **Actualizar confirmaciones** y revisá la dirección
   mostrada. Ingresá otra vez tu contraseña y confirmá el cierre de sesiones.
   Elegí **Finalizar cambio de correo**.
5. Entrá con el correo nuevo y la misma contraseña. Queda verificado y el anterior
   deja de identificar esa cuenta.

La solicitud vence a los treinta minutos por defecto. Podés cancelarla; un
reemplazo invalida los enlaces anteriores y cambia su id. Se comparte el
enfriamiento de verificación: sesenta segundos por cuenta, incluso después de
cancelar. Tras verificar por primera vez puede ser necesario esperar ese minuto.
No se reserva la dirección nueva: si otra cuenta la ocupa, finalizar falla sin
modificar el correo actual. Las pestañas abiertas necesitan actualizarse.

## Efectos

Finalizar conserva identificador de cuenta, nombre, hash de contraseña, contenido,
equipos y sus credenciales independientes. Cierra todas las sesiones web e
invalida enlaces de cuenta anteriores. La web elimina cookies y muestra el motivo
en login. No cancela operaciones ya autorizadas antes del cierre.

Correo verificado, revocación de sesiones, consumo de enlaces, borrado de la
solicitud, evento `email.changed` y avisos cifrados se confirman juntos. El evento
no contiene direcciones, ids de sesión o texto libre. El worker intenta enviar
los avisos sin enlaces o credenciales a ambos correos después del commit.
La cola sobrevive a reinicios; SMTP aceptado no garantiza bandeja final.

Cambiar o recuperar la contraseña cancela solicitudes de correo pendientes.
Recuperación conserva su alcance de cierre web e invalidación de códigos y
credenciales de agentes; ahora también envía un aviso posterior sin enlace,
contraseña o token. La recuperación y el registro cifrado del aviso son atómicos;
un fallo posterior de SMTP no deshace la recuperación. [Cola y límites](MAIL_DELIVERY.md).

## API y controles

| Ruta | Contrato |
| --- | --- |
| `GET /api/account/email` | Bearer, `pending` propio o null; id de solicitud, correo nuevo, fechas y banderas. |
| `POST /api/account/email/request` | Bearer, `newEmail`, `currentPassword`, `confirmed: true`; 2 KiB. |
| `POST /api/account/email/confirm-current` | Público, token del correo actual y `confirmed: true`; 1 KiB. |
| `POST /api/account/email/confirm-new` | Público, token del correo nuevo y `confirmed: true`; 1 KiB. |
| `POST /api/account/email/complete` | Bearer, `requestId`, `currentPassword`, `confirmed: true`; 2 KiB. |
| `POST /api/account/email/cancel` | Bearer, `requestId`, `confirmed: true`; 1 KiB. |

POST correcto: 204. Datos/enlaces inválidos: 400; sesión inválida: 401;
contraseña/bloqueo o correo actual no verificado: 403; solicitud sustituida o
dirección no disponible: 409; límites/enfriamiento: 429. Respuestas del controlador
no-store/nosniff. Tokens separados, con propósito, vencimiento y uso único.

Request/complete usan `AccountEmailChange`, cinco solicitudes/minuto/IP por
defecto; status/cancel, `DeviceAccount`; confirmaciones, `AuthPublic`. Además rige
el límite global. Contraseña incorrecta comparte contador/bloqueo con login,
borrado y cambio de clave. La clave actual admite longitudes anteriores. Dueño
y sesión vienen de claims, no del cuerpo. Device no autoriza las rutas privadas.

`AuthSecurity:EmailChangeMinutes` configura duración de 1–30 minutos (variable de
proceso `AuthSecurity__EmailChangeMinutes`, requiere reiniciar). No se cambió
`.env`. Las direcciones siguen el recorte exterior/minúsculas ya usado por cuentas.

Las mutaciones bloquean el usuario, revalidan su sesión y coordinan versiones con
login, refresh, contraseña y borrado. Reintentos acotados y unicidad de PostgreSQL
impiden dos cuentas con la misma dirección. Un formulario viejo no finaliza o
cancela una solicitud reemplazada. El mantenimiento revalida id/eligibilidad.

## Datos y migración

`20261024031825_ConfirmedEmailChange` agrega `email_change_requests`, como máximo
una fila por usuario: correos actual/nuevo, dos hashes SHA-256 de tokens aleatorios,
id, fechas y versión, con columnas en español. No guarda claves/tokens en claro
ni altera columnas de `users`. Amplía el CHECK de actividad de trece a catorce.

Las solicitudes se retiran al completar, cancelar o cambiar/recuperar contraseña;
se eliminan con la cuenta. Si solo vencen, son elegibles para mantenimiento siete
días después (`AccountTokenDays`). La purga automática sigue desactivada: vencer
no borra físicamente la fila. No se acordó una política de producción.

Exportación agrega `pendingEmailChange` con correo nuevo, fechas y banderas, o
null; excluye tokens, hashes, versión e id interno. Versión 1 aditiva, compatible
con respuestas anteriores. Backups cifrados incluyen la tabla completa y tienen
custodia independiente. Downgrade se detiene si quedan solicitudes; el CHECK
anterior también impide volver con eventos `EmailChanged`. No elimina eventos.

La confirmación usa no-store/no-referrer y retira query del historial. La ruta se
excluye de logs de Next; API conserva campos mínimos sin direcciones, cuerpos,
tokens o excepciones SMTP. Correos, infraestructura y backups tienen alcance propio.

Referencia: [OWASP: cambio de correo](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html#changing-a-users-registered-email-address).
Evidencia y omisiones en el [changelog](../CHANGELOG.md).
