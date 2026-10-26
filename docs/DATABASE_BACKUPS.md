# Copia cifrada y ensayo de recuperación

Actualizado: 2026-10-25. Herramienta manual para PostgreSQL 16 local de Docker
Compose en Windows. Incluye restauración temporal y ensayo de invalidación de accesos; la programación y la
recuperación real del servicio siguen requiriendo su procedimiento operativo.

## Crear una copia

Con Docker/PostgreSQL disponibles, desde la raíz del proyecto, en otra terminal:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\backup.ps1
```

El helper compila en `BackupTools`, separado de la API activa, y pide dos veces
una contraseña de copia de entre 16 y 1024 caracteres. Elegí una frase larga y
guardala fuera del repositorio. No la escribas como argumento, en `.env` o en un
chat. La herramienta no guarda esa contraseña y no puede recuperarla.

Al terminar muestra JSON con `status: "created-and-verified"`, ruta `file`, fecha
UTC de la instantánea y número de tablas comprobadas. Solo publica el nombre
final si el archivo escrito se pudo descifrar, restaurar y comparar correctamente.

Las copias se guardan en:

```text
%LOCALAPPDATA%\SecurityCenterAI\Backups\SecurityCenterAI-<fecha-UTC>-<UUID>.scaibackup
```

Esa carpeta tiene permisos explícitos para el usuario Windows actual y SYSTEM.
La creación admite `-Directory` únicamente dentro de esa carpeta dedicada; rechaza
repositorio, OneDrive y rutas con uniones/enlaces. No modifica permisos de carpetas
ajenas. Git también ignora `.scaibackup` y `.scaibackup.partial`.

La copia permanece cifrada con contraseña, independientemente de la identidad
Windows. Los permisos de carpeta son una protección local adicional; no son DPAPI
ni sustituyen la custodia de la contraseña o una copia fuera de esta PC.

## Comprobar una copia existente

Usá la ruta `file` informada al crearla:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\backup.ps1 -Action verify -File 'C:\ruta\copia.scaibackup'
```

Pide la contraseña y devuelve `status: "verified"` si coincide el inventario.
La verificación no necesita que exista la base original: utiliza `postgres` para
comprobar el servidor y crea su propio destino temporal. Requiere conservar el
repositorio/herramienta, Docker con PostgreSQL 16 y los datos de conexión locales.

El ensayo crea una base nueva `scai_restore_test_<UUID>`, restringe conexiones de
PUBLIC, restaura allí y la elimina al terminar. No ofrece un parámetro para
elegir otra base de destino, no usa `--clean` y no apunta la API a la copia.
En cada ejecución puede existir una sola base temporal de ese ensayo.

La autenticación del archivo ocurre **antes** de crear la base temporal. Una
contraseña incorrecta o un archivo alterado no llegan a `pg_restore`. Si falla la
comparación posterior a restaurar, tampoco se informa éxito y se retira la base.

Para ampliar la comprobación con invalidación de sesiones, enlaces, agentes y
cola exclusivamente en la temporal, usar `-Action rehearse` con el mismo `-File`.
Verifica primero el inventario y luego la conservación del contenido después de
retirar accesos. No conserva la temporal ni modifica el archivo de copia.
Requiere el esquema revisado actual; copias anteriores pueden seguir admitiendo
`verify` aunque no `rehearse`. [Comando, resultado y límites](RECOVERY_REHEARSAL.md).

## Contenido y consistencia

Es una copia administrativa de la base, distinta de la exportación de cuenta.
Incluye usuarios y hashes de contraseñas, sesiones, hashes de tokens/credenciales,
dispositivos, análisis, hallazgos, feedback, catálogo, marcas mínimas y migraciones.
Incluye también la tabla de actividad privada de seguridad incorporada después.
La cola `account_email_outbox` se incluye con el contenido ya cifrado. El anillo
DPAPI de correo no forma parte del dump ni del archivo de copia: permanece bajo
el usuario Windows de la API. Una recuperación real debe retirar pendientes y
tokens restaurados antes de habilitar SMTP; no se reproducen correos antiguos
como parte del ensayo aislado. [Custodia](MAIL_DELIVERY.md).
Debe custodiarse como información sensible aunque esté cifrada.

Una transacción de lectura exporta una instantánea PostgreSQL. El inventario y
`pg_dump --snapshot` leen esa misma instantánea, incluso si hay escrituras
concurrentes. El dump usa formato custom; se transporta por flujos binarios de
.NET y no por redirección de texto de Windows PowerShell.

El inventario cifrado contiene SHA-256 y conteos de cada tabla pública y una
huella del esquema actual: columnas/defaults, índices y restricciones. Las filas
se ordenan por su JSON canónico antes de calcular la huella. Para CHECK se compara
la expresión normalizada por PostgreSQL mediante EXPLAIN sin ANALYZE: el servidor
puede representar de manera equivalente los casts de un arreglo al restaurarlo.
No se omiten las restricciones por esa diferencia textual.

