# Decisiones de Security Center AI

Registro actualizado al **2026-10-21**. Una decisión aceptada como dirección del
producto no implica que esté implementada. La [Guía maestra del proyecto](docs/Guia_Maestra_Security_Center_AI_Actualizada.pdf), versión
1.0 del **2026-07-27**, es la fuente de los objetivos que se identifican como tales.
Las fechas siguientes registran su confirmación o adopción en esta sesión; no
atribuyen a esta fecha el origen de todo el código existente.

## ADR-0001 — Mantener .NET 8 y fijar su SDK para el repositorio

- **Fecha:** 2026-10-20.
- **Estado:** Aceptada e implementada en la configuración.
- **Contexto:** Los proyectos tienen destino `net8.0`; en el equipo también existe
  SDK 10. La selección automática del SDK más reciente no expresa el entorno
  elegido para este backend.
- **Decisión:** Mantener .NET 8. Usar `global.json` con `version: 8.0.400`,
  `rollForward: latestPatch` y `allowPrerelease: false`. Conservar SDK 10 instalado
  para otros trabajos.
- **Motivo:** Compilar con una familia de SDK explícita sin migrar el proyecto por
  la presencia de otra versión en el equipo.
- **Consecuencias:** Debe existir un SDK compatible de la familia `8.0.4xx` y el
  runtime necesario para ejecutar la API. No se modifica el destino del proyecto.
  `dotnet-ef` queda fijado como herramienta local en 8.0.11, coherente con la
  versión de EF Core del repositorio.
- **Evidencia:** [global.json](global.json),
  [.config/dotnet-tools.json](.config/dotnet-tools.json) y los archivos `.csproj`.

## ADR-0002 — PostgreSQL 16 en Docker para desarrollo local

- **Fecha:** 2026-10-20.
- **Estado:** Aceptada e implementada en la configuración.
- **Contexto:** El backend usa Npgsql y necesita una base PostgreSQL con esquema
  administrado por migraciones.
- **Decisión:** Ejecutar `postgres:16-alpine` con Docker Compose, volumen
  persistente y puerto publicado exclusivamente en `127.0.0.1:5432`. La API se
  ejecuta en el anfitrión mediante el helper de desarrollo.
- **Motivo:** Proporcionar un entorno local reproducible y evitar publicar el
  puerto de la base en todas las interfaces de red del equipo.
- **Consecuencias:** El motor Docker debe estar disponible. El volumen conserva
  datos y credenciales iniciales; cambiar solamente `.env` no cambia la contraseña
  de un usuario ya creado. Esto no define el alojamiento de producción ni acredita
  que el contenedor esté en ejecución.
- **Evidencia:** [docker-compose.yml](docker-compose.yml) y
  [scripts/dev.ps1](scripts/dev.ps1).

## ADR-0003 — Cargar la configuración local mediante un helper de proceso

- **Fecha:** 2026-10-20.
- **Estado:** Aceptada e implementada.
- **Contexto:** Docker Compose interpreta `.env`, pero la configuración estándar
  de la API .NET no lo carga automáticamente. Ambos procesos deben usar las mismas
  credenciales locales.
- **Decisión:** `scripts/dev.ps1` lee las cuatro variables admitidas de `.env`
  (`POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` y `JWT_KEY`), construye la
  conexión y prepara las variables del proceso y sus hijos, incluidas
  `ConnectionStrings__PostgreSQL` y `Jwt__Key`. Restaura el entorno previo al salir.
- **Motivo:** Evitar divergencias entre PostgreSQL y la API sin guardar secretos
  reales en archivos versionados ni modificar variables de usuario o sistema.
- **Consecuencias:** El formato admitido es `NOMBRE=valor`, con comillas opcionales
  alrededor del valor completo, sin interpolación ni ejecución de comandos. `start`
  coordina contenedor, herramientas, migraciones y API; `migrate` requiere una base
  disponible. `test` no necesita `.env`. Ejecutar `dotnet run` directamente requiere
  preparar su configuración por otro medio.
