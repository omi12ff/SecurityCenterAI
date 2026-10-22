# Security Center AI

Copiloto para la seguridad, salud y mantenimiento de equipos Windows.
El repositorio contiene una API .NET 8 con PostgreSQL y una web Next.js para
cuentas, sesiones y dashboard con datos de la API.

**Estado al 21 de octubre de 2026:** base interna **0.1** y avances hacia
**0.5**. A cuentas y sesiones se suma el bloque de dispositivos y la base de
reglas y hallazgos. La consola Windows ejecuta diez comprobaciones de solo lectura
y permite enviarlas explícitamente a la API. El servidor valida el informe,
calcula puntos confirmados y cobertura, y la web muestra el historial del equipo.
La validación de cada bloque se registra en [CHANGELOG.md](CHANGELOG.md).
La beta sigue incompleta: faltan las explicaciones de IA, la distribución y
operación del agente y los controles de producción.
Todavía no hay una versión pública etiquetada ni un despliegue de esta web.

## Documentación del proyecto

Para recordar dónde nos quedamos, empieza por [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md).

| Documento | Qué encontrarás |
| --- | --- |
| [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md) | Visión, estado real y siguiente paso. |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Componentes actuales, flujos y arquitectura prevista. |
| [ROADMAP.md](ROADMAP.md) | Etapas, prioridades y criterios para avanzar. |
| [DECISIONS.md](DECISIONS.md) | Decisiones técnicas y de producto con sus motivos. |
| [SECURITY.md](SECURITY.md) | Controles actuales, pendientes y reporte de problemas. |
| [PRIVACY_NOTES.md](PRIVACY_NOTES.md) | Datos que se guardan y límites actuales de privacidad. |
| [CHANGELOG.md](CHANGELOG.md) | Historial de cambios y verificaciones realizadas. |
| [Dispositivos y vinculación](docs/DEVICES.md) | Alcance, credenciales y límites del bloque de dispositivos. |
| [Agente Windows](docs/WINDOWS_AGENT.md) | Cómo vincular la consola, realizar comprobaciones locales y proteger su credencial. |
| [Enviar y consultar análisis](docs/ANALYSIS_INGESTION.md) | Envío manual, reintentos, puntos confirmados, cobertura y límites de la evidencia. |
| [Guía maestra original (PDF)](docs/Guia_Maestra_Security_Center_AI_Actualizada.pdf) | Referencia del producto, versión documental 1.0 del 27 de julio de 2026. |

## Requisitos

- .NET SDK 8.0.4xx (seleccionado por `global.json`; incluye el runtime de la API).
- Docker Desktop con el motor WSL 2 activo y contenedores Linux.
- PowerShell 5.1 o superior y Git.
- Node.js 24 y npm para el frontend.

Extensiones de VS Code: **C#**, **C# Dev Kit** y **Container Tools**
(`ms-azuretools.vscode-containers`). Para el frontend: **ESLint**
(`dbaeumer.vscode-eslint`) y **Prettier** (`esbenp.prettier-vscode`).

## Ejecutar el backend en desarrollo

1. Abre Docker Desktop y espera que el motor esté listo.
2. Copia `.env.example` como `.env` si todavía no existe. Configura una contraseña
   fuerte para PostgreSQL y una `JWT_KEY` aleatoria de al menos 32 caracteres.
   Usa una variable por línea (`NOMBRE=valor`), sin interpolación ni comandos;
   puedes encerrar el valor completo entre comillas simples o dobles.
