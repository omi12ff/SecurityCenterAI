# Eliminar la cuenta

Revisión: **22 de octubre de 2026**.

## Uso desde Mi cuenta

1. Puedes guardar una copia desde **Exportar mi cuenta (JSON)** antes de borrar.
2. En **Eliminar mi cuenta**, lee el alcance de la operación.
3. Ingresa la **Contraseña actual** y escribe exactamente `ELIMINAR`.
4. Pulsa **Eliminar mi cuenta**. Tras confirmarse el borrado, vuelves al login
   con un aviso y se eliminan las cookies de sesión de ese navegador.

La operación elimina los datos activos de la cuenta de forma permanente. No
incluye un plazo de recuperación ni una papelera. Si la contraseña es incorrecta,
la cuenta sigue disponible y el intento utiliza el contador/bloqueo temporal
existente de la cuenta. Un error de confirmación no consume intentos de contraseña.

## Datos eliminados

La eliminación de la fila del propietario y las relaciones con cascada quitan:

- Perfil, correo y hash de contraseña de la cuenta.
- Sesiones, credenciales de renovación y tokens de verificación/recuperación.
- Actividad privada de seguridad de la cuenta, sin conservar un evento nuevo de cuenta borrada.
- Todos los dispositivos, incluidos los revocados, sus códigos de vinculación
  y credenciales del agente.
- Todos los análisis, también los antiguos sin dispositivo, sus hallazgos y
  valoraciones asociadas.
- Las marcas mínimas de informes eliminados de esos dispositivos.

El catálogo compartido de reglas y las otras cuentas se conservan. Las marcas
mínimas dejan de ser necesarias: las credenciales del dispositivo también se
eliminan y ya no pueden autorizar reenvíos. No se conserva una marca nueva de
cuenta eliminada ni su correo para impedir un registro posterior.

Registrar nuevamente el mismo correo crea una identidad nueva. No restaura
los equipos, análisis ni credenciales anteriores.

## Alcance sobre copias y equipos

La operación no accede al equipo Windows. Los informes locales y `credential.enc`
permanecen en `%LOCALAPPDATA%\SecurityCenterAI\Agent`. Esa credencial ya no
autoriza a la API, aunque el comando local `status` siga mostrando su archivo.
`heartbeat` o un envío posterior recibirán rechazo; una cuenta nueva necesita
una vinculación nueva para continuar usando el servicio.

Las descargas, archivos compartidos, capturas, correos ya enviados, mensajes
pendientes en una cola y backups requieren custodia y borrado independientes.
Los tokens de correo eliminados ya no autorizan operaciones de cuenta. No se
envía un correo adicional al eliminar. Una exportación que ya estaba leyendo
una instantánea puede terminar con datos previos al borrado.

La política de conservación del contenido, limpieza de backups, registros operativos y restauración
de producción sigue pendiente. Restaurar un backup antiguo puede recuperar datos
anteriores al borrado: esta función no implementa un registro externo de borrados
que permita reaplicarlos durante la restauración.
El [backup manual](DATABASE_BACKUPS.md) incluye el estado de la fecha de copia;
su ensayo restaura únicamente en una base temporal y no reactiva la cuenta en la API.

## Contrato del backend

`DELETE /api/account` requiere Bearer de una sesión activa y este JSON:

```json
{ "password": "contraseña actual", "confirmation": "ELIMINAR" }
```

El propietario se obtiene de la sesión. No admite un identificador de otra cuenta
ni campos adicionales. El cuerpo está limitado a **2 KiB**. La contraseña conserva
sus caracteres y espacios; no se le aplican reglas de longitud mínima de registro
a una contraseña anterior válida. El máximo es de 128 caracteres.

Respuestas: `204` al confirmar el borrado; `400` para datos/confirmación inválidos;
`401` para sesión ausente o inválida; `403` para contraseña incorrecta o bloqueo;
`413` para cuerpo excesivo; `429` por límites y `409` si persiste un conflicto
concurrente. Sin un tipo JSON admitido puede responder `415`.

El límite `AccountDeletion` predeterminado permite tres solicitudes por minuto/IP,
además del global. Comparte `RateLimits:WindowSeconds` y admite configurar
`RateLimits:AccountDeletionPermitLimit`. Las llamadas intermediadas por Next.js
comparten su IP. Los fallos de contraseña comparten el contador y bloqueo de login.

En PostgreSQL una transacción bloquea la fila del propietario antes de revalidar
la sesión y contraseña actuales. El borrado usa las cascadas existentes y tiene
reintentos acotados ante conflictos o deadlocks. No requiere migración. Las
pruebas con EF InMemory cargan los dependientes para representar esas cascadas;
la comprobación real de PostgreSQL se registra aparte en el changelog.

La web usa una Server Action: el Bearer y la contraseña no se devuelven en estados
de error. Se limpian las cookies y se informa de éxito solamente después de un
`204`. Ante un error de conexión, la pantalla informa que no pudo confirmar el
resultado; conviene comprobar el estado antes de repetir.

[Exportación previa](ACCOUNT_EXPORT.md), [inventario de privacidad](../PRIVACY_NOTES.md)
y [validación efectuada](../CHANGELOG.md).
