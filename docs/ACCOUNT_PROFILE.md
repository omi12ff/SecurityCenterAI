# Editar el nombre del perfil

Implementado el 2026-10-23. En **Mi cuenta → Tu perfil**, editá **Nombre completo**
y elegí **Guardar nombre**. Se admiten de 2 a 100 caracteres después de quitar
espacios en los extremos. Se conservan los espacios interiores y los caracteres
internacionales; no se admiten saltos de línea o caracteres de control.

La confirmación aparece en la misma pantalla. Si no se puede confirmar el
resultado, actualizá la página para consultar el nombre guardado.

## Efectos y alcance

Solo cambia `users.nombre_completo` y su versión de concurrencia. No modifica
correo, contraseña, verificación, contenido o accesos. No cierra sesiones ni
renueva tokens. El dashboard consulta el perfil guardado, igual que Dispositivos:
al volver a cargar esas páginas se ve el nombre nuevo incluso con un JWT anterior.
Una pestaña ya abierta conserva su pantalla hasta actualizar o navegar.

Los claims del JWT siguen siendo una instantánea hasta su renovación normal;
no se usa el nombre del token para autorizar o mostrar el perfil actual. La
exportación de cuenta devuelve el nombre actual guardado y mantiene su formato.
No se reescriben informes o entradas anteriores.

Es una edición de presentación. No agrega una entrada al catálogo de
eventos de seguridad ni envía correo. Los logs de API mantienen su política de
campos mínimos, sin cuerpo o nombre introducido. El [cambio de correo](ACCOUNT_EMAIL_CHANGE.md)
está disponible en un flujo separado con doble confirmación y reautenticación.

## Contrato y seguridad

`POST /api/account/profile/name`, con Bearer activo y cuerpo `{"name":"Nombre"}`
de hasta 2 KiB. El valor sin recortar admite hasta 200 caracteres para permitir
espacios exteriores; el nombre final queda limitado a 100. Responde 204, 400 por
datos inválidos, 401 si la sesión dejó de estar disponible, 409 por conflicto
persistente o 429 por límites. Comparte `DeviceAccount` y límite global por IP.
La respuesta del controlador no se almacena en caché y lleva `nosniff`.

El dueño y sesión vienen de claims validados; no se autoriza mediante ids del
cuerpo. Campos adicionales no pueden cambiar otros datos del perfil. En
PostgreSQL se bloquea la fila de usuario, se comprueba de nuevo la sesión y se
coordina la versión con cambios sensibles. Reintentos acotados ante conflictos;
repetir el mismo nombre no genera otra escritura.

La web trata el nombre como texto, incluido cualquier texto que parezca HTML.
No se agregan campos, tablas, dependencias o migraciones.
Pruebas y límites de verificación en el [changelog](../CHANGELOG.md).
