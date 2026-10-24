# Revisar y cerrar sesiones web

Implementado el 2026-10-23. En **Mi cuenta → Ver sesiones activas**
(`http://localhost:3000/account/sessions`) podés revisar los accesos web y cerrar
una sesión específica, incluida la actual, con confirmación.

## Qué muestra

Una sesión se crea al registrarse o iniciar sesión. La lista incluye solo sesiones
propias, no revocadas y con vencimiento posterior a la consulta. Puede seguir
habilitada con el navegador cerrado: no demuestra conexión o presencia actual.
La sesión que autorizó la consulta aparece primero en la página 1. Las demás se
ordenan por creación e identificador descendentes, con 50 por página.

Cada entrada muestra creación y vencimiento en America/Asuncion. No se recopilan
IP, ubicación, navegador, sistema o última actividad para este listado. Los
identificadores técnicos se usan para seleccionar la sesión; no son credenciales.
No se muestran ni devuelven tokens, hashes o versiones de concurrencia.

La paginación es una consulta actual, no una instantánea entre páginas. Otros
accesos/cierres pueden desplazar filas; **Actualizar sesiones** vuelve a página 1.
La consulta admite páginas 1–10000 y distingue páginas vacías de errores.
La renovación de acceso vuelve a página 1 conservando el destino de sesiones.

Por defecto la cuenta admite diez sesiones habilitadas. La lista muestra el
límite que devuelve la API. Login con clave correcta recibe 409 si ya no hay
lugar; cerrar una sesión o recuperar la contraseña permite liberar acceso.
Renovar no ocupa un lugar adicional y las vencidas/revocadas no cuentan.
La reducción del límite no expulsa accesos anteriores automáticamente.
[Configuración y concurrencia](ACCOUNT_SECURITY_POLICY.md).

## Cerrar una sesión

Marcá la confirmación de la entrada y elegí **Cerrar esta sesión** o
**Cerrar mi sesión actual**. El servidor verifica propietario y confirmación.
El cierre invalida sus access tokens y la renovación, incluidas credenciales
rotadas; las siguientes solicitudes autenticadas son rechazadas. No cancela
operaciones que ya habían sido autorizadas antes del cierre.

Cerrar otra sesión conserva la actual. Cerrar la actual elimina las dos cookies
del navegador que hizo la operación y vuelve al login. En otros navegadores las
cookies pueden permanecer hasta su siguiente intento; no permiten recuperar el acceso.
Un error incierto pide actualizar el listado antes de confirmar el resultado.

También está disponible **Cerrar todas las sesiones**. Los equipos del agente
conservan su autorización independiente: se revocan desde **Dispositivos**.
Cerrar una sesión no borra cuenta, análisis o historial.

## API y persistencia

| Ruta | Contrato |
| --- | --- |
| `GET /api/account/sessions?page=1` | JWT Bearer activo; dueño y sesión actual de claims validados. Devuelve `sessions`, `page`, `limit: 50`, `hasMore`, `maxActiveSessions`. Cada entrada contiene `id`, `createdAtUtc`, `expiresAtUtc`, `isCurrent`. |
| `POST /api/account/sessions/{id}/revoke` | Bearer y cuerpo `{"confirmed":true}` de hasta 1 KiB. Devuelve 200 con `currentSession` calculado por la API. Sin confirmación: 400. Sesión ajena/inexistente: 404 equivalente. |

Comparten `DeviceAccount` y límite global por IP; respuestas sin caché y con
`nosniff`. Device no permite entrar a estas rutas. No se acepta un propietario
en query o cuerpo para autorizar la consulta o el cierre.

Reutiliza las tablas existentes, sin migración. Guarda revocación y evento
`session.revoked` en la misma transacción; repetir una revocación ya completada
no duplica el evento. Una sesión vencida propia tampoco agrega un cierre nuevo.
Los conflictos con renovaciones se resuelven con la versión de concurrencia.
La purga técnica mantiene su alcance y configuración desactivada por defecto.
El evento no conserva el identificador de la sesión.

No cambia el formato de exportación de cuenta: las tablas internas de sesiones
y credenciales siguen excluidas; el historial de actividad sí se exporta.
Siguen pendientes MFA, alertas, reautenticación para nuevas
acciones sensibles y validación de capacidad para operación externa.

Pruebas y alcance de este bloque en el [changelog](../CHANGELOG.md).