- **Evidencia:** [scripts/dev.ps1](scripts/dev.ps1). Los valores de los secretos no
  forman parte de este registro.

## ADR-0004 — Conservar los nombres originales de los claims JWT

- **Fecha:** 2026-10-20.
- **Estado:** Aceptada e implementada.
- **Contexto:** El token usa `sub` para identificar al usuario y el dashboard busca
  ese mismo claim. El mapeo automático de claims impedía recuperar ese nombre y
  producía un 401 aun con un token válido.
- **Decisión:** Configurar `JwtBearerOptions.MapInboundClaims = false` y conservar
  `sub` como identificador empleado por el dashboard.
- **Motivo:** Alinear la emisión del token y la lectura de identidad sin cambiar
  el contrato de autenticación.
- **Consecuencias:** El código que lea identidad debe usar los nombres de claims
  elegidos explícitamente. Se mantienen las validaciones de firma, emisor,
  audiencia y vencimiento. Esta corrección no incorpora refresh tokens ni
  revocación de sesiones.
- **Evidencia:** [Program.cs](src/SecurityCenterAI.Api/Program.cs),
  [TokenService.cs](src/SecurityCenterAI.Api/Services/TokenService.cs),
  [DashboardController.cs](src/SecurityCenterAI.Api/Controllers/DashboardController.cs)
  y [pruebas HTTP](tests/SecurityCenterAI.Tests/DashboardAuthenticationTests.cs).

## ADR-0005 — Validar los parámetros de los records de entrada

- **Fecha:** 2026-10-20.
- **Estado:** Aceptada e implementada.
- **Contexto:** Registro y login usan records posicionales. Los atributos dirigidos
  a sus propiedades provocaban una excepción durante la validación de MVC.
- **Decisión:** Mantener los records y aplicar los atributos de validación a los
  parámetros del constructor, sin el destino `property:`.
- **Motivo:** Permitir que MVC procese la validación de ambos contratos y devuelva
  400 para solicitudes inválidas.
- **Consecuencias:** El formato JSON de los contratos se conserva. Los nuevos
  records posicionales de entrada deberán seguir el mismo criterio y comprobarse
  mediante solicitudes HTTP, además de compilar.
- **Evidencia:** [AuthContracts.cs](src/SecurityCenterAI.Api/Contracts/AuthContracts.cs)
  y [pruebas HTTP](tests/SecurityCenterAI.Tests/DashboardAuthenticationTests.cs).

## ADR-0006 — Adoptar la dirección web Next.js + backend + agente Windows

- **Fecha de adopción documentada:** 2026-10-20.
- **Origen:** Guía maestra v1.0, 2026-07-27, secciones 4 y 13.
- **Estado:** Aceptada como dirección; frontend y agente pendientes de implementar.
- **Contexto:** El producto necesita una interfaz accesible y verificaciones
  locales de Windows que una web normal no puede realizar por sí sola.
- **Decisión:** Mantener el backend .NET y PostgreSQL; orientar la web a Next.js y
  las verificaciones locales a un agente ligero Windows. La interfaz de escritorio
  completa corresponde a una evolución posterior.
- **Motivo:** Asignar interfaz y gestión a la web, evaluación y persistencia al
  backend, y obtención de evidencias locales al agente.
- **Consecuencias:** Antes del primer análisis real deben definirse dispositivos,
  vinculación temporal, credenciales por equipo, revocación y autorización por
  recurso. No están elegidos el empaquetado del agente ni el almacenamiento de
  tokens del frontend. Esta adopción no inicia su implementación.
- **Referencia:** [ARCHITECTURE.md](ARCHITECTURE.md), arquitectura objetivo.

## ADR-0007 — Puntuar con reglas y usar IA para explicar evidencia

- **Fecha de adopción documentada:** 2026-10-20.
- **Origen:** Guía maestra v1.0, 2026-07-27, secciones 5, 8 y 10.
- **Estado:** Aceptada como dirección; motor de reglas e IA pendientes.
- **Contexto:** El usuario debe comprender qué se verificó y por qué recibe una
  recomendación. Los resultados necesitan evidencia y un cálculo reproducible.