`pg_restore` usa una transacción y detiene el ensayo ante errores. La comparación
lee el esquema y las filas realmente restaurados, no solo el índice del archivo.
El dump también tiene una huella SHA-256 dentro del contenido autenticado.

## Cifrado, límites y fallos

Formato propio versionado `SCAIBK01`: sal aleatoria de 32 bytes, nonce de 12,
longitud, etiqueta de 16 y contenido cifrado. Usa AES-256-GCM y una clave de 32 bytes
derivada mediante PBKDF2-HMAC-SHA256 con 600 000 iteraciones. La cabecera completa
es dato asociado autenticado. No hay contraseña o clave persistida junto al archivo.

El contenido descifrado combina manifiesto JSON y dump. El límite de **64 MiB**
incluye ambos; esta herramienta inicial trabaja en memoria y puede necesitar
varias veces ese espacio. No pretende cubrir bases grandes o recuperación continua.
No escribe un dump descifrado a disco. La RAM, paginación, diagnósticos del sistema
y el volumen PostgreSQL tienen protecciones independientes del archivo cifrado.

La contraseña viaja por la entrada estándar del proceso hijo; la conexión se
prepara en su entorno. No se incluyen contraseñas en argumentos ni se imprimen
errores de SQL o conexión. .NET/PowerShell necesitan representaciones transitorias
en memoria; esto no impide que un administrador o proceso comprometido las lea.

El comando interno retorna 0 al completar, 1 por fallo general, 2 por argumentos
inválidos, 4 si no se pudo confirmar creación/retirada temporal, 5 por autenticación
del archivo fallida y 130 por cancelación/tiempo. El helper traduce los fallos en
errores PowerShell. Cada comando Docker tiene plazo y la operación completa, cinco
minutos. El retiro temporal usa su propio plazo aunque se cancele el trabajo.

Un cierre abrupto del proceso, Docker o Windows puede impedir el bloque de
limpieza. Si se informa el código 4, revisá la base temporal indicada. Tras una
interrupción podés consultar en DBeaver:

```sql
SELECT datname FROM pg_database
WHERE datname ~ '^scai_restore_test_[a-f0-9]{32}$';
```

Revisá y retirá solo destinos temporales identificados de ese ensayo. La API no
debe configurarse para usarlos. Solo deben verificarse archivos de una base cuyo
administrador sea de confianza: restaurar un dump ejecuta las instrucciones SQL
incluidas por el administrador de origen.

## Pruebas aisladas

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-backup.ps1
```

Crea una base `scai_backup_fixture_<UUID>` vacía y aplica las migraciones sobre
ella. Usa exclusivamente datos ficticios, prueba cifrado/restauración, escrituras
concurrentes, contraseñas erróneas, alteración de archivos, inventario incoherente,
origen inexistente y ausencia de restauración sobre el origen después de borrar
un análisis. Prueba también el CLI con una contraseña Unicode y retira sus bases
y archivos. No copia datos personales de la base habitual.

También comprueba que cambiar una restricción CHECK altera la huella de esquema
sin cambiar las huellas de las filas, y revierte ese cambio de prueba.
La suite añade cinco ensayos de preparación: rechazo real de accesos por API,
nuevo login, conservación, rollback, destinos/esquemas desconocidos y limpieza.
El CLI también ejecuta `rehearse`. En las pruebas habituales del backend, los ocho ensayos PostgreSQL se omiten
explícitamente hasta ejecutar este script. Los casos de formato y cifrado no
requieren Docker. `-DatabaseName` del helper está reservado al prefijo de fixtures.

## Recuperación real pendiente

No se automatizan calendarios, conservación/rotación, copias remotas, recuperación
a un instante ni sustitución de la base activa. Tampoco se incluyen roles/grants
del clúster, `.env`, claves JWT, correo/Mailpit, logs o archivos del agente. Guardar
una copia en esta misma PC no protege frente a la pérdida de la PC.

Una copia antigua conserva datos borrados después y puede conservar hashes de
accesos antes revocados. Antes de reabrir un servicio recuperado deben resolverse
los borrados posteriores e invalidarse los accesos restaurados, con responsables
y pérdida/tiempo de recuperación acordados. `rehearse` prueba su invalidación
solo en una temporal retirada; no concilia borrados ni sustituye el servicio.
La política operativa se mantiene en
[pendientes](PENDING_FEATURES.md).

Referencias utilizadas: [pg_dump de PostgreSQL 16](https://www.postgresql.org/docs/16/app-pgdump.html),
[pg_restore](https://www.postgresql.org/docs/16/app-pgrestore.html) y
[AES-GCM de .NET](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm?view=net-8.0).
