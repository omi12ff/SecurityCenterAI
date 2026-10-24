# Decisiones de Security Center AI

Registro actualizado al **2026-10-22**. Una decisión aceptada como dirección del
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

## ADR-0016 — Guías locales y feedback privado por análisis

- **Fecha:** 2026-10-22. **Estado:** aceptada e implementada.
- **Contexto:** explicar hallazgos sin atribuir certeza a estados desconocidos.
  El usuario eligió guías locales sin servicio de IA externo.
- **Decisión:** plantillas v1 por regla, versión y estado, referencias fijas de
  Microsoft y revisión manual. No se alteran el catálogo ni el puntaje v1.
  Evidencia y comentarios se muestran como texto, sin interpretar instrucciones.
- **Feedback:** una valoración editable por propietario/análisis, comentario
  opcional de 1000 caracteres y versión de guía; acceso Bearer exclusivo y
  verificación conjunta de propiedad del dispositivo y análisis. La última
  escritura prevalece, sin historial de ediciones ni publicación de comentarios.
- **Consecuencias:** sin proveedor, credenciales ni transferencia de datos a IA.
  Administradores de la base y copias pueden acceder al feedback. Retención y
  borrado/exportación individual siguen pendientes. Se prepara una prueba local
  supervisada, sin desplegar ni declarar lista la beta externa.
- **Referencia:** [uso y límites](docs/LOCAL_GUIDES_AND_FEEDBACK.md).

## ADR-0017 — Distribución portátil para pruebas supervisadas

- **Fecha:** 2026-10-22. **Estado:** aceptada e implementada.
- **Contexto:** el agente anterior requería el repositorio y `dotnet run`.
  Para probarlo con un usuario autorizado se necesita un paquete ejecutable y
  un recorrido operativo que mantenga el vínculo y la privacidad existentes.
- **Decisión:** ZIP `win-x64` con runtime .NET 8 incluido, sin archivo único ni
  trimming. Contiene binarios publicados, ayuda offline, acceso `.cmd` que abre
  la ayuda y wrapper PowerShell opcional. No instala servicio, autoarranque,
  actualizador ni solicita elevación. `scan` sigue local por defecto; los envíos
  requieren `send` o `scan --send`.
- **Diagnóstico:** `doctor [--json]` informa requisitos básicos y límites sin
  escribir archivos, consultar la red o leer credenciales/reportes. `version`
  proviene de los metadatos del ensamblado. Ambos se ejecutan antes del vault.
  Una ubicación local de datos no válida falla sin guardar junto al ejecutable.
- **Integridad:** manifiesto con versión del agente/runtime y hashes de archivos,
  más SHA-256 del ZIP; la publicación no sobrescribe entregas y excluye datos
  locales o archivos del servidor. Los hashes detectan cambios, pero no sustituyen
  autenticidad ni firma de código. El paquete todavía no está firmado.
- **Consecuencias:** mover o sustituir la carpeta del programa no cambia
  `%LOCALAPPDATA%\SecurityCenterAI\Agent` ni DPAPI `CurrentUser`. Se actualiza
  extrayendo una nueva carpeta completa; parches del runtime incluido requieren
  republicar y distribuir. No se recomienda desactivar protecciones para ejecutarlo.
  La validación del anfitrión no certifica otras ediciones, ARM64, Server ni una
  máquina limpia sin SDK. La beta externa y su infraestructura siguen pendientes.
- **Referencia:** [distribución y operación](docs/AGENT_PRIVATE_BETA.md).

## ADR-0018 — Comparación manual de observaciones

- **Fecha:** 2026-10-22. **Estado:** aceptada e implementada.
- **Contexto:** la prueba en otra PC sigue pendiente y el usuario pidió avanzar
  en software al disponer solo de su equipo actual. Se adelanta la comparación
  de informes prevista en 1.1, manteniendo pendiente el resto de automatización.
- **Decisión:** comparar dos análisis del mismo usuario y dispositivo mediante
  lectura, sin persistir diferencias ni modificar informes. Se valida orden de
  recepción y de finalización de comprobaciones cuando ambos lo informan.
  Se distinguen mejoras observadas, revisión, nueva información, información
  perdida, mismo estado y casos no comparables. Evidencia y fechas permanecen
  visibles como observaciones originales, sin atribuir causalidad.