- **Decisión:** Producir hallazgos, severidad y puntuación con reglas programadas y
  versionadas. Usar IA para explicaciones y conversación con contexto aprobado,
  aplicando minimización de datos.
- **Motivo:** Evitar diagnósticos inventados y conservar trazabilidad del cálculo.
- **Consecuencias:** Cada análisis futuro necesita evidencias y versiones de reglas;
  deberán diseñarse sus entidades, contratos y pruebas. No se ha elegido proveedor
  de IA. El dashboard actual solo lee puntuaciones almacenadas y no satisface aún
  este flujo.
- **Referencia:** [ARCHITECTURE.md](ARCHITECTURE.md), flujo del primer análisis real.

## ADR-0008 — Organizar la documentación antes de ampliar el producto

- **Fecha:** 2026-10-20.
- **Estado:** Aceptada para esta etapa.
- **Contexto:** La guía describe un producto mucho más amplio que el backend
  existente. Se necesita un contexto fiel para retomar el trabajo sin presentar
  planes como funciones disponibles.
- **Decisión:** Documentar primero contexto, arquitectura, decisiones y prioridades;
  no comenzar el frontend durante esta etapa.
- **Motivo:** Alinear al equipo y conservar un registro verificable de avances,
  límites y objetivos.
- **Consecuencias:** Los cambios futuros deben distinguir implementación actual,
  objetivos y comprobaciones realizadas. Completar esta documentación no equivale
  a completar una versión de producto ni a validar Docker o PostgreSQL.

## ADR-0009 — Implementar la primera web con Server Actions y cookie de sesión

- **Fecha:** 2026-10-20.
- **Estado:** Aceptada e implementada; 16 unitarias, siete E2E con PostgreSQL
  real, chequeo de tipos y build de producción aprobados localmente.
- **Contexto:** Tras completar la documentación de ADR-0008, se autorizó el
  primer bloque hacia 0.5: registro, login y dashboard con los contratos .NET
  existentes. Este ADR concreta la web prevista en ADR-0006 y conserva ambos
  antecedentes; no completa el agente ni la beta privada.
- **Decisión:** Usar Next.js 16.4.2 App Router, React 19.3.1, TypeScript 5.9.3
  y Node.js 24 en `src/SecurityCenterAI.Web`. Intermediar registro, login y
  logout mediante Server Actions. Guardar el JWT en la cookie
  `security_center_session`, con `HttpOnly`, `SameSite=Lax`, `Path=/`, `Secure`
  en producción y vencimiento limitado por el token. Consultar la API desde
  Next.js con Bearer y `API_BASE_URL`, variable exclusiva del servidor cuyo
  valor local predeterminado es `http://localhost:5169`.
- **Motivo:** Integrar la web con la API actual y mantener la clave de firma y
  las llamadas autenticadas en el servidor. Mostrar datos reales, carga,
  ausencia de análisis y errores en español, con una interfaz adaptable.
- **Consecuencias:** El token no se guarda en `localStorage`. El parser del
  frontend solo extrae metadatos y comprueba estructura y vencimiento: .NET
  verifica la firma y autoriza cada consulta de dashboard. El logout elimina
  la cookie local, pero no revoca el JWT en el backend. No se incorporan refresh
  tokens, recuperación de contraseña, agente, verificaciones ni IA. La sesión
  de producción requiere TLS y el resto de controles de publicación.
- **Validación realizada:** 16 unitarias con `node:test` y siete E2E con
  Playwright, Chromium y servicios locales reales; chequeo de tipos, build y
  revisión visual en escritorio/móvil. El caso de historial usa siete registros
  sintéticos en PostgreSQL y verifica el límite de cinco resultados. Las cuentas
  de prueba y sus análisis se eliminan al terminar. Trazas, vídeos y capturas
  automáticas están deshabilitados; las capturas de revisión son opcionales con
  `SC_AI_VISUAL_REVIEW=1` y usan cuentas ficticias, sin tokens.