3. Desde la raíz del repositorio ejecuta:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev.ps1
   ```

Este comando permite ejecutar el script aunque la política actual sea
`Restricted`; el cambio de política se limita a ese proceso de PowerShell.
El script inicia los servicios locales, espera a PostgreSQL, restaura la
herramienta local `dotnet-ef`, compila la API una vez, aplica las migraciones
e inicia la API con esos binarios.
Swagger: <http://localhost:5169/swagger>. Detén la API con `Ctrl+C`.
PostgreSQL conserva sus datos en el volumen de Docker.
El perfil local utiliza HTTP; la redirección a HTTPS se habilita fuera de
`Development`.

Si la misma API ya está activa en el puerto elegido, repetir el comando muestra
su dirección y termina correctamente, sin recompilar ni aplicar migraciones.
Para cargar cambios y reiniciar esa instancia del proyecto, usa:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev.ps1 -Restart
```

El reinicio comprueba la ruta del ejecutable y espera que termine antes de
compilar. Una aplicación ajena que ocupe el puerto requiere elegir otro puerto.

El helper fija `ASPNETCORE_ENVIRONMENT=Development` solo para su proceso y lo
restaura al terminar. Es un comando de desarrollo, no de despliegue. Permite
elegir `-Configuration` (por defecto `Debug`) y `-Port` (por defecto `5169`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev.ps1 -Configuration AccountQa -Port 5179
```

Una configuración de compilación separada evita compartir binarios con otra API
activa; el puerto alternativo evita ocupar su dirección. Configura también
`API_BASE_URL` en el servidor Next.js si usas esa instancia.
Si cambias el puerto de la web, alinea `FrontendUrl` en el proceso de la API:
esa URL construye los enlaces de correo, además de configurar el origen CORS.

Para aplicar migraciones con PostgreSQL ya en ejecución, o ejecutar las pruebas:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev.ps1 -Action migrate
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev.ps1 -Action test
```

Las 82 pruebas del backend usan una base en memoria y correo de prueba, sin Docker
ni `.env`. Comprueban autenticación, tokens, autorización y aislamiento del
dashboard. La solución incorpora además las pruebas específicas del agente
Windows, descritas en [su guía](docs/WINDOWS_AGENT.md#comprobar-el-agente).
La suite E2E del frontend utiliza PostgreSQL y correo local reales,
como se describe abajo. Los resultados concretos están en
[CHANGELOG.md](CHANGELOG.md).

Para detener los servicios locales conservando el volumen de PostgreSQL,
ejecuta `docker compose stop`.

`.env` configura Docker Compose; .NET no lo carga automáticamente. El script lee
los cuatro valores de `.env` y prepara `ConnectionStrings__PostgreSQL` y
`Jwt__Key` únicamente para el proceso de desarrollo; al terminar restaura el
entorno anterior. La conexión utiliza `localhost:5432`. No subas `.env` a Git.

La contraseña de PostgreSQL se fija al crear su volumen por primera vez. Cambiar
solo `.env` después no modifica la contraseña del usuario existente.

El bloque de cuentas añade la migración `20261021114159_AccountSessionsAndRecovery`
a la inicial. Conserva las cuentas y análisis y agrega campos de seguridad y
tablas de tokens/sesiones. Antes de aplicarla sobre datos que necesites conservar,
prepara una copia de la base. Después vuelve a iniciar sesión: los JWT anteriores
no tienen `sid` ni refresh. Las cuentas existentes aparecen con correo pendiente
y pueden solicitar su enlace desde `/account`; el dashboard sigue disponible.
Si conservas una API iniciada antes de estos cambios en `5169`, reiníciala para
que cargue el código nuevo. La instancia previa del usuario se dejó intacta
durante las pruebas; la instancia de QA utilizó `5179` y ya se detuvo.

## Ejecutar el frontend en desarrollo

Con el backend activo, abre otra terminal en la raíz del repositorio:

```powershell
npm.cmd --prefix src/SecurityCenterAI.Web ci
npm.cmd --prefix src/SecurityCenterAI.Web run dev
```

Abre <http://localhost:3000>. La web incluye `/register`, `/login`, `/dashboard`,
`/account`, `/devices`, `/forgot-password`, `/reset-password` y `/verify-email`.
Dashboard y cuenta requieren sesión. Las pantallas muestran datos de la API y
un estado vacío cuando no existen análisis. La web consulta los resultados
enviados explícitamente desde el agente; no ejecuta comprobaciones por sí sola.

La web usa Next.js 16.4.2, React 19.3.1 y TypeScript 5.9.3. `API_BASE_URL` es una
variable exclusiva del servidor Next.js y usa `http://localhost:5169` por defecto.
Para otra dirección, configúrala en `src/SecurityCenterAI.Web/.env.local` y reinicia
la web. La clave de firma JWT y las credenciales de PostgreSQL pertenecen al
backend; el frontend solo necesita la dirección de la API.

Las Server Actions gestionan la cuenta y conservan access token y refresh token
en cookies `HttpOnly`, `SameSite=Lax`, con `Secure` en producción. No se utiliza
`localStorage`. El backend verifica que la sesión asociada siga activa: cerrar
o revocar una sesión invalida también sus JWT. La renovación rota el refresh
token; reutilizar uno anterior revoca esa sesión. `/account` permite consultar
el estado del correo y cerrar todos los accesos, incluido el actual. Todavía
no hay un listado de sesiones ni revocación individual desde esa pantalla.

## Correo de desarrollo y recuperación

El entorno local usa Mailpit para capturar los correos de verificación y
recuperación. Abre <http://localhost:8025> para consultar la bandeja después de
registrarte, solicitar un reenvío desde cuenta o usar «Olvidé mi contraseña».
Mailpit recibe SMTP en `127.0.0.1:1025`; no entrega esos mensajes a una bandeja
externa. Los enlaces llevan a la web local y requieren confirmar la acción.

Los tokens de correo son de un uso: verificación durante 24 horas y recuperación
durante 30 minutos. Restablecer la contraseña revoca las sesiones de la cuenta y
requiere volver a iniciar sesión. La respuesta a recuperación es genérica aunque
el correo no esté registrado. El reenvío tiene una espera de 60 segundos por
cuenta y tipo de correo; también hay límites de solicitudes. Si SMTP falla, la
respuesta genérica no confirma entrega: revisa que Mailpit esté saludable y
solicita otro enlace después de la espera. La cola de correo es local y volátil.

Mailpit muestra enlaces que permiten actuar sobre cuentas: úsalo con cuentas
ficticias, mantenlo local y no compartas mensajes ni capturas con sus tokens.
Su almacenamiento y los datos tratados están descritos en
[PRIVACY_NOTES.md](PRIVACY_NOTES.md). Fuera de `Development`/`Testing` se requiere SMTP
configurado con TLS y una `FrontendUrl` HTTPS; no se ha elegido ni contratado
un proveedor de correo de producción.

## Dispositivos

En `/devices` puedes dar un nombre a un dispositivo, solicitar o regenerar
su código de vinculación y revocarlo. Crear dispositivos y códigos requiere
correo verificado y se permite uno no revocado por cuenta, incluso si todavía
está pendiente de vincular. Verifica la dirección desde `/account` y Mailpit
antes de probar ese recorrido local.

El código dura diez minutos y es de un uso. Al canjearlo, el cliente recibe una
credencial propia de 90 días, separada de la sesión web. Regenerar invalida
códigos anteriores; una credencial existente sigue funcionando hasta canjear
el nuevo código. Para bloquearla de inmediato, revoca el dispositivo.

El [agente Windows de consola](docs/WINDOWS_AGENT.md) permite canjear ese código
y enviar un heartbeat manual. La web mostrará el equipo vinculado y su último
contacto después de actualizar la página. `scan` guarda las diez comprobaciones
localmente; el envío requiere `scan -Send` o `send` explícitos. El resultado
aparece en **Ver equipo e historial** y en el resumen del dashboard.
Un dispositivo `linked` tiene una credencial válida; no acredita que esté online
o que tenga un resultado favorable. El protocolo está en
[docs/DEVICES.md](docs/DEVICES.md).

Restablecer la contraseña revoca también las credenciales y códigos de
dispositivo; conserva su registro y requiere un código nuevo para vincularlo.
«Cerrar todas las sesiones» solo afecta las sesiones web.

Con la API y la web activas, abre otra terminal en la raíz del proyecto:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\agent.ps1 -Action scan -Send
```

Para enviar o reintentar el último informe compatible sin volver a comprobar Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\agent.ps1 -Action send
```

Los informes anteriores del esquema 1 requieren un `scan` nuevo. El puntaje
representa puntos confirmados sobre diez criterios limitados; léelo junto con
la cobertura y los desconocidos. No es una certificación de seguridad del PC.
Los reintentos conservan el mismo identificador para no duplicar el historial.
Detalles, datos enviados y recuperación: [guía de ingesta](docs/ANALYSIS_INGESTION.md).

## Comprobar la aplicación

Para comprobar el frontend desde la raíz:

```powershell
npm.cmd --prefix src/SecurityCenterAI.Web run typecheck
npm.cmd --prefix src/SecurityCenterAI.Web test
npm.cmd --prefix src/SecurityCenterAI.Web run build
```

`typecheck` genera los tipos de Next.js y ejecuta TypeScript; funciona también
desde una copia recién instalada. `test` ejecuta pruebas unitarias sin
servidores. `build` genera la compilación que sirve
`npm.cmd --prefix src/SecurityCenterAI.Web start`. Estos comandos no
publican el sitio; el despliegue y TLS de producción siguen pendientes.

Para ejecutar las pruebas E2E con **backend, frontend, PostgreSQL y Mailpit
activos**, abre una tercera terminal desde la raíz:

```powershell
cd src/SecurityCenterAI.Web
npx.cmd playwright install chromium
npm.cmd run test:e2e
```

La suite E2E usa Chromium, solicitudes HTTP/SMTP reales y cuentas
únicas `e2e-…@example.invalid`. La limpieza se limita al correo creado por cada
prueba y requiere Docker en la
ruta de comandos. El caso de historial inserta siete registros sintéticos en
PostgreSQL para comprobar el total y el límite de cinco resultados; no genera
análisis de la PC. Al eliminar la cuenta se borran sus registros en cascada.
Las trazas, vídeos y capturas automáticas están deshabilitados. La revisión
visual puede habilitar capturas de cuentas ficticias con `SC_AI_VISUAL_REVIEW=1`;
es opcional y no hace falta para ejecutar la suite.

Para probar servicios en otras direcciones, configura estas variables en la
terminal que ejecuta Playwright:

| Variable E2E | Valor predeterminado | Finalidad |
| --- | --- | --- |
| `E2E_BASE_URL` | `http://localhost:3000` | Web visitada por Chromium y origen esperado de los enlaces de correo. |
| `E2E_API_BASE_URL` | `http://localhost:5169` | API de pruebas. |
| `E2E_MAILPIT_URL` | `http://localhost:8025` | Bandeja de prueba y limpieza de mensajes por destinatario exacto. |

La API debe usar la misma dirección de web en `FrontendUrl` y la web debe usar
la API elegida en `API_BASE_URL`. Durante la verificación del 2026-10-21 se
usaron web `3001` y API `5179` para separar la instancia de QA.

Los resultados de las suites y las revisiones de cada bloque se registran en
[CHANGELOG.md](CHANGELOG.md). Son comprobaciones locales; no publican la web
ni completan la beta 0.5.

## Consultar PostgreSQL con DBeaver

Con Docker Desktop abierto y el contenedor iniciado, crea una conexión de tipo
PostgreSQL con estos datos:

| Campo | Valor |
| --- | --- |
| Host | `127.0.0.1` |
| Puerto | `5432` |
| Base de datos | Valor de `POSTGRES_DB` en `.env` (por defecto, `security_center_ai`). |
| Usuario | Valor de `POSTGRES_USER` en `.env` (por defecto, `security_center`). |
| Contraseña | Valor de `POSTGRES_PASSWORD` en `.env`. |

Prueba la conexión y acepta la descarga del driver si DBeaver la solicita.
Dentro de la conexión, abre `Schemas > public > Tables`. Encontrarás `users`,
`security_analyses`, `user_sessions`, `refresh_credentials`,
`account_action_tokens` y `__EFMigrationsHistory` tras aplicar las migraciones.
El bloque de dispositivos añade `devices`, `device_pairing_codes`,
`device_credentials`, `security_rules` y `security_findings`, y la columna
opcional `security_analyses.id_dispositivo`.
Para ver registros, usa el menú
contextual de una tabla: `View Data > All Rows`.

La API puede estar detenida mientras consultas la base; el contenedor de
PostgreSQL debe seguir activo. Si necesitas iniciarlo por separado:

```powershell
docker compose up -d --wait postgres
```

### Columnas de users

Las once columnas de `users` usan nombres en español mediante un mapeo de EF
Core. Las propiedades C# y los contratos JSON existentes conservan sus nombres;
por ejemplo, el nombre del perfil sigue llegando como `name` en la API.

| Columna en PostgreSQL | Propiedad C# | Qué significa |
| --- | --- | --- |
| `id_usuario` | `Id` | Identificador único de la cuenta, de tipo UUID. |
| `nombre_completo` | `Name` | Nombre que se indicó al registrarse. |
| `correo_electronico` | `Email` | Correo de acceso, normalizado y único. |
| `hash_contrasena` | `PasswordHash` | Hash usado para verificar la contraseña; no contiene una contraseña legible. |
| `fecha_creacion_utc` | `CreatedAtUtc` | Momento de creación de la cuenta en UTC. |
| `correo_verificado` | `EmailVerified` | Indica si se confirmó la dirección mediante su enlace. |
| `intentos_acceso_fallidos` | `FailedLoginAttempts` | Contador usado para el bloqueo temporal de login. |
| `fecha_fin_bloqueo_utc` | `LockoutEndUtc` | Hasta cuándo dura el bloqueo; puede estar vacía. |
| `fecha_ultima_solicitud_recuperacion_utc` | `LastPasswordResetRequestedAtUtc` | Última solicitud de recuperación, para limitar reenvíos. |
| `fecha_ultimo_envio_verificacion_utc` | `LastVerificationSentAtUtc` | Último envío de verificación, para limitar reenvíos. |
| `version_concurrencia` | `ConcurrencyStamp` | UUID técnico que detecta cambios simultáneos; no es la versión de la aplicación. |

El cambio se aplica con `20261021120952_RenameUserColumnsToSpanish` y conserva
los datos. También renombra el índice único de correo a
`IX_users_correo_electronico`.
La tabla sigue llamándose `users` y las demás tablas mantienen sus columnas.
Después de migrar, actualiza la vista de DBeaver para ver los encabezados nuevos.
No traduzcas columnas manualmente: el mapeo y la migración deben coincidir.
La API que estaba abierta antes del renombrado debe reiniciarse para cargar
los mapeos nuevos: detenla con `Ctrl+C` y vuelve a ejecutar `scripts/dev.ps1`.

## Problemas frecuentes al ejecutar

- Si `docker` o `node` no se reconocen después de instalarlos, abre una terminal
  nueva o reinicia VS Code.
- Si Docker indica que no puede conectar con el motor, abre Docker Desktop y
  espera a que termine de iniciar.
- Si falla la autenticación de PostgreSQL, comprueba que `.env` coincide con las
  credenciales usadas al crear el volumen. Conserva los datos mientras revisas
  la configuración.
- Si aparece un error de columna en `users`, compara los nombres con la última
  migración y el mapeo de EF: la propiedad `Name` utiliza `nombre_completo`.
  Una API anterior que todavía busque `"Name"` requiere reiniciarse con el código
  nuevo. La reparación histórica de `"NOMBRE"` a `"Name"` ocurrió antes de esta
  traducción planificada y se conserva en el changelog. «Database is already up
  to date» solo informa del historial de migraciones; no repara cambios manuales.
- Si Swagger no responde, ejecuta el script de inicio y revisa su salida.
  `/health` confirma que la API responde; no verifica la conexión con PostgreSQL.
- Si el puerto ya está ocupado o hay binarios bloqueados por otra API, identifica
  la instancia que necesitas conservar. El helper reconoce la API de este
  proyecto y permite reiniciarla con `-Restart`. Para otra instancia paralela,
  usa `-Port` y `-Configuration` distintos: cambiar solo el puerto no libera DLL.
  `migrate` y `test` también avisan si la configuración elegida está en uso;
  detén esa API o elige otra configuración antes de compilar.
- Si la web muestra que no pudo conectar con el servicio, comprueba que la API
  responde en la dirección de `API_BASE_URL`. Los formularios llaman al backend
  desde el servidor Next.js.
- Si el login informa que solo se cerró este navegador, la API no confirmó la
  revocación. Las cookies locales se borraron; vuelve a entrar cuando el servicio
  esté disponible y usa «Cerrar todas las sesiones» si necesitas revocar copias.

## Contrato de la API usado por la web

| Ruta | Acceso y finalidad |
| --- | --- |
| `POST /api/auth/register` | Público: crea cuenta y sesión; solicita correo de verificación. |
| `POST /api/auth/login` | Público: valida credenciales y crea sesión. |
| `POST /api/auth/refresh` | Refresh opaco en el cuerpo: rota la credencial y entrega nuevos tokens. |
| `POST /api/auth/logout` | Refresh opaco en el cuerpo: revoca su sesión. |
| `POST /api/auth/logout-all` | Bearer: revoca todas las sesiones de la cuenta. |
| `GET /api/auth/me` | Bearer: perfil y estado de correo actuales. |
| `POST /api/auth/resend-verification` | Bearer: solicita otro enlace, sujeto a espera y límites. |
| `POST /api/auth/verify-email` | Token de correo en el cuerpo: confirma dirección. |
| `POST /api/auth/forgot-password` | Público: solicita enlace con respuesta genérica. |
| `POST /api/auth/reset-password` | Token y nueva contraseña: consume enlace y revoca sesiones. |
| `GET /api/dashboard` | Bearer: análisis guardados propios. |
| `GET /health` | Público: respuesta fija de salud HTTP. |

Las rutas de dispositivos y los endpoints del agente están
documentados en [docs/DEVICES.md](docs/DEVICES.md#contrato-http). El detalle
devuelve hasta 20 análisis y 100 hallazgos por análisis. `POST /api/agent/analyses`
permite la ingesta manual mediante credencial Device, con el contrato descrito
en [la guía de envío](docs/ANALYSIS_INGESTION.md).

El dashboard consulta análisis guardados. El servidor calcula los puntos
confirmados al aceptar resultados del agente, sin ejecutar consultas remotas
de Windows. El detalle de los flujos está en [ARCHITECTURE.md](ARCHITECTURE.md).

Los secretos de producción deben configurarse mediante variables de entorno:
`ConnectionStrings__PostgreSQL`, `Jwt__Key`, `Email__Username` y
`Email__Password`. La configuración de correo también utiliza `Email__Host`,
`Email__Port`, `Email__From`, `Email__EnableSsl` y `FrontendUrl`.
Nunca subas secretos al repositorio. Estas opciones no sustituyen preparar TLS,
custodia de datos, correo y el resto de los controles de publicación.

## Flujo Git recomendado

- `main`: versiones estables.
- `develop`: integración.
- `feature/backend-auth`: trabajo de autenticación.
- `feature/frontend-auth`: pantallas e integración del frontend.