- **Puntos y cobertura:** solo hay deltas con fuente/esquema/modelo admitidos,
  catálogo completo versión 1, valores disponibles y mismo conjunto aplicable.
  Cambios desde desconocido aportan información; no prueban una reparación.
- **Acceso:** sesión Bearer exclusiva, propiedad conjunta de dispositivo y ambos
  análisis, 404 para selecciones ajenas. Device no permite consultar resultados.
  Revocar un dispositivo conserva la consulta de su historial por el propietario.
- **Consecuencias:** sin migraciones ni dependencias nuevas. La web valida el
  contrato y conserva como texto la evidencia, incluida la de reglas ambiguas
  antiguas. La compilación de verificación usa una salida `.next-qa` distinta de
  la del servidor de desarrollo. La prueba en otra PC y la beta externa siguen pendientes.
- **Referencia:** [uso y límites](docs/ANALYSIS_COMPARISON.md).

## ADR-0019 — Control individual del análisis con marca mínima contra reenvíos

- **Fecha:** 2026-10-22. **Estado:** aceptada e implementada; migración aplicada,
  147 pruebas API, 145 del agente y 40 unitarias web/tipos/build aprobados.
  Tres E2E nuevos aprobados, incluida carrera de cinco rondas con PostgreSQL.
  El paquete `0.4.1-preview.1` pasó 1.599 comprobaciones del helper público.
  El alcance de esas verificaciones se registra en el changelog.
  Amplía el control de historial y feedback
  de ADR-0016 y ADR-0018, sin implementar borrado completo de cuenta.
- **Contexto:** el propietario necesita descargar y borrar un resultado. El
  informe permanece local en el agente y un reintento idempotente posterior no
  debe restaurar silenciosamente lo que se eliminó del historial.
- **Decisión:** exportación JSON versión 1 mediante DTO explícitos con análisis
  completo, resultados, dispositivo id/nombre y feedback propio, sin agregar
  datos de cuenta, secretos o hashes internos. Borrado individual con Bearer de
  propietario, confirmación verdadera, campos extra rechazados y cuerpo de 1 KiB.
  Una transacción elimina análisis, resultados y todo el feedback asociado.
- **Marca mínima:** si existe ReportId, conservar únicamente DeviceId, ReportId
  y DeletedAtUtc en `deleted_analysis_reports`, con columnas en español y clave
  compuesta dispositivo/reporte. La ingesta rechaza ese mismo identificador con
  `410`. Las versiones de concurrencia de usuario y dispositivo coordinan las
  carreras entre eliminación y envío. Un análisis legacy sin ReportId no crea marca.
- **Motivo:** respetar el borrado ante reintentos conservando solo los datos
  necesarios para identificar el reporte eliminado; no retener su huella, puntos,
  evidencias o comentarios para bloquearlo.
- **Consecuencias:** la marca permanece sin purga automática mientras exista el
  dispositivo; su FK tiene cascada ante borrado físico, no revocación. El agente
  `0.4.1-preview.1` informa de `410` y exige un nuevo `scan`, sin borrar credencial
  o informe local y sin reintento automático. Las descargas son copias en texto
  claro y no se eliminan con el registro; tampoco los informes locales o backups.
  Retención automática, eliminación/exportación completas de cuenta y operación
  de copias siguen pendientes. El texto libre exportado requiere revisión antes
  de compartirlo.
- **Referencia:** [uso, contrato y privacidad](docs/ANALYSIS_HISTORY.md),
  [migración](src/SecurityCenterAI.Infrastructure/Persistence/Migrations/20261022194730_PrivateAnalysisHistory.cs)
  e [inventario](PRIVACY_NOTES.md).

## ADR-0020 — Exportación de perfil e historial completo de la cuenta

- **Fecha:** 2026-10-22. **Estado:** aceptada e implementada. Pasaron 158 pruebas
  del backend, 49 unitarias web, tipos/build y dos E2E nuevos con PostgreSQL;
  el alcance de estas comprobaciones consta en el changelog.
- **Contexto:** la exportación individual exige seleccionar un análisis. El
  propietario necesita descargar en una operación su perfil, todos los equipos
  y el historial, incluidos registros antiguos y equipos revocados.
