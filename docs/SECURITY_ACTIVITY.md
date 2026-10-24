# Actividad privada de seguridad

Actualizado: 2026-10-24. Auditoría interna por cuenta.
No implementa monitoreo de intrusiones, alertas o una auditoría externa inalterable.

## Usarla desde la web

En **Mi cuenta**, elegí **Ver actividad de seguridad**. También está disponible
en `http://localhost:3000/account/activity` con una sesión válida.

Muestra hasta **50 eventos por consulta**, del más reciente al más antiguo, con
fecha en America/Asuncion y nombre de acción en español. **Ver eventos anteriores**
continúa el historial; **Ver más recientes** vuelve al comienzo de la selección.
No hay un límite total de 50 ni un número de página arbitrario. También podés
usar Atrás del navegador. Una consulta fallida se muestra como error, no como
historial vacío.

Elegí una acción y, opcionalmente, **Desde (UTC)** y **Hasta (UTC)**. Las fechas
incluyen días completos UTC: desde las 00:00 del primero hasta antes de las
00:00 del día siguiente al último. Las horas mostradas conservan America/Asuncion.
**Aplicar filtros** vuelve a los primeros resultados de esa selección;
**Limpiar filtros** vuelve a todo el historial. Un resultado sin coincidencias
se distingue de una cuenta sin actividad. **Actualizar resultados** vuelve al
comienzo de los filtros y permite ver los eventos nuevos.

La exportación de cuenta descarga toda la cuenta y actividad, sin aplicar los
filtros o la continuación de la pantalla.

El registro comienza con esta actualización: no se reconstruyen eventos pasados.
Una cuenta existente puede mostrar un historial vacío hasta su siguiente acción
registrada. Esto no demuestra que antes no haya habido accesos.

## Qué se registra

| Código de API/exportación | Acción completada |
| --- | --- |
| `account.registered` | Creación de cuenta con sesión inicial. |
| `session.started` | Login correcto posterior al registro. |
| `session.revoked` | Cierre mediante refresh o cierre individual confirmado desde el inventario de sesiones del propietario. |
| `sessions.revoked` | Cierre global de sesiones. |
| `session.replay-blocked` | Reutilización de un refresh consumido y cierre de la sesión aún no revocada. |
| `email.verified` | Verificación con un token válido de un uso. |
| `email.changed` | Cambio completado después de confirmar ambas direcciones y revalidar contraseña; incluye cierre de sesiones web. |
| `password.reset` | Restablecimiento de contraseña completado. |
| `password.changed` | Cambio autenticado desde Mi cuenta, con cierre de sesiones web e invalidación de recuperación pendiente. |
| `device.registered` | Registro de un dispositivo con su código inicial. |
| `device.code-issued` | Emisión posterior de un código de vinculación. |
| `device.paired` | Vinculación completada con una credencial nueva. |
| `device.revoked` | Revocación del acceso de un dispositivo. |
| `analysis.deleted` | Borrado individual confirmado de un análisis. |

Repetir un cierre/revocación ya completado o un token de un uso consumido no
agrega otro evento. No se registra cada refresh normal, heartbeat o informe.
La reutilización de refresh también puede deberse a solicitudes concurrentes;
el evento por sí solo no confirma una intrusión.

No se registran intentos fallidos de login, solicitudes de recuperación,
exportaciones, lecturas, feedback, operaciones de backup/purga ni todas las
peticiones HTTP. Esa cobertura y las alertas requieren otro bloque.

## Datos y privacidad

La migración `20261023175852_PrivateSecurityActivity` agrega `security_activity`
con **cuatro columnas**:

| Columna | Finalidad |
| --- | --- |
| `id_evento` | UUID de la entrada para ordenar empates e identificarla en la respuesta. |
| `id_usuario` | Propietario, con clave foránea y borrado en cascada. |
| `tipo_evento` | Uno de los catorce tipos cerrados del catálogo. |
| `fecha_evento_utc` | Fecha UTC obtenida del reloj del servidor. |

No hay texto libre, correo, nombre de equipo, IP, agente de usuario, URL,
contraseña, token, hash, identificador de sesión/dispositivo/análisis/reporte,
evidencia o comentario. Sigue siendo información personal por su relación con
la cuenta; no es anónima.

El evento genérico de borrado de análisis no identifica el contenido eliminado.
La marca mínima dispositivo/reporte/fecha conserva por separado su función contra
reingesta. Borrar la cuenta elimina también toda su actividad; no se conserva un
evento nuevo de cuenta borrada ni un identificador externo de ese usuario.

No hay purga automática de actividad mientras exista la cuenta. El mantenimiento
técnico de sesiones/tokens conserva estas entradas. La política de conservación
y una eventual limpieza acotada deben acordarse antes de producción. Descargas y
backups anteriores pueden conservar actividad aunque se elimine su cuenta.

