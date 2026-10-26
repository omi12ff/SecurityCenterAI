# Cambiar la contraseña desde Mi cuenta

Implementado el 2026-10-23. En **Mi cuenta → Cambiar contraseña**, ingresá la
actual, una nueva diferente y su repetición. Confirmá el cierre de sesiones y
elegí **Guardar contraseña nueva**. Al terminar volvés al login. Si olvidaste
la actual, sigue disponible la recuperación por correo.

La nueva admite de 15 a 128 caracteres y no puede ser solo espacios. También se
comprueba la lista local inicial de valores previsibles; [detalle de conteo y límites](ACCOUNT_SECURITY_POLICY.md). Las claves
se validan exactamente: no se recortan o normalizan. Para la actual se admite
la longitud anterior de hasta 128, sin exigir el mínimo nuevo. Los errores no
devuelven ninguna contraseña al cliente.

## Efectos

- Actualiza el hash y cierra todas las sesiones web propias, incluida la actual.
  Invalida JWT y refresh rotados; no cancela operaciones ya autorizadas.
- Consume los enlaces de recuperación pendientes. Conserva verificación del
  correo actual y cancela solicitudes de cambio de dirección. Conserva
  contenido y acceso independiente de agentes, revocable en Dispositivos.
- Registra `password.changed` sin secretos, id de sesión o texto libre. Es distinto
  de `password.reset` por recuperación y se incluye en la exportación existente.
- Guarda un aviso cifrado sin contraseñas o enlaces de acceso en el mismo commit.
  El worker lo entrega después y conserva intentos tras reinicios. No recibir
  el aviso no permite deducir el resultado del cambio; SMTP no confirma bandeja final.

La web borra sus dos cookies al confirmar. Otros navegadores pueden conservar
cookies hasta su siguiente intento, pero el servidor rechaza su acceso. Ante una
respuesta incierta, intentá entrar con la clave nueva o recuperar el acceso antes
de repetir la operación.

## API y controles

`POST /api/account/password` requiere Bearer activo y cuerpo de hasta 2 KiB con
`currentPassword`, `newPassword` y `confirmed: true`. Responde 204; 400 por datos
inválidos, 403 si falla la contraseña o hay bloqueo, 401 si la sesión dejó de ser
válida, 409 por conflicto persistente y 429 por límites. No autoriza por un
propietario enviado en query o cuerpo.

`AccountPassword` permite cinco solicitudes/minuto/IP por defecto, además del
límite global (`RateLimits:AccountPasswordPermitLimit`). Comparte contador y
bloqueo por contraseña incorrecta con login/eliminación: cinco fallos y quince
minutos con las opciones actuales. Los límites por IP son de una instancia y las
llamadas intermediadas por Next.js comparten dirección.

En PostgreSQL bloquea la fila del usuario y vuelve a comprobar la sesión.
Hash, revocaciones, enlaces, evento y registro de correo se confirman en la misma transacción,
coordinados mediante versiones con login/refresh/reset/borrado. Un fallo del
evento o al guardar la cola impide el cambio; SMTP se intenta después de confirmar la escritura.

## Migración y alcance

`20261024003036_AccountPasswordActivity` amplía el CHECK a trece tipos. No agrega
columnas ni modifica claves o contenido existentes. El downgrade no borra o
reinterpreta eventos: el CHECK anterior rechaza el retorno mientras existan
entradas `PasswordChanged`. Requiere planificar un rollback compatible.

Siguen pendientes un corpus amplio de contraseñas comprometidas, MFA y operación de correo de producción.
El mínimo actual no completa esas políticas. El aviso del cambio autenticado se
complementa con un aviso posterior a recuperación, sin enlaces o credenciales.
Ambos usan la [cola cifrada persistente](MAIL_DELIVERY.md); [correo y cancelaciones](ACCOUNT_EMAIL_CHANGE.md).
La purga técnica no cambia su alcance.

Referencia: [OWASP: cambio de contraseña](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html#change-password-feature).
Pruebas y límites en el [changelog](../CHANGELOG.md).