- **Decisión:** `GET /api/account/export`, con Bearer de sesión activa y
  propietario obtenido del claim autenticado. JSON versión 1 con perfil,
  dispositivos, análisis completos con todos sus resultados y feedback propio,
  y marcas mínimas de informes eliminados. No aplica los límites visuales del
  historial. Una transacción `RepeatableRead` en PostgreSQL mantiene la misma
  instantánea durante las consultas `AsNoTracking` en serie.
- **Motivo:** conservar el contenido guardado y sus relaciones, sin recalcular
  puntos o fabricar metadatos ausentes, con aislamiento de cuenta y una lectura
  coherente frente al borrado concurrente.
- **Consecuencias:** el perfil incluye nombre y correo deliberadamente. DTO
  explícitos excluyen secretos, hashes y tablas internas de autenticación. La
  ruta web intermedia en el servidor y usa adjunto JSON, `no-store`, `nosniff`
  y `no-referrer`. Un límite específico permite tres solicitudes por minuto/IP
  por defecto, con la ventana/configuración existentes. No requiere migración,
  no modifica registros y no persiste un archivo de exportación en el servidor.
  La respuesta se prepara en memoria; historiales muy grandes requieren revisión
  de capacidad antes de una operación externa. Las descargas necesitan custodia
  propia. Borrado completo de cuenta, retención y operación de backups siguen
  pendientes; el archivo no tiene importación/restauración.
- **Referencia:** [contenido, uso y límites](docs/ACCOUNT_EXPORT.md).

## ADR-0021 — Borrado de cuenta con reautenticación y cascadas existentes

- **Fecha:** 2026-10-22. **Estado:** aceptada e implementada. Pasaron 167 pruebas
  del backend, 145 del agente, 52 unitarias web, tipos/build y tres E2E nuevos,
  incluidas tres rondas concurrentes con PostgreSQL. Alcance en el changelog.
- **Contexto:** la exportación de cuenta y el borrado individual ya existen;
  el propietario necesita retirar el perfil y todos sus datos activos.
- **Decisión:** Bearer de sesión activa, contraseña actual y confirmación exacta
  `ELIMINAR` en `DELETE /api/account`, sin UUID de otro propietario ni campos
  adicionales. Límite de 2 KiB y política específica de tres solicitudes/minuto/IP
  por defecto. Los errores de contraseña comparten el contador/bloqueo de login.
- **Concurrencia:** transacción PostgreSQL con bloqueo de la fila de usuario,
  revalidación de la sesión y contraseña y borrado mediante las cascadas existentes.
  Reintentos acotados ante conflictos/deadlocks. Ingesta/login/refresh rechazan
  referencias a una cuenta eliminada y reintentan conflictos admisibles.
- **Consecuencias:** desaparecen sesiones, tokens, dispositivos/credenciales,
  informes, hallazgos, feedback y marcas mínimas. El catálogo y otras cuentas
  se conservan. No se retiene un nuevo registro de cuenta borrada y un nuevo
  registro del correo crea otra identidad. La web limpia cookies tras un `204`.
  Los archivos locales, correos/colas y backups tienen custodia independiente.
  No hay recuperación ni reaplicación automática de borrados después de restaurar
  una copia anterior. Sin migración, correo nuevo o cambios del agente.
- **Referencia:** [contrato y límites](docs/ACCOUNT_DELETION.md).

## ADR-0022 — Retención técnica opcional con vista previa y lotes

- **Fecha:** 2026-10-22. **Estado:** aceptada e implementada. Pasaron 181 pruebas
  habituales del backend y tres adicionales en PostgreSQL aislado. Los plazos
  iniciales no representan una política de conservación de producción aprobada.
- **Contexto:** los estados vencido, consumido y revocado no eliminan registros.
  Se necesita mantenimiento acotado sin perder la detección de reutilización de
  refresh ni permitir que reaparezcan análisis borrados.
- **Decisión:** vista previa de solo lectura y aplicación manual con confirmación
  explícita. Trabajo periódico desactivado por defecto; al habilitarlo espera un
  intervalo completo. Plazos iniciales de 30 días para sesiones/credenciales y 7
  para tokens/códigos, desde su invalidez. Un lote por categoría y una transacción
  con bloqueo asesor PostgreSQL, predicado revalidado al borrar y reintentos acotados.