## API y exportación

`GET /api/account/security-activity` exige Bearer de sesión activa de cuenta;
no admite la credencial Device. El propietario se obtiene del claim validado,
no de parámetros de consulta. Reutiliza el límite `DeviceAccount` por IP.
Solo expone GET; no hay endpoints de creación, edición o borrado de entradas.

La respuesta contiene `events`, `hasMore`, `limit: 50` y `nextBefore`. Cada evento devuelve
solo `id`, `action` y `occurredAtUtc`. La consulta usa un índice por propietario,
fecha descendente y UUID descendente y materializa como máximo 51 filas para detectar
eventos anteriores. `nextBefore` es el id del último evento devuelto cuando
`hasMore` es true; de lo contrario es null. Respuestas privadas con `no-store` y `nosniff`; la ruta web
también tiene `no-referrer`.

| Parámetro opcional | Uso |
| --- | --- |
| `action` | Uno de los catorce códigos exactos de la tabla anterior; vacío equivale a todos. |
| `from` | Primer día UTC incluido, formato `yyyy-MM-dd`. |
| `to` | Último día UTC incluido, formato `yyyy-MM-dd`; máximo `9999-12-30`. |
| `before` | UUID no vacío de continuación devuelto en la consulta anterior. Conservá los mismos filtros. |

Fechas imposibles/invertidas, acciones desconocidas, parámetros conocidos
repetidos y continuaciones inválidas reciben 400 sin reflejar el valor enviado.
Un evento de otra cuenta, inexistente o fuera de los filtros produce el mismo
error de continuación. Un parámetro `userId` no cambia el propietario de claims.

La continuación resuelve el evento dentro de la cuenta y los filtros y busca
fechas menores o, en un empate, UUID menores. Evita desplazar páginas por nuevas
entradas recientes y aprovecha el orden único del índice existente. Cada consulta
tiene su propia lectura: no constituye una instantánea persistente de todo el
recorrido. No usa offset ni carga todo el historial; tampoco escribe actividad al consultar.
La web reconstruye enlaces solo con los cuatro parámetros validados y mantiene
los filtros al avanzar. Si es necesario pasar por la página `/session/renew`, se
vuelve al historial inicial, como en el flujo anterior; la renovación proactiva
conserva la URL. No cambia esquema, categorías recopiladas o conservación.
El índice existente no incorpora el tipo de acción; la capacidad y rendimiento
con grandes volúmenes siguen pendientes de validación.
La ruta se excluye del log de solicitudes entrantes de Next en desarrollo para
no imprimir filtros o ids de continuación. Esto no configura proxies externos.

Patrón de referencia: [paginación de EF Core](https://learn.microsoft.com/en-us/ef/core/querying/pagination).

La [exportación de cuenta](ACCOUNT_EXPORT.md) agrega `securityActivity` con todas
las entradas propias, sin límite de presentación. Es un campo aditivo del formato
1; la web acepta también respuestas antiguas que todavía no incluyen el campo.
Conserva la instantánea `RepeatableRead` y excluye los datos internos de autenticación.

## Atomicidad y protección

El registro se añade al mismo `AppDbContext` y `SaveChanges` de la acción. Donde
hay varias escrituras, participa de la misma transacción. Si falla su inserción,
también se revierte la operación: no se informa una acción completada sin su
entrada. Los reintentos limpian el seguimiento antes de volver a registrar.

EF rechaza cambios o borrados de entradas existentes salvo al eliminar
su cuenta. PostgreSQL comprueba el catálogo de tipos y la relación con
el propietario. Las operaciones se coordinan con el manejo de conflictos de
cuenta/dispositivo; una cuenta eliminada no debe reaparecer por una carrera.

Estos controles no impiden que un administrador o código con SQL directo y los
permisos actuales modifique la tabla. No hay firma criptográfica, rol escritor
separado, almacén externo o garantía contra una base comprometida. Esos controles
y la revisión operativa permanecen en [pendientes](PENDING_FEATURES.md).

## Verificación

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-security-activity.ps1
```

Crea `scai_audit_test_<UUID>` vacío, aplica las migraciones y usa cuentas ficticias.
Comprueba fallos de escritura con rollback, separación de cuentas, cascadas y
carreras de login/revocación/borrado y continuación filtrada con fechas empatadas.
Retira la base al terminar. Los ensayos PostgreSQL
se omiten explícitamente en las pruebas habituales hasta ejecutar ese script.

La validación completa, incluidos frontend, E2E, backup de la tabla y preservación
de datos anteriores, se registra en [CHANGELOG.md](../CHANGELOG.md).
