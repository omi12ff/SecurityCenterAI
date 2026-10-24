# Mantenimiento de registros técnicos

Actualizado: 2026-10-24. Implementado para desarrollo local; la limpieza automática
está **desactivada por defecto**. Este bloque no define una política de conservación
del contenido del usuario ni completa la preparación para producción.

## Vista previa

Con PostgreSQL local disponible, desde la raíz del repositorio, en otra terminal:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\retention.ps1
```

Lee únicamente `POSTGRES_DB`, `POSTGRES_USER` y `POSTGRES_PASSWORD` del `.env` como
datos, compila en `RetentionTools` y consulta la base local en `127.0.0.1:5432`.
Puede convivir con la API de desarrollo. No requiere la clave JWT, no inicia HTTP
ni correo y no aplica migraciones. No imprime credenciales ni identificadores.

Después de la compilación muestra JSON con `status: "preview"`, fecha UTC,
fechas de corte, tamaño de lote, conteos `eligible` y `removed` en cero.
Las cinco categorías son `sessions`, `accountTokens`, `pairingCodes`,
`deviceCredentials` y `emailChanges`. Los conteos elegibles abarcan toda la base,
no una cuenta. El campo nuevo es aditivo y cuenta solicitudes, no correos enviados.

## Qué puede eliminar

Los plazos cuentan desde el estado que invalida el registro, no desde su creación.
Una fecha igual al corte también es elegible. Basta con cumplir una de las
condiciones de la fila correspondiente:

| Categoría | Condición con los valores predeterminados |
| --- | --- |
| Sesiones | Expiró hace al menos 30 días o fue revocada hace al menos 30 días. |
| Tokens de cuenta | Token de verificación/recuperación vencido o consumido hace al menos 7 días. |
| Cambios de correo | Solicitud vencida hace al menos 7 días; usa `AccountTokenDays`. |
| Códigos de vinculación | Vencido, consumido o revocado hace al menos 7 días. |
| Credenciales de dispositivos | Vencida o revocada hace al menos 30 días. |

Al eliminar una sesión cerrada antigua, PostgreSQL elimina en cascada sus
credenciales de renovación. **No se purgan por separado los hashes de refresh**:
incluso los consumidos permanecen mientras la sesión siga activa, para detectar
reutilización. Los conteos de sesiones no incluyen el número de hashes eliminados
por cascada; un lote acota las filas padre, no todas las filas dependientes.

Se conservan perfiles, dispositivos —incluidos revocados—, análisis, hallazgos,
feedback, catálogo de reglas, marcas mínimas de informes eliminados y actividad
privada de seguridad. Las marcas
siguen impidiendo reingesta con `410` mientras exista su dispositivo. La eliminación
individual y el [borrado de cuenta](ACCOUNT_DELETION.md) mantienen su alcance propio.

## Aplicación manual

Tras revisar la vista previa y acordar el plazo de conservación de la instalación:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\retention.ps1 -Action apply -ConfirmPurge
```

Es un borrado físico de registros técnicos de **todas las cuentas** de esa base.
`-Action apply` sin `-ConfirmPurge` falla antes de abrir la base. El helper habilita
la purga únicamente en su proceso y restaura las variables al terminar; no activa
el trabajo periódico de la API ni modifica `.env` o `appsettings.json`.

Una ejecución procesa como máximo `BatchSize` filas de cada categoría. Con el
valor por defecto son hasta 200 sesiones, 200 tokens, 200 códigos y 200 credenciales,
más las cascadas de refresh. Si quedan elegibles, se puede revisar otra vista previa
y repetir; no hay un bucle que vacíe todo el historial técnico en una orden.

La vista previa no reserva filas ni congela el tiempo para la aplicación posterior.
Cada ejecución calcula nuevos cortes; las filas elegibles pueden cambiar entre
ambas. La aplicación revalida la condición en el propio `DELETE`, usa una sola
transacción y coordina las instancias mediante un bloqueo asesor de PostgreSQL.
Si otra purga tiene el bloqueo, informa `busy`, con `eligible: null` y cero borrados.
Conflictos de serialización o deadlocks tienen hasta tres intentos transaccionales.

El comando interno del binario admite `--retention-preview`, o bien
`--retention-apply --confirm-retention` con `Retention__Enabled=true`. Sus códigos
de salida son 0 al terminar, 1 ante error, 2 por argumentos inválidos, 3 si está
ocupado y 130 por cancelación. El helper traduce un resultado distinto de cero
a error de PowerShell. No registra los detalles de conexión en esos errores.

## Configuración y trabajo periódico

Sección `Retention` de `src/SecurityCenterAI.Api/appsettings.json`:

| Opción | Predeterminado | Valores admitidos |
| --- | --- | --- |
| `Enabled` | `false` | `true` / `false` |
| `ClosedSessionDays` | 30 | 7 a 365 días |
| `AccountTokenDays` | 7 | 1 a 365 días |
| `PairingCodeDays` | 7 | 1 a 365 días |
| `DeviceCredentialDays` | 30 | 7 a 365 días |
| `BatchSize` | 200 | 1 a 1000 filas por categoría |
| `IntervalHours` | 24 | 1 a 168 horas |

Pueden configurarse como variables del proceso, por ejemplo
`Retention__ClosedSessionDays`. Los valores inválidos se rechazan. El helper de
vista previa fuerza `Enabled=false`; el de aplicación manual fuerza `true`.
Los demás ajustes se leen de la configuración habitual del binario.

En la API, `Enabled=true` habilita un trabajo periódico con esos mismos criterios.
Espera el intervalo completo antes de su primera ejecución: habilitarlo no purga
durante el arranque. Desactivado, no abre un ámbito de base de datos para este
trabajo. El log del trabajo solo agrega estado y conteos; no es una auditoría
durable. El bloqueo asesor coordina únicamente las purgas que usan este protocolo.

## Comprobaciones y límites

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-retention.ps1
```

Requiere Docker/PostgreSQL local y el `.env` de desarrollo. Crea una base vacía
`scai_retention_test_<UUID>`, carga exclusivamente datos ficticios y ejecuta
las pruebas PostgreSQL y del CLI. No copia datos de tu base. Al terminar elimina
solo esa base temporal. El parámetro `-DatabaseName` del helper está reservado a
este prefijo de pruebas; sin él se usa la base local del `.env`.

Comprueba lotes y cascadas reales, conservación de contenido y hashes activos,
exclusión entre purgas y revalidación si una sesión cambia entre selección y
borrado. Las pruebas sin PostgreSQL cubren cortes, cancelación, desactivación y
ausencia de purga al arrancar. En una corrida normal las tres pruebas PostgreSQL
se omiten explícitamente hasta ejecutar este script.

Los plazos predeterminados son una configuración técnica inicial, no una política
legal o de producción aprobada. Siguen pendientes conservación de contenido,
auditoría durable y operación de copias externas/recuperación real. Existe
[backup manual cifrado con ensayo aislado](DATABASE_BACKUPS.md). No se limpian
Mailpit, colas de correo, logs, informes/credenciales del agente, descargas ni
backups. Una copia anterior puede conservar filas purgadas y necesita tratamiento
propio; no hay reaplicación automática de borrados al restaurarla.