- **Motivo:** limitar el alcance y permitir revisar conteos antes de una operación
  física; conservar las garantías de seguridad y el contenido del producto.
- **Consecuencias:** no se purgan refresh individualmente; solo desaparecen por
  cascada con una sesión cerrada antigua. Todos los hashes de sesiones activas se
  conservan. Usuarios, dispositivos, informes, feedback, catálogo y marcas mínimas
  quedan fuera. Los conteos de padres no incluyen cascadas y la vista previa no
  reserva filas. CLI sin HTTP, correo o migraciones, logs agregados sin identidades.
  Las copias, archivos locales, correo y auditoría durable requieren otro bloque.
  No se aplicó purga a la base personal ni se activó el trabajo periódico.
- **Referencia:** [operación, opciones y pruebas](docs/RETENTION_MAINTENANCE.md).

## ADR-0023 — Copia cifrada con contraseña y ensayo de recuperación aislado

- **Fecha:** 2026-10-23. **Estado:** aceptada e implementada. Pasaron 191 pruebas
  habituales y tres ensayos PostgreSQL de este bloque, con datos ficticios.
- **Contexto:** las exportaciones de cuenta excluyen registros internos y no son
  un backup administrativo. Se necesita probar recuperación sin sobrescribir la
  base habitual ni dejar dumps en claro dentro del repositorio/OneDrive.
- **Decisión:** helper manual con contraseña por stdin y salida en la carpeta
  dedicada de LocalAppData con ACL usuario/SYSTEM. Clave PBKDF2-SHA256, sal/nonce
  aleatorios y AES-256-GCM con cabecera autenticada. Límite local de 64 MiB;
  manifiesto y dump permanecen en memoria hasta cifrarse.
- **Consistencia:** una instantánea exportada sirve para inventario y `pg_dump`.
  Se comparan filas/conteos de todas las tablas públicas y esquema. Los CHECK se
  normalizan mediante el planificador PostgreSQL sin ANALYZE para conservar la
  comparación ante casts equivalentes introducidos al restaurar.
- **Validación:** el archivo escrito se autentica y restaura con `pg_restore` en
  una base nueva de nombre generado. Se compara el inventario y se retira esa
  base; solo entonces se publica el archivo final. Una verificación posterior
  no requiere que exista la base original. No hay destino de restauración activo.
- **Consecuencias:** es una copia sensible que incluye hashes y registros de
  autenticación. Requiere custodia independiente de contraseña y archivo. Una
  interrupción abrupta puede requerir revisar destinos temporales. Siguen pendientes
  calendario, conservación, copia externa y recuperación real con tratamiento de
  borrados posteriores e invalidación de accesos restaurados. No se creó una copia
  personal con una contraseña elegida por el agente.
- **Referencia:** [comandos, formato y límites](docs/DATABASE_BACKUPS.md) e
  [inventario de pendientes](docs/PENDING_FEATURES.md).

## ADR-0024 — Actividad privada mínima y atómica por cuenta

- **Fecha:** 2026-10-23. **Estado:** aceptada e implementada. Pasaron 197 pruebas
  habituales, tres ensayos PostgreSQL nuevos, regresión de tres ensayos de backup,
  145 del agente, 56 unitarias web, tipos/build y dos E2E nuevos en corridas separadas.
- **Contexto:** falta una revisión de acciones sensibles accesible al propietario;
  conservar textos, credenciales o identificadores de contenidos borrados ampliaría
  innecesariamente la recopilación y el alcance del borrado existente.
- **Decisión:** tabla de cuatro columnas con UUID de evento, usuario, tipo cerrado
  y fecha UTC. Doce acciones completadas se añaden al mismo contexto/transacción
  del cambio. No se guardan IP, texto libre, secretos o identificadores de sesiones,
  dispositivos o informes. No se reconstruye actividad anterior a la migración.
- **Acceso:** GET con Bearer y propietario del claim, últimos 50 más `hasMore`.
  La web ofrece Mi cuenta → Actividad de seguridad y la exportación completa agrega
  el campo aditivo `securityActivity` al formato 1, manteniendo lectura de respuestas antiguas.