- **Evidencia:** [package.json](src/SecurityCenterAI.Web/package.json),
  [Server Actions](src/SecurityCenterAI.Web/src/app/actions.ts),
  [sesión](src/SecurityCenterAI.Web/src/lib/session.ts),
  [cliente de API](src/SecurityCenterAI.Web/src/lib/api.ts) y
  [tests](src/SecurityCenterAI.Web/tests).

## ADR-0010 — Vincular JWT a sesiones revocables y rotar la renovación

- **Fecha:** 2026-10-21.
- **Estado:** Aceptada e implementada; validación local del bloque con 30 pruebas
  del backend y 17 E2E de servicios reales aprobadas el 2026-10-21.
- **Contexto:** ADR-0009 permitió la primera web con JWT y logout local. Una
  copia del token seguía utilizable y no había renovación ni cierre global.
- **Decisión:** Asociar cada JWT con una sesión persistida mediante `sid` y
  comprobarla en cada solicitud autenticada. Access tokens de 15 minutos y
  refresh opacos de 32 bytes, con hashes SHA-256 y límite de sesión de 30 días.
  Rotar las credenciales de renovación y revocar la sesión si se reutiliza una
  consumida. Usar concurrencia optimista en las actualizaciones.
- **Motivo:** Permitir logout efectivo, cierre global y revocación tras reset
  sin esperar al vencimiento de todos los JWT.
- **Consecuencias:** Next.js conserva access y refresh en cookies `HttpOnly`;
  la firma permanece en .NET. Renovar requiere una Server Action para actualizar
  cookies. La detección de reutilización puede revocar una sesión si dos
  solicitudes compiten con el mismo refresh. Se requiere coordinar el cliente.
  La base pasa a ser necesaria para validar cada sesión. Vencimiento y consumo
  no eliminan registros: retención y purga siguen pendientes. El listado y cierre
  individual de otras sesiones no forman parte de la pantalla de cuenta actual.
- **Relación:** Sustituye el alcance de sesión y logout local de ADR-0009;
  conserva su elección de Server Actions y cookies. El resto del ADR-0009 es
  antecedente del primer bloque, incluidas sus pruebas, no del nuevo bloque.
- **Evidencia:** [sesión web](src/SecurityCenterAI.Web/src/lib/session.ts),
  [entidades](src/SecurityCenterAI.Domain/Entities) y
  [modelo EF](src/SecurityCenterAI.Infrastructure/Persistence/AppDbContext.cs).

## ADR-0011 — Tokens de correo de un uso y SMTP local con Mailpit

- **Fecha:** 2026-10-21.
- **Estado:** Aceptada e implementada; validación local del bloque con PostgreSQL
  y SMTP/Mailpit reales, incluyendo correo de un uso y reset, el 2026-10-21.
- **Contexto:** La base de cuentas necesitaba confirmar correo y recuperar
  contraseña sin exponer tokens en respuestas o depender de credenciales SMTP
  externas durante desarrollo.
- **Decisión:** Generar tokens opacos con propósito y hash en base, expiración
  y consumo de un uso. Verificación de 24 horas y recuperación de 30 minutos.
  Construir enlaces desde `FrontendUrl`; consumirlos por POST tras confirmación
  en la web. El reset revoca sesiones y requiere login normal.
- **Motivo:** Separar prueba de acceso al correo, recuperación de contraseña y
  autenticación, y permitir comprobar sus recorridos con correo local.
- **Consecuencias:** Mailpit captura mensajes en loopback, SMTP 1025 y UI 8025.
  Sus correos contienen secretos portadores y requieren custodia y retención.
  La API usa una cola volátil de 100 mensajes; no hay outbox durable ni
  reintentos persistidos. Mailpit limita la captura a 200 mensajes y no tiene
  volumen persistente en Compose.
  Fuera de Development/Testing se exige SMTP autenticado con TLS y frontend
  HTTPS; no se elige todavía un proveedor. Hay respuestas genéricas, espera por
  cuenta, bloqueo de login y límites en memoria por IP. Al intermediar Next.js,
  la IP observada es compartida; no se confía en cabeceras de proxy arbitrarias.
  Quedan MFA, política de contraseñas, alertas y operación de producción.