- **Protección y borrado:** catálogo CHECK, validación de cambios en el contexto EF y
  cascada al borrar la cuenta. No se conserva una entrada nueva de cuenta borrada;
  borrar un análisis registra solo tipo/fecha, sin su identificador o contenido.
  La purga técnica de sesiones/tokens no elimina estas entradas.
- **Consecuencias:** falla la operación si no puede guardar su evento. Conflictos
  con borrado concurrente se manejan sin dejar eventos huérfanos. Es protección
  de aplicación, no frente a SQL directo, administradores o una base comprometida.
  Conservación, cobertura de intentos fallidos/otras operaciones, escritor separado,
  auditoría externa y monitoreo permanecen pendientes.
- **Referencia:** [uso, catálogo y límites](docs/SECURITY_ACTIVITY.md).

## ADR-0025 — Readiness de PostgreSQL y logs por campos permitidos

- **Fecha:** 2026-10-23. **Estado:** aceptada e implementada.
- **Contexto:** liveness devolvía éxito sin comprobar la base y los logs generales
  de framework/EF podían incluir SQL, rutas o mensajes de excepción.
- **Decisión:** conservar `/health` independiente; agregar `/health/ready` con
  conexión y SELECT 1, cancelación/tiempos de tres segundos, límite por IP y
  respuesta mínima 200/503. No hacer escrituras o correos de prueba.
- **Diagnóstico:** UUID propio por petición, plantilla de ruta y códigos cerrados
  en consola JSON. Manejador de excepciones genérico y lista cerrada de categorías
  revisadas, sin scopes, SQL, cuerpos, valores de URL o excepción. Los niveles de
  configuración no habilitan categorías adicionales. Eventos mínimos de fallo
  de cola/SMTP conservados sin destinatario o token.
- **Consecuencias:** menor detalle para diagnosticar; hay que revisar código antes
  de añadir campos o categorías. No demuestra permisos de todas las operaciones,
  esquema o entrega SMTP. Persistencia, custodia de logs, métricas y alertas siguen
  pendientes; scripts, otros servicios y fallos previos al arranque tienen alcance propio.
- **Referencia:** [operación, campos y límites](docs/OPERATIONAL_MONITORING.md).

## ADR-0026 — Inventario mínimo y revocación individual de sesiones propias

- **Fecha:** 2026-10-23. **Estado:** aceptada e implementada.
- **Contexto:** el propietario podía cerrar todos los accesos, pero no consultar
  sesiones habilitadas o elegir una sin interrumpir las demás.
- **Decisión:** consulta de sesiones propias no revocadas/no vencidas, 50 por
  página con `hasMore`, primero la actual. Solo id, creación, vencimiento y bandera
  actual; no agregar IP, navegador, ubicación o última actividad.
- **Mutación:** Bearer, propietario y confirmación; reutilizar revocación con
  concurrencia y evento mínimo atómico. Sesión ajena/inexistente produce el mismo
  404; repetir un cierre no duplica el evento. El servidor informa si es la actual.
- **Web:** listado desde Mi cuenta, confirmación por entrada y borrado de cookies
  al cerrar la actual. La autorización del agente sigue siendo independiente.
- **Consecuencias:** listado puntual, no presencia en línea; cambios simultáneos
  pueden desplazar páginas. No cancela solicitudes ya autorizadas. No cambia el
  formato de exportación o las tablas; quedan pendientes límites por cuenta,
  métricas/alertas, MFA y capacidad para operación externa.
- **Referencia:** [uso y contrato](docs/ACCOUNT_SESSIONS.md).

## ADR-0027 — Cambio de contraseña con reautenticación y cierre de sesiones

- **Fecha:** 2026-10-23. **Estado:** aceptada e implementada.
- **Contexto:** cambiar una contraseña conocida requería iniciar recuperación por
  correo; faltaba una operación con contraseña actual y sesión activa.
- **Decisión:** Bearer, confirmación explícita y comprobación de contraseña actual
  con contador/bloqueo compartido. La clave nueva conserva el criterio actual de
  8–128 caracteres, diferente y sin normalización. No se amplía a una política de
  claves comprometidas en este bloque.
- **Consistencia:** bloqueo del usuario, revalidación de sesión y versiones con
  reintentos acotados. Hash, cierre de sesiones, consumo de recuperación pendiente
  y evento `password.changed` en el mismo commit; no toca agentes o verificación.
- **Notificación:** posterior al commit, sin secretos o enlaces, por el transporte
  existente. El fallo del aviso no revierte la operación; entrega durable pendiente.
- **Consecuencias:** vuelve al login sin crear otra sesión. La migración amplía el
  CHECK del catálogo; la exportación conserva el formato. No se borran eventos al
  intentar volver a una versión anterior; requiere planificar compatibilidad.
- **Referencia:** [uso y límites](docs/ACCOUNT_PASSWORD.md).

## ADR-0028 — Nombre editable y perfil guardado como fuente de presentación

- **Fecha:** 2026-10-23. **Estado:** aceptada e implementada.
- **Contexto:** el nombre no era editable y el dashboard mostraba una instantánea
  del JWT, mientras Dispositivos ya consultaba los datos del usuario.
- **Decisión:** operación autenticada de un campo por propietario y sesión activa;
  nombre de 2–100 caracteres sin controles, con recorte exterior. Bloqueo/versión
  de usuario coordinan concurrencia con cambios sensibles. No copia otros campos
  del cuerpo ni modifica accesos. Guardar el mismo nombre no vuelve a escribir.
- **Presentación:** dashboard consulta el perfil actual y análisis en paralelo.
  No se emiten nuevos tokens para actualizar una etiqueta de la interfaz; cookies
  y vencimientos se conservan. Las pestañas abiertas necesitan actualizar/navegar.
- **Consecuencias:** no hay campos nuevos, migración o nuevo evento de seguridad
  para esta edición de presentación. El cambio de correo requiere otro flujo con
  reautenticación y confirmación/verificación; continúa pendiente.
- **Referencia:** [uso, contrato y límites](docs/ACCOUNT_PROFILE.md).

## ADR-0029 — Política inicial local de contraseñas nuevas y capacidad de sesiones

- **Fecha y estado:** 2026-10-23, aceptado e implementado.
- **Antecedente:** actualiza el mínimo de contraseña descrito en ADR-0027;
  se conservan sus decisiones de cierre de sesiones, eventos y notificación.
- **Contexto:** faltaban un mínimo actualizado para credenciales nuevas y una
  capacidad por cuenta independiente del límite por IP.
- **Decisión:** 15 puntos Unicode mínimo, 128 unidades UTF-16 máximo y lista
  local inicial de 23 valores previsibles; sin consulta externa o requisitos de
  composición. No se impone la regla a claves anteriores al autenticar. Diez
  sesiones web habilitadas por cuenta, configurable 1–100; login correcto sin
  capacidad devuelve 409, conserva accesos y permite liberar lugar por cierre
  o recuperación. Versión de usuario coordina altas concurrentes en PostgreSQL.
- **Motivo y consecuencias:** incorpora controles sin reescribir hashes, expulsar
  sesiones existentes o agregar tablas. La lista pequeña no reemplaza un corpus
  amplio de filtraciones ni completa los controles de producción.
- **Referencia:** [política y límites](docs/ACCOUNT_SECURITY_POLICY.md).

## ADR-0030 — Reintentos SMTP y diagnóstico local sin ampliar persistencia

- **Fecha y estado:** 2026-10-23, aceptado e implementado.
- **Contexto:** un fallo SMTP transitorio perdía el único intento de entrega.
- **Decisión:** intentos y esperas acotados para fallos temporales; descarte
  definitivo o por antigüedad. Interfaz de transporte separada, cancelación y
  logs con código/número de intento. SMTP local se prueba con NOOP sin mensaje.
- **Motivo y consecuencias:** mejora recuperación ante fallos breves conservando
  el alcance de privacidad. La cola sigue volátil, puede perder o duplicar
  mensajes y no prueba entrega final. Persistencia durable sigue pendiente.
- **Referencia:** [correo y límites](docs/MAIL_DELIVERY.md).

## ADR-0031 — Cambio de correo con ambas direcciones y reautenticación final

- **Fecha y estado:** 2026-10-24, aceptado e implementado.
- **Contexto:** el correo identifica login/recuperación. No hay MFA y una cookie
  o un único enlace no deben bastar para cambiarlo.