- **Evidencia:** [SMTP](src/SecurityCenterAI.Api/Services/AccountEmailSender.cs),
  [opciones](src/SecurityCenterAI.Api/Configuration/EmailOptions.cs),
  [tokens](src/SecurityCenterAI.Api/Services/OpaqueToken.cs) y
  [privacidad](PRIVACY_NOTES.md).

## ADR-0012 — Nombres en español para las columnas de users

- **Fecha:** 2026-10-21.
- **Estado:** Aceptada, implementada y aplicada a la base local; 30 pruebas del
  backend, 17 E2E y comprobación del modelo aprobadas. Los once valores de la
  cuenta previa se conservaron, junto con claves y relaciones.
- **Contexto:** Se solicitó comprender y consultar las once columnas de cuentas
  con encabezados en español en PostgreSQL y DBeaver.
- **Decisión:** Mantener las propiedades C# y los contratos JSON existentes,
  mapear las once columnas de `users` con `HasColumnName` y generar una migración
  de renombrado. La tabla continúa como `users`; otras tablas no se traducen.
  La correspondencia completa está en [README.md](README.md#columnas-de-users).
- **Motivo:** Mostrar nombres comprensibles al consultar la base sin cambiar
  los contratos del backend y la web ni sustituir los datos existentes.
- **Consecuencias:** SQL y consultas guardadas sobre `users` deben usar los
  encabezados nuevos; EF sigue usando las propiedades en inglés. Se aplica la
  migración tras una copia de la base y se reinician las instancias antiguas de
  la API. Se conserva el historial de migraciones y la reparación previa de
  `"NOMBRE"` a `"Name"` como antecedente; no es el mecanismo de esta traducción.
  Los nombres nuevos no cambian el hash, los permisos ni la retención de datos.
- **Evidencia:** [modelo EF](src/SecurityCenterAI.Infrastructure/Persistence/AppDbContext.cs),
  [migración de renombrado](src/SecurityCenterAI.Infrastructure/Persistence/Migrations/20261021120952_RenameUserColumnsToSpanish.cs)
  e [inventario de datos](PRIVACY_NOTES.md).

## ADR-0013 — Dispositivos con credencial propia y base versionada de hallazgos

- **Fecha:** 2026-10-21.
- **Estado:** Aceptada, implementada y aplicada a PostgreSQL local; 46 pruebas
  del backend, 21 unitarias web, tipos, build y 24 E2E aprobados. Alcance de
  cada comprobación registrado en el changelog.
- **Contexto:** El siguiente paso del roadmap necesita identificar un equipo sin
  utilizar la contraseña o sesión web como credencial permanente del agente.
  Los análisis previos tampoco identifican dispositivo, reglas o evidencias.
- **Decisión:** Añadir dispositivos vinculados a una cuenta, con correo verificado,
  un máximo de uno no revocado por cuenta, código temporal de un uso y credencial
  opaca propia. El código dura diez minutos y la credencial 90 días; ambos son
  aleatorios y sus hashes se guardan en la base. Separar las autorizaciones Bearer de usuario y Device. Preparar
  las entidades y relaciones de análisis, reglas versionadas y hallazgos con
  instantáneas de evidencia y recomendación, conservando los análisis previos.
- **Motivo:** Construir la vinculación y autorización antes de que un futuro
  agente envíe resultados y conservar la trazabilidad de las reglas aplicadas.
- **Consecuencias:** El primer alcance de la credencial de dispositivo se limita
  al contacto heartbeat. Crear o vincular un dispositivo no ejecuta verificaciones
  ni genera análisis, hallazgos o puntajes. El agente Windows y la ingesta quedan
  para el siguiente bloque. Se mantienen propiedades C#/JSON en inglés y columnas
  nuevas en español; las tablas existentes conservan sus nombres y columnas.
- **Transiciones:** Regenerar invalida códigos anteriores y conserva la credencial
  actual hasta canjear el nuevo. El canje revoca la anterior en una transacción.
  Reset revoca también credenciales y códigos de dispositivo, sin borrar su
  registro; logout-all solo cierra sesiones web. La versión del usuario avanza
  en vinculación y reemisión para detectar concurrencia con reset.
- **Límites:** Lectura de hasta 100 dispositivos, 20 análisis por detalle y
  100 hallazgos por análisis. Sin ingesta o endpoints para modificar reglas.
  El estado `linked` depende de credencial vigente, no de presencia online.
- **Validación:** Los E2E sobre PostgreSQL comprobaron límite concurrente de
  dispositivo, consumo único y carrera pair/reset, además del recorrido UI y la
  separación de credenciales. Reglas y hallazgos se comprobaron con datos
  sintéticos de EF InMemory y una comprobación adicional de PostgreSQL/UI,
  incluyendo evidencia HTML presentada como texto. No existe un análisis real
  de una PC. La cuenta original se conservó y los registros de prueba se limpiaron.
- **Evidencia:** [protocolo](docs/DEVICES.md),
  [mapeo](src/SecurityCenterAI.Infrastructure/Persistence/DeviceModelConfiguration.cs)
  y [migración](src/SecurityCenterAI.Infrastructure/Persistence/Migrations/20261021123143_DevicesAndFindingFoundation.cs).

## ADR-0014 — Agente Windows manual, credencial DPAPI y resultados locales

- **Fecha:** 2026-10-21.
- **Estado:** Aceptada e implementada como prueba de concepto; 84 pruebas del
  agente, 46 de regresión del backend y comprobación manual en Windows aprobadas.
  Amplía ADR-0006 y ADR-0013 sin completar la ingesta en esta etapa; ADR-0015
  amplía después la consola con envío explícito y puntos confirmados.
- **Contexto:** El dispositivo ya puede registrarse y emitir un código; falta
  un cliente Windows que lo canjee sin recibir la contraseña de la cuenta y
  permita verificar la obtención de evidencia antes de conectar el puntaje.
- **Decisión:** Incorporar una consola .NET 8 Windows de uso manual con `pair`,
  `heartbeat`, `status`, `scan`, `forget` y `help`. Leer el código con entrada
  oculta, sin argumento CLI; guardar credencial, destino y metadatos mediante
  DPAPI CurrentUser en LocalAppData fuera del repositorio. HTTPS salvo HTTP en
  localhost/127.0.0.1/[::1] exactos; sin redirecciones y con límites temporales
  y de respuesta. El token únicamente autentica heartbeat y no llega a la web.
- **Comprobaciones:** Diez consultas locales predeterminadas de solo lectura,
  con regla y versión, `Passed`, `Attention`, `Unknown` o `NotApplicable`,
  evidencia seleccionada, severidad y recomendación. Un error de permisos o
  datos ausentes no aprueba una regla. Defender no representa a otros antivirus;
  servicio Windows Update no determina parches y SMB1 solo evalúa el servidor.
- **Motivo:** Validar vinculación, custodia y obtención de evidencia con un
  alcance revisable antes de añadir ingesta o automatización. Permitir `scan`
  offline sin crear una cuenta ni enviar el estado del sistema.
- **Consecuencias:** Informes JSON locales en texto claro al solicitar `scan`,
  sin ingesta, puntaje o resultados nuevos en la web. DPAPI no protege contra
  procesos del mismo usuario. `forget` borra solo la copia local; revocar desde
  la web invalida el acceso remoto. Informes y registros remotos no se eliminan
  automáticamente. Quedan instalador firmado, servicio, actualización, revisión
  de permisos por edición, retención y operación de producción.
- **Evidencia:** [guía del agente y fuentes Microsoft](docs/WINDOWS_AGENT.md),
  [consola](src/SecurityCenterAI.Agent) e [inventario local](PRIVACY_NOTES.md#datos-locales-del-agente-windows).

## ADR-0015 — Ingesta manual idempotente, puntos confirmados y cobertura

- **Fecha:** 2026-10-21.
- **Estado:** Aceptada e implementada, con migración aplicada. Aprobadas 82
  pruebas del backend, 127 del agente, 26 unitarias web y 31 E2E; tipos y builds
  correctos. Alcance y límites de la evidencia en [CHANGELOG.md](CHANGELOG.md).
- **Contexto:** ADR-0014 dejó informes locales sin ingesta. El siguiente bloque
  del roadmap necesita conectar esas observaciones con el historial web sin
  recibir puntajes decididos por el cliente, duplicar envíos o presentar datos
  desconocidos como aprobaciones.
- **Decisión:** Añadir `POST /api/agent/analyses` con autenticación Device
  exclusiva. El esquema 2 contiene UUID estable de reporte, versión declarada
  del agente, fechas y exactamente diez resultados con regla/version, estado,
  evidencia seleccionada y fecha de observación. Propietario y dispositivo se
  obtienen de la credencial. El catálogo del servidor decide títulos,
  recomendaciones, severidad y pesos; no hay IA en el cálculo.
- **Idempotencia:** La pareja dispositivo/reporte es única. Un hash canónico
  permite devolver el análisis existente para el mismo contenido y rechazar
  con conflicto un identificador reutilizado con contenido distinto. Captura
  y recepción se guardan por separado. Reintentar no requiere otro escaneo.
- **Cálculo versión 1:** Los pesos suman 100 antes de excluir no aplicables:
  antivirus Defender 15, tiempo real 15, antigüedad de firmas 10, firewall 15,
  Secure Boot 10, TPM 5, BitLocker 10, servicio Windows Update 5, UAC 10 y
  SMB1 servidor 5. Los puntos confirmados son peso `passed` sobre peso
  aplicable. `attention` y `unknown` no suman; `notApplicable` se excluye.
  Si no hay conocidos (`passed` o `attention`), el puntaje es `null`.
  La cobertura es conocidos sobre número de reglas aplicables; todas no
  aplicables tienen cobertura 0. La web conserva los cuatro estados y marca
  los resultados parciales.
- **Motivo:** Los puntos confirmados expresan cuánto criterio favorable se
  observó, y la cobertura hace visible lo que falta. Un desconocido no
  demuestra riesgo ni protección. El cálculo es reproducible y versionado,
  sin convertirse en una probabilidad o certificación del equipo.
- **Envío y límites:** `scan` sigue siendo local por defecto. `scan -Send`
  guarda y envía explícitamente; `send` reintenta el último esquema 2. Los
  informes esquema 1 requieren un nuevo escaneo. Se limitan el cuerpo a
  64 KiB y los envíos a 6/minuto por IP, además de la política global.
  Las fechas son UTC, con antigüedad máxima de siete días, duración máxima
  de treinta minutos y tolerancia futura de cinco minutos; observaciones
  dentro del escaneo con cinco segundos de margen.
- **Consecuencias:** Se conservan cuentas, dispositivos e historial previo.
  `Score` pasa a nullable y los metadatos nuevos quedan vacíos en legacy;
  no se inventa cobertura o versión histórica. El informe y la evidencia son
  autodeclarados: Device autentica una credencial, sin atestación de hardware,
  binario o Windows. No hay servicio, envío automático, remediación,
  comandos remotos ni proveedor de IA. Retención, exportación y borrado
  individual siguen pendientes.
- **Relación:** Concreta el puntaje determinista de ADR-0007, amplía el alcance
  de Device de ADR-0013 y sustituye la limitación de informes solo locales de
  ADR-0014. Conserva las garantías de vinculación, DPAPI y ejecución manual.
- **Evidencia:** [protocolo e interpretación](docs/ANALYSIS_INGESTION.md),
  [agente](docs/WINDOWS_AGENT.md), [arquitectura](ARCHITECTURE.md) e
  [inventario de datos](PRIVACY_NOTES.md).

## Cómo continuar este registro

Para una decisión nueva, añadir un ADR con identificador, fecha, estado, contexto,
decisión, motivo, consecuencias y referencias. Registrar alternativas solo si se
evaluaron realmente. Cuando una decisión cambie, conservar su antecedente y marcar
qué ADR la reemplaza; no reescribir el historial como si la nueva opción hubiera
estado elegida desde el principio.