- **Decisión:** pedir contraseña al solicitar y finalizar, confirmar ambos correos
  con tokens distintos de un uso y mantener el identificador hasta el último paso.
  Solicitud única por usuario, vencimiento acotado, enfriamiento y cancelación.
  Bloqueo/versión del usuario y unicidad coordinan transacciones concurrentes.
- **Efectos:** cambio verificado, cierre web, consumo de enlaces, retiro de
  solicitud y `email.changed` atómicos. Conserva agentes/contenido. Cambio/reset
  de contraseña retiran solicitudes; reset añade aviso posterior sin secretos.
  Notificaciones de mejor esfuerzo, sin hacer durable la cola existente.
- **Datos y consecuencias:** tabla técnica con columnas en español, correos y
  hashes; exportación solo de metadatos. Caducadas elegibles para purga opcional
  con `AccountTokenDays`. Downgrade bloqueado con solicitudes o eventos incompatibles.
  No resuelve pérdida de acceso al correo actual, MFA o correo de producción.
- **Referencia:** [uso, contrato y límites](docs/ACCOUNT_EMAIL_CHANGE.md).

## ADR-0032 — Consulta de actividad con filtros y continuación por evento

- **Fecha y estado:** 2026-10-24, aceptado e implementado.
- **Contexto:** la pantalla solo mostraba las últimas 50 entradas; consultar
  eventos más antiguos requería descargar toda la cuenta.
- **Decisión:** selección opcional por acción y días completos UTC, con hasta
  50 resultados y continuación por fecha/UUID del último evento. El punto se
  resuelve dentro de la cuenta y los filtros, sin offset ni propietario del cuerpo.
  Orden único aprovecha el índice existente y evita desplazar páginas cuando
  llegan eventos recientes. Consultas inválidas no parecen historiales vacíos.
- **Consecuencias:** no agrega columnas, categorías o escrituras; cada consulta
  es independiente, sin instantánea persistente de todo el recorrido. Exportación
  conserva toda la actividad y no aplica filtros. Horas visibles en Asuncion,
  días de filtros explícitamente UTC. Auditoría operativa global sigue pendiente.
- **Referencia:** [uso y contrato](docs/SECURITY_ACTIVITY.md).

## ADR-0033 — Revisión de enlaces/mensajes con reglas y contador mínimo persistente

- **Fecha y estado:** 2026-10-24, aceptado e implementado localmente.
- **Contexto:** la guía exige detector básico y límites diarios en la 1.0; el
  usuario mantiene la elección de no incorporar IA externa.
- **Decisión:** análisis explícito de estructura HTTP/HTTPS y señales textuales
  acotadas, sin abrir destinos, DNS o reputación. No devuelve seguridad/fraude
  confirmado o porcentaje de confianza. Observaciones fijas y dominio ASCII;
  nunca copia el contenido, credenciales o parámetros al resultado.
- **Cuota y datos:** veinticinco consultas combinadas por defecto/día UTC,
  configurable. Una fila por cuenta con último día/cantidad/versionado, sin
  historial de contenido o reportes. Bloqueo de usuario, revalidación de sesión
  y transacción coordinan cuotas concurrentes; cuenta/correo verificado exigidos.
  Exportación incluye metadatos; borrado de cuenta hace cascada y backups los incluyen.
- **Consecuencias:** regex lineales y entradas limitadas; posibles falsos positivos
  y omisiones explícitos. Una respuesta perdida tras commit puede consumir cuota.
  No agrega proveedor externo, analítica o eventos de actividad.
- **Referencia:** [contrato, privacidad y reglas](docs/LINK_MESSAGE_CHECKS.md).

## ADR-0034 — Priorizar las funciones 1.0 y mantener compatibilidad como pendiente

- **Fecha y estado:** 2026-10-24, decisión explícita del usuario e implementada en el plan.
- **Contexto:** el usuario no dispone de otra PC/VM y pidió enfocarse en la primera
  versión de la guía, en lugar de seguir ampliando bloques operativos.
- **Decisión:** completar 1.0 gratuita: landing, detector y cierre del recorrido
  manual, políticas/contacto. Se conserva el alcance de etapas posteriores y
  la preparación operativa necesaria para publicar.
- **Landing:** visitantes ven presentación, recorrido y límites; sesiones válidas
  siguen al dashboard y refresh existente pasa por la renovación anterior. Sin
  métricas, rastreadores o servicios externos nuevos.
- **Consecuencias:** otra PC/VM queda pospuesta, no aprobada; no se anuncia soporte
  Windows 10 u otras condiciones no verificadas. Se solicitan datos del responsable,
  país y contacto antes de completar páginas de privacidad/términos/errores.
- **Referencia:** [checklist 1.0](docs/RELEASE_1_0.md).

## ADR-0035 — Primeros pasos y descarga privada del agente existente

- **Fecha:** 2026-10-24.
- **Estado:** implementado localmente; no es una distribución pública.
- **Contexto:** el cierre 1.0 necesita que el usuario pueda pasar de su cuenta al
  primer análisis sin buscar comandos o paquetes en el repositorio.
- **Decisión:** guía privada con progreso derivado de perfil/equipo/análisis y
  descarga del único ZIP seleccionado en configuración del servidor. Revalidar
  sesión y correo, comprobar checksum y servir desde el mismo descriptor. Mantener
  análisis/envío manuales y separar la URL accesible al agente de la URL interna.
- **Motivo:** reutilizar el paquete portátil y los permisos existentes sin otra
  fuente de progreso, credenciales en comandos o ejecución desde el navegador.
- **Consecuencias:** requiere ZIP/checksum confiables y almacenamiento persistente
  accesible al proceso Next.js. No ofrece firma, instalador, actualizador, soporte
  Windows 10 validado o compatibilidad externa. Sin tablas o migraciones nuevas.
- **Referencia:** [recorrido, configuración y límites](docs/GETTING_STARTED.md).

## ADR-0036 — Prioridades por estado y lectura del historial

- **Fecha:** 2026-10-24.
- **Estado:** implementado localmente.
- **Contexto:** los puntos no identificaban qué revisar primero y varios informes
  completos hacían largo el recorrido del historial del equipo.
- **Decisión:** separar attention y unknown del último informe, con hasta tres
  enlaces por grupo; ordenar solo attention por severidad del catálogo. Conservar
  resumen y estados originales; abrir informes anteriores bajo demanda y por hash.
- **Motivo:** mostrar observaciones accionables sin interpretar desconocidos como
  problemas confirmados, recalcular resultados o introducir otro estado persistido.
- **Consecuencias:** la web realiza lecturas autorizadas adicionales; ante fallo
  presenta indisponibilidad. Historial mantiene límites actuales y exportación
  completa independiente. No hay nuevos endpoints, entidades o migraciones.
- **Referencia:** [prioridades e historial](docs/DASHBOARD_PRIORITIES.md).

## ADR-0037 — Borradores públicos y preparación de errores en el navegador

- **Fecha:** 2026-10-24.
- **Estado:** implementado localmente; identidad, contacto y textos finales pendientes.
- **Contexto:** el usuario pidió completar la primera versión, pero aún no definió
  responsable, país y correo público y solicitó un recordatorio.
- **Decisión:** ofrecer privacidad/términos identificados como borradores y un
  formulario que prepara un JSON local revisable, copiable o descargable. No
  inventar identidad, jurisdicción, receptor, recepción de tickets o aceptación
  contractual. Conservar los datos pendientes en el checklist de lanzamiento.
- **Motivo:** permitir revisar el recorrido y describir el tratamiento real sin
  enviar información a un canal no acordado. El reporte usa campos cerrados y
  no añade cuenta, navegador, URL, logs o archivos automáticamente.
- **Consecuencias:** no hay nuevos endpoints, tablas o servicios. El usuario debe
  revisar el texto libre y acordar un canal antes de compartirlo. La validación
  integrada usa cuentas e informes ficticios; no completa compatibilidad en otra
  PC, operación pública o revisión final de políticas.
- **Referencias:** [información y controles](docs/PUBLIC_INFORMATION.md),
  [recorrido integrado](docs/RELEASE_VALIDATION.md).

## Cómo continuar este registro

Para una decisión nueva, añadir un ADR con identificador, fecha, estado, contexto,
decisión, motivo, consecuencias y referencias. Registrar alternativas solo si se
evaluaron realmente. Cuando una decisión cambie, conservar su antecedente y marcar
qué ADR la reemplaza; no reescribir el historial como si la nueva opción hubiera
estado elegida desde el principio.
