# Contexto de Security Center AI

Última revisión: **2026-10-24**. Este archivo sirve para retomar el proyecto y
distinguir lo implementado de lo previsto. Las instrucciones de ejecución están
en [README.md](README.md).

## Visión del producto

Security Center AI busca ayudar a usuarios de Windows a entender el estado de
seguridad, actualizaciones, drivers y salud de su computadora. La experiencia
prevista combina una web, un backend y un agente instalado en Windows.

El agente obtendrá datos verificables; reglas programadas evaluarán los
resultados y calcularán el puntaje; la IA explicará esos resultados en español
claro y ayudará al usuario a resolverlos. El lanzamiento público previsto será
gratuito y tendrá límites de uso. Los planes pagos corresponden a etapas futuras.

La fuente de esta visión es la
[guía aportada por el equipo](docs/Guia_Maestra_Security_Center_AI_Actualizada.pdf),
versión documental 1.0 del 27 de julio de 2026. Se conserva una copia sin cambios
en `docs/`. Su versión documental no equivale a una versión publicada del producto.

## Dónde nos quedamos

**Prioridad acordada el 2026-10-24:** completar la primera versión **1.0 gratuita**
de la guía, con [plan de cierre](docs/RELEASE_1_0.md). El usuario pospuso otra PC/VM;
no se interpreta como validación de compatibilidad. Se prioriza producto sobre
nuevas ampliaciones operativas y se conserva la elección de guías sin IA externa.

**Recordatorio solicitado el 2026-10-24:** todavía faltan el nombre del responsable,
el país de operación y el correo público de contacto. Recordárselo al usuario al
retomar las páginas de privacidad, términos y reporte de errores, y antes de
publicar. Mantener esas páginas como borradores locales hasta definir los datos.
Pendientes registrados en el [checklist 1.0](docs/RELEASE_1_0.md).

Tenemos la **base interna 0.1** y la web de cuentas y dashboard hacia la **beta
0.5**, que sigue incompleta. Tras el primer frontend se incorporaron
sesiones revocables, renovación, verificación de correo, recuperación
y límites de intentos. El agente de consola añade diez comprobaciones de Windows
y envío explícito a la API; la web presenta resultados, puntos confirmados y
cobertura por dispositivo. Las
verificaciones de cada bloque se conservan en [CHANGELOG.md](CHANGELOG.md).
El historial incorpora comparación manual, exportación JSON y borrado individual
con marca mínima para impedir que un reintento restaure un informe eliminado.

| Área | Estado comprobable en el repositorio |
| --- | --- |
| API | ASP.NET Core en .NET 8, con proyectos API, Domain e Infrastructure. |
| Registro y login | Validación, correo normalizado, contraseña con hash, JWT asociado a sesión y bloqueo temporal por intentos. |
| Persistencia | PostgreSQL y EF Core: usuarios, análisis, sesiones y tokens de cuenta; migraciones versionadas. |
| Dashboard | Endpoint protegido que devuelve el último puntaje, el total y hasta cinco análisis recientes del usuario. |
| Swagger | Configurado para el entorno Development. |
| Estado de la API | Liveness HTTP y readiness PostgreSQL acotado, 200/503 sin detalles. `scripts/status.ps1` consulta ambos y comprueba SMTP NOOP en loopback sin envío. |
| Desarrollo local | Docker Compose para PostgreSQL y Mailpit; `scripts/dev.ps1` para iniciar, migrar o probar. |
| Pruebas del backend | 251 habituales aprobadas de nuevo en `ReleaseClosureQa`; las 23 PostgreSQL omitidas allí aprobaron aparte: 16 de cuentas/actividad, 4 de retención y 3 de backup. |
| Frontend Next.js | Next.js 16.4.2, React 19.3.1 y TypeScript 5.9.3; registro, login, dashboard y gestión de cuenta en español. |
| Sesión web | Access y refresh tokens en cookies `HttpOnly`, `SameSite=Lax`, `Secure` en producción; renovación rotatoria y revocación en el backend. |
| Sesiones activas | Inventario propio paginado y cierre confirmado. Máximo diez habilitadas por cuenta, configurable 1–100 y coordinado por versión en PostgreSQL; refresh no consume lugar nuevo. Mantiene autorización independiente del agente. |
| Cambio de contraseña | Desde Mi cuenta con contraseña actual/nueva y confirmación. Cierra sesiones web e invalida recuperación pendiente, conserva agentes y registra evento; aviso posterior de mejor esfuerzo. |
| Nombre del perfil | Editable en Mi cuenta; 2–100 caracteres, espacios exteriores recortados y sin controles. Dashboard y Dispositivos consultan el perfil actual sin renovar tokens o cerrar sesiones. |
| Cambio de correo | Reautenticación al solicitar y finalizar, confirmación de ambas direcciones, cancelación y cierre web. Conserva equipos y contenido; cambio/reset de clave cancelan solicitudes. Recuperación agrega aviso sin enlaces. |
| Correo y recuperación | Mailpit local y enlaces de un uso; cola volátil con reintentos SMTP acotados, descarte por antigüedad y logs mínimos. Sin durabilidad o proveedor externo elegido. |
| Límites | Rate limiting en memoria por IP, espera entre correos y bloqueo temporal por fallos de login. |
| Pruebas del frontend | 96/96 unitarias, tipos y build. Páginas públicas/reportes y recorrido integrado con cuenta/agente ficticios. Evidencia por corrida en el changelog; no es toda la suite histórica. |
| Dispositivos | Web `/devices`, correo verificado, código de un uso, credencial propia y revocación. Un dispositivo no revocado por cuenta. |
| Agente Windows | Consola .NET 8 de uso manual: vinculación, credencial DPAPI CurrentUser, heartbeat, estado local y olvido de credencial. Sin instalador o servicio. |
| Comprobaciones locales | Diez reglas de solo lectura con estados explícitos, evidencia seleccionada e informe JSON local al solicitar `scan`; `scan -Send` o `send` solicitan el envío manual. |
| Pruebas del agente | 145/145 repetidas nuevamente en Windows el 2026-10-24 con `ReleaseClosureQa`. DPAPI real, consultas/HTTP controlados, lectura/envío y `410`; separadas de PostgreSQL. |
| Reglas y hallazgos web | Ingesta con credencial Device, esquema 2, reintentos idempotentes y diez resultados por reporte. El servidor decide títulos, recomendaciones, severidad y pesos con su catálogo. |
| Puntaje | Puntos confirmados deterministas, cobertura y cuatro conteos de estado. Desconocido no equivale a correcto ni a riesgo comprobado; sin resultados conocidos el puntaje es `null`. |
| Control del historial | Desde Entender este análisis: exportación JSON del resultado y feedback, eliminación confirmada de análisis/evidencias/feedback y marca mínima por dispositivo/reporte. El mismo reporte eliminado recibe `410`; el agente pide un escaneo nuevo. |
| Exportación de datos de cuenta | Desde Mi cuenta: perfil y todo el contenido propio del producto como JSON, incluidos equipos revocados, informes antiguos, feedback y marcas mínimas. Excluye secretos y registros internos de autenticación. |
| Eliminación de cuenta | Contraseña actual y confirmación exacta; borra datos activos y accesos mediante cascadas. Las copias, correos/colas y archivos locales tienen custodia independiente. |
| Mantenimiento técnico | Vista previa y purga manual confirmada de sesiones/tokens/códigos/credenciales antiguos. Lotes limitados y trabajo periódico desactivado por defecto; conserva contenido, marcas mínimas y hashes de sesiones activas. |
| Backup local | Copia manual cifrada con contraseña, inventario de la misma instantánea y restauración comprobada en una base temporal que se retira. No sustituye la base activa ni programa copias. |
| Actividad privada | Mi cuenta permite filtrar acción/días UTC y continuar hacia eventos anteriores, 50 por consulta, de catorce acciones completadas. Exportación completa sin filtros. Cuatro columnas mínimas, escritura atómica y cascada de cuenta, sin purga automática. No es auditoría externa inalterable ni monitoreo completo. |
| Logs de API | JSON con plantilla de ruta, estado, duración e id de petición generado; sin URL libre, cuerpo, cabeceras, scopes, SQL o excepción. Lista cerrada de categorías. No crea archivos ni alertas. |
| Detector de enlaces/mensajes | Revisión limitada por reglas `local-signals-v1`, con confirmación y 25 consultas/día UTC por defecto, combinadas por cuenta. Cuota persistente; sin guardar contenido/resultados o conectar con los destinos. |
| Landing | Página local de visitantes con registro, recorrido y límites; raíz conserva acceso al dashboard/renovación de sesiones existentes. Privacidad/términos/contacto públicos pendientes. |
| Primeros pasos | `/getting-started`: progreso propio, acceso privado al ZIP y guía de vinculación/scan/send. Paquete existente seleccionado en `.env.local`, sin recompilar el agente o publicar una release. [Uso](docs/GETTING_STARTED.md). |
| Prioridades e historial | Resumen separa revisión y desconocidos del último informe, con enlaces a cada guía; tabla con equipo/cobertura y anteriores plegables por equipo. Conserva resultados y usa lecturas existentes. [Uso](docs/DASHBOARD_PRIORITIES.md). |
| Información pública y errores | `/privacy` y `/terms` en borrador; `/report-problem` prepara un JSON local sin envío, datos automáticos o persistencia. Enlaces antes del registro y desde los layouts. [Estado](docs/PUBLIC_INFORMATION.md). |
| IA externa | Pospuesta por elección del usuario; se mantienen guías y reglas locales. |
| Alertas, automatización y suscripciones | Pendientes, según el roadmap. |

`SecurityAnalysis` incorpora una relación opcional a dispositivo para conservar
los análisis previos. `POST /api/agent/analyses` recibe observaciones acotadas
del cliente y crea el análisis en el dispositivo de su credencial. No ejecuta
cmdlets remotos ni prueba la veracidad del informe. Los datos de cuenta se
devuelven en registro y login; ya se puede editar el nombre del perfil. El cambio
de correo ya exige ambas confirmaciones y reautenticación en un flujo separado.

Las once columnas de `users` están en español mediante mapeos de EF y una
migración de renombrado aplicada. Las propiedades C# y contratos JSON conservan sus nombres
y las demás tablas existentes no se traducen. Las tablas y campos nuevos de
dispositivos utilizan columnas en español. Las equivalencias para DBeaver están en
[README.md](README.md#columnas-de-users); aplicación y comprobaciones de este
cambio se registran por separado en el changelog.

El bloque de dispositivos añade 16 casos del backend: 46/46 aprobados con la
suite previa, comprobando verificación en BD, aislamiento de schemes, códigos
y credenciales, vigencia, revocación, reset y lecturas. También pasaron 21
unitarias web, tipos, build y 24 E2E; se aplicó la migración nueva sobre
PostgreSQL después de una copia y se conservó la cuenta previa. Los nuevos E2E
comprueban carreras de dispositivo y vinculación, reset y la UI móvil con
historial vacío. Además se comprobó en PostgreSQL y la UI un conjunto sintético
de regla, análisis y hallazgo, con HTML mostrado como texto y sin ejecución.
La revisión a 360/1440 px no mostró desborde. La limpieza conservó la cuenta
original sin cambios y dejó cero filas de prueba; esos resultados no son un
análisis de una PC.

El bloque del agente de consola aprobó 84 pruebas en Windows y las 46 de
regresión del backend. Se ejecutaron `help`, `status` y las diez comprobaciones
reales sin elevar permisos, generando el informe minimizado fuera de OneDrive
y conservando los estados desconocidos. No se canjeó el dispositivo personal
del usuario: su vinculación se completa manualmente con la guía. También
pasaron las 21 unitarias web y tipos tras ajustar los textos; el build y los
24 E2E anteriores no se repitieron para ese cambio de copia. La evidencia y
sus límites están en [CHANGELOG.md](CHANGELOG.md).

## Verificaciones realizadas

El cierre local volvió a comprobar 251 pruebas habituales, 145 del agente y las
23 PostgreSQL separadas (16/4/3), con 96 unidades web y tipos/build. Se agregaron
pruebas de documentos/reportes locales y un recorrido de registro, correo, ZIP,
informes ficticios, comparación, exportaciones y borrados. [Alcance](docs/RELEASE_VALIDATION.md).
No ejecuta el agente personal ni amplía compatibilidad a otra PC.

Prioridades/historial aprobó 91 unitarias web, tipos/build y nueve E2E seleccionados
de dashboard, historial y acceso. Los tres casos nuevos se repitieron tras compactar
el detalle duplicado del resumen y revisar escritorio/móvil. Un 429 inicial se
resolvió respetando Retry-After en el fixture, sin modificar la API. Sin cambios
de backend, esquema o agente. [Uso y límites](docs/DASHBOARD_PRIORITIES.md).

Primeros pasos aprobó 85 unitarias web, tipos/build y cinco E2E seleccionados con
landing. Un E2E adicional contra producción local comprobó paquete ausente,
destino inválido, copia de comandos y renovación de sesión. Se conservó el ZIP
0.4.1-preview.1 y se seleccionó mediante configuración local ignorada. Las pruebas
usaron cuentas ficticias; no vincularon, ejecutaron o enviaron el agente personal.
Sin cambios de API, tablas o migraciones. [Uso](docs/GETTING_STARTED.md).

El detector aprobó 251 pruebas habituales y 77 unitarias web, tipos/build y tres
E2E iniciales. Dieciséis ensayos PostgreSQL de cuentas/actividad y tres de backups
aprobaron aparte, incluidos cuota concurrente, rollback/cascada y nueva tabla.
Los veintitrés PostgreSQL se omiten en la corrida habitual; retención no se repitió.
La migración de contador se aplicó tras copia DPAPI verificada. [Alcance](docs/LINK_MESSAGE_CHECKS.md).
Evidencia final de landing/recorrido y preservación de datos en el changelog.

La consulta filtrada de actividad aprobó 241 pruebas habituales del backend,
72 unitarias web y tipos/build. Catorce ensayos PostgreSQL de cuentas/actividad
aprobaron aparte, con un caso nuevo de fechas empatadas, nuevas entradas entre
consultas, aislamiento y bordes de día UTC. Los 21 ensayos PostgreSQL se omiten
en la corrida habitual; retención/backups conservan la evidencia anterior.
Tres E2E seleccionados de actividad aprobaron navegación, filtros, exportación
completa, aislamiento, vacío/error y móvil. No se repitieron agente, toda la
suite E2E histórica o CI remoto. Sin esquema o categorías de datos nuevos.
[Uso y límites](docs/SECURITY_ACTIVITY.md).

El cambio de correo y aviso de recuperación aprobaron 239 pruebas habituales,
69 unitarias web, tipos/build y cinco E2E seleccionados juntos. Veinte ensayos
PostgreSQL se ejecutaron aparte: trece de cuentas/actividad, cuatro de retención
y tres de backup, incluida la tabla nueva y el tipo `EmailChanged`. Migración
aplicada después de copia técnica cifrada DPAPI verificada. No se cambió la cuenta,
correo o contraseña personales; las pruebas usaron cuentas ficticias. No se
repitieron agente, toda la suite E2E histórica o el runner alojado de CI.
[Uso y límites](docs/ACCOUNT_EMAIL_CHANGE.md).

El bloque de política de cuentas y correo incorpora mínimo de 15 y lista local
inicial para claves nuevas, capacidad de sesiones por cuenta, reintentos SMTP,
caducidad y diagnóstico NOOP local. No cambió esquemas, claves o accesos personales.
El helper consolidado aprobó 230 pruebas backend, 145 del agente, quince de
PostgreSQL aislado, 66 unitarias web, tipos y build. Se prepara CI con permisos
mínimos y acciones fijadas, validado con actionlint; sin ejecución remota.
[Política](docs/ACCOUNT_SECURITY_POLICY.md), [correo](docs/MAIL_DELIVERY.md),
[verificación](docs/VERIFICATION.md) y evidencia exacta en el changelog.

El bloque de actividad privada aprobó 197 pruebas habituales del backend, con
nueve ensayos PostgreSQL omitidos explícitamente, y 56 unitarias web, tipos y build.
Tres ensayos nuevos con PostgreSQL aislado comprobaron rollback de la operación
si falla el evento, cascadas/aislamiento y tres rondas concurrentes de login,
revocación y borrado de cuenta. Los tres ensayos de backup también se repitieron,
incluida una entrada en la tabla nueva. Pasaron 145 pruebas del agente.
Dos E2E nuevos pasaron en corridas separadas: acceso desde Mi cuenta y aislamiento,
y móvil de 360 px con vacío, límite de 50, cabeceras privadas y exportación de
65 eventos. La migración aditiva se aplicó tras una copia técnica protegida con
DPAPI CurrentUser, fuera de OneDrive; no sustituye un backup portátil con contraseña.
La API habitual se actualizó y EF no detectó cambios de modelo pendientes.
Las ocho tablas anteriores conservaron huellas y conteos; no quedaron cuentas
E2E o bases temporales. La tabla de actividad quedó vacía para la cuenta existente
hasta su siguiente acción registrada. API y web respondieron `200`.
[Uso y cobertura](docs/SECURITY_ACTIVITY.md).

El bloque de backups añadió diez casos sin Docker y tres ensayos PostgreSQL.
Pasaron 191 pruebas habituales del backend, con seis omisiones explícitas para
las suites PostgreSQL. `scripts/test-backup.ps1` aprobó las tres nuevas, además
del CLI y una contraseña Unicode por stdin, en una base vacía migrada y con datos
ficticios. Comprobó todas las tablas, cambios concurrentes, rechazo de alteraciones,
esquema diferente, origen inexistente y conservación del origen tras verificar una
copia anterior a un borrado. Retiró bases y archivos temporales. Se corrigió la
comparación de casts equivalentes de CHECK al restaurar sin omitir las restricciones.
El primer backup de datos personales se crea manualmente con una contraseña
ingresada en la terminal; durante este bloque no se creó esa copia ni se restauró
sobre la base habitual. [Uso](docs/DATABASE_BACKUPS.md).
Las 145 pruebas del agente también pasaron en `BackupQa`. Las huellas de ocho
tablas habituales coincidieron, no quedaron bases temporales de backup y API/web
respondieron `200`. No se modificó el frontend ni se repitieron sus suites.

El mantenimiento técnico añadió catorce casos del backend: 181 aprobados, con
tres pruebas PostgreSQL omitidas explícitamente en esa corrida. Las tres se
aprobaron por separado en una base temporal vacía con datos ficticios: lotes y
cascadas, conservación de hashes activos/contenido, exclusión entre purgas y
revalidación de una sesión modificada entre selección y borrado. También pasaron
CLI preview/apply y rechazo de aplicación sin confirmación. La base temporal se
eliminó al terminar. La vista previa en la base habitual informó cero elegibles;
no se aplicó purga ni se habilitó el worker. [Operación](docs/RETENTION_MAINTENANCE.md).
Pasaron de nuevo las 145 pruebas del agente. Ocho tablas conservaron conteos y
huellas; API habitual actualizada, sin migraciones nuevas, y API/web respondieron
`200`. No se repitieron las suites web al no haber cambios de frontend.

El borrado de cuenta aprobó 167 pruebas del backend, 145 del agente y 52 unitarias
web, tipos y build. Tres E2E nuevos con PostgreSQL y Mailpit verificaron cascadas,
aislamiento, contraseña/confirmación, cookies y tres rondas concurrentes con
login, refresh e ingesta. Se corrigieron errores 500 reproducidos durante esas
carreras, incluidos deadlocks envueltos por el proveedor. Sin migración ni cambios
del agente. Las huellas y conteos de ocho tablas coincidieron con el estado
anterior a estas pruebas, que ya tenía cero análisis activos y tres marcas mínimas.
La cuenta, dispositivo, credencial y catálogo existentes se conservaron. Se detuvo
la API de QA y quedaron activas la API y web habituales.

La exportación de datos de cuenta aprobó 158 pruebas del backend, 49 unitarias
web, tipos y build de producción. Dos E2E nuevos con PostgreSQL/Mailpit reales
verificaron cuenta propia, equipos revocados, feedback/marcas, 23 análisis y
un registro antiguo de 101 hallazgos, junto con la descarga móvil y conservación
de la sesión. No requirió una migración ni cambios del agente. Las huellas y
conteos de siete tablas coincidieron con el estado anterior a las pruebas:
se conservaron los tres análisis y treinta resultados originales. La API de QA
se detuvo; API y web habituales quedaron activas. El detalle está en el changelog.

El bloque de control del historial aprobó 147 pruebas API, 145 del agente y
40 unitarias web, tipos y build de producción. Se aplicó
`20261022194730_PrivateAnalysisHistory` tras un respaldo temporal: agrega solo
la tabla de tres columnas para rechazar el reenvío de informes eliminados.
Tres E2E nuevos comprobaron contrato, descarga/borrado móvil y cinco rondas de
carrera de reenvío/eliminación con PostgreSQL real. El paquete
`0.4.1-preview.1` aprobó 1.599 comprobaciones del helper público, nueve procesos
de diagnóstico y cuatro alteraciones rechazadas en el anfitrión; su alcance,
hash y tamaño están en [CHANGELOG.md](CHANGELOG.md).
La comparación de conteos y huellas con el respaldo previo restaurado en una
base temporal confirmó intactos los datos originales de seis tablas, incluidos
los tres análisis y treinta resultados. La limpieza dejó cero marcas de prueba.

El bloque de ingesta aprobó 82 pruebas API, 127 del agente, 26 unitarias web,
tipos, build de producción y 31 E2E. Se verificaron recepción idempotente y
carreras de envío con revocación, reset y revinculación en PostgreSQL. La
migración `20261021192739_AgentAnalysisIngestion` quedó aplicada tras una copia,
sin cambiar las filas previas de usuario, dispositivo o credencial. Se
limpiaron todas las cuentas y resultados de QA y permanecen las diez reglas
canónicas. Las comprobaciones personales no se enviaron en estas pruebas.
Las vistas sintéticas de 360/1440 px no mostraron desborde ni ejecución de HTML.

En la puesta en marcha y el primer frontend del **2026-10-20** se comprobó:

- Compilación y seis pruebas correctas con .NET 8: hash de contraseña, flujo de
  registro/login/dashboard, datos aislados por usuario, orden y límite de cinco
  resultados, formularios inválidos y tokens ausentes o inválidos.
- Contenedor PostgreSQL saludable y migración inicial aplicada.
- Solicitudes HTTP contra PostgreSQL real: registro, correo duplicado, login,
  contraseña incorrecta, entrada inválida y dashboard con y sin token.
- Disponibilidad de Swagger durante esa comprobación. Se eliminó la cuenta
  temporal creada para verificar el flujo.
- 16 pruebas unitarias de la web aprobadas: validación de formularios, ausencia
  de contraseñas en estados de error y lectura segura de estructura, claims y
  vencimiento del JWT. Esas pruebas no verifican la firma ni sustituyen a la API.
- Chequeo de tipos y build de producción de la web aprobados; el chequeo genera
  los tipos de Next.js antes de ejecutar TypeScript.
- Siete E2E aprobados con Chromium, web, API y PostgreSQL reales: autenticación,
  acceso sin sesión, rechazo de firma falsa, errores, vista móvil e historial.
  Para el historial se insertaron siete registros sintéticos en la cuenta de
  prueba y se comprobaron el total y los últimos cinco resultados. Las cuentas
  temporales y sus análisis se eliminaron mediante el fixture.
- Revisión visual de la interfaz en escritorio y móvil.

Estos resultados son antecedentes de una ejecución local; no garantizan que los
procesos sigan activos. La primera comprobación HTTP manual con PostgreSQL fue
independiente de los seis casos automatizados del backend, que usan InMemory.
La suite E2E incorporada después sí automatiza el recorrido web con PostgreSQL.
Los E2E comprueban el recorrido web sobre servicios reales, con datos sintéticos
de prueba. No equivalen a un análisis real del equipo ni a una validación de
producción. La web todavía no se publicó.

Los resultados anteriores corresponden al primer frontend. El **2026-10-21**
pasaron **30 pruebas del backend** del bloque de cuentas, con uso único y
vencimiento de tokens, hashes, rotación, revocación, cierre global, bloqueo,
espera entre correos y 429. Usan EF InMemory, reloj controlable y capturador
de correo; la validación SQL, SMTP y web reales se registra por separado en
[CHANGELOG.md](CHANGELOG.md).

También pasaron 17 unitarias web, el chequeo de tipos y 17 E2E con web, API,
PostgreSQL y SMTP/Mailpit reales. Se aplicó la migración de cuentas tras una
copia local. Los E2E comprobaron verificación y recuperación por correo,
renovación, cierre global entre navegadores, aislamiento y carreras de refresh,
verificación y reset en PostgreSQL; los fixtures terminaron su limpieza sin error.
La consulta final conservó una cuenta previa y no encontró cuentas E2E, análisis,
sesiones ni tokens de prueba. Los mensajes propios se eliminaron por fixture.
La revisión móvil de recuperación y verificación a 360 px no mostró desborde
horizontal ni errores de página. Las instancias de QA se detuvieron; las que
el usuario ya tenía en 3000/5169 quedaron intactas. En esa entrega las instancias
anteriores necesitaban reinicio y login nuevo para cargar el bloque; no es una
indicación de que las rutas actuales sigan sin funcionar.

## Base técnica y configuración

- `SecurityCenterAI.sln` agrupa el backend, el agente de consola y las pruebas.
- `global.json` selecciona la banda 8.0.4xx del SDK y permite sus actualizaciones
  de parche, aunque haya otras versiones del SDK instaladas.
- `.config/dotnet-tools.json` fija `dotnet-ef` 8.0.11.
- `docker-compose.yml` ejecuta PostgreSQL 16 en `127.0.0.1:5432` y Mailpit en
  SMTP `127.0.0.1:1025` y web `127.0.0.1:8025`. La base permanece en un volumen.
- `.env` es local y está excluido de Git. Contiene la configuración de la base
  y la clave JWT; los documentos no deben incluir sus valores reales.
- `.dockerignore` excluye secretos locales, dependencias y resultados de
  compilación/pruebas del contexto de construcción Docker.
- `scripts/dev.ps1` carga `.env` y prepara las variables que necesita la API
  únicamente para su proceso, incluido `ASPNETCORE_ENVIRONMENT=Development`.
  Admite `-Configuration` y `-Port` para una instancia local aislada, y restaura
  el entorno previo al salir. .NET no lee ese archivo por sí mismo.
- El perfil HTTP del script usa `http://localhost:5169`. Es una configuración
  de desarrollo local; el despliegue público todavía no está preparado.
- La web requiere Node.js 24 y utiliza App Router. Sus comandos npm son `dev`,
  `build`, `start`, `typecheck`, `test` y `test:e2e`.
- `API_BASE_URL` solo se usa en el servidor Next.js; por defecto apunta a
  `http://localhost:5169`. La web local se abre en `http://localhost:3000`.
- Las Server Actions intermedian las operaciones de cuenta. Next.js conserva
  access y refresh tokens en cookies `HttpOnly`, y consulta la API con Bearer.
  La clave de firma permanece en .NET; no se utiliza `localStorage`.
- El parser del JWT extrae metadatos; no valida su firma ni decide permisos.
  .NET comprueba firma y sesión activa en base de datos para cada solicitud
  autenticada. La página de cuenta obtiene los datos actuales desde la API.
- El access token dura 15 minutos y el refresh token opaco se rota con una
  vigencia de 30 días. La base guarda hashes de refresh y tokens de correo.
  Revocar la sesión invalida sus access tokens; reutilizar un refresh consumido
  revoca esa misma sesión, incluidas solicitudes concurrentes con el token viejo.
- La verificación de correo dura 24 horas y la recuperación, 30 minutos.
  Restablecer contraseña revoca todas las sesiones y exige iniciar sesión de
  nuevo. No se ha incorporado MFA ni un proveedor SMTP de producción.

Consulta [ARCHITECTURE.md](ARCHITECTURE.md) y [DECISIONS.md](DECISIONS.md)
para conocer los flujos y los motivos de estas decisiones.

## Reglas del producto para las próximas etapas

- Mostrar hechos y evidencia; distinguir un riesgo confirmado de una advertencia,
  una mejora opcional o información preventiva.
- Calcular puntajes y severidades con reglas deterministas y versionadas.
- Usar IA para explicar hallazgos verificados; los datos desconocidos deben
  conservar ese estado.
- Mostrar un estado pendiente cuando todavía no haya un análisis, sin atribuir
  un puntaje al equipo.
- Recopilar los datos mínimos y explicar qué se analiza, qué se envía y cómo
  puede borrarse.
- Diseñar el agente con permisos mínimos y acciones acotadas; evitar la
  ejecución de comandos arbitrarios.
- Para drivers, distinguir problemas comprobados de la antigüedad de una
  versión y usar fuentes oficiales.

Estas reglas orientan funciones futuras; no implican que sus controles estén
implementados. El inventario actual y sus pendientes están en
[SECURITY.md](SECURITY.md) y [PRIVACY_NOTES.md](PRIVACY_NOTES.md).

## Siguiente trabajo

Se completaron cuentas, sesiones, dispositivos y la base de resultados sobre
la web. La prueba de concepto del agente Windows permite vincular, comprobar
el equipo y enviar el resultado explícitamente; [su guía](docs/WINDOWS_AGENT.md) describe los comandos,
permisos y limitaciones. La beta 0.5 sigue incompleta y la aplicación no se ha publicado.
Quedan controles de producción, como MFA, política completa de contraseñas,
operación del correo, conservación de contenido, auditoría y despliegue.

La [ingesta manual](docs/ANALYSIS_INGESTION.md) conecta esquema 2, fechas,
reglas versionadas, puntos confirmados y cobertura con el historial del equipo.
Los informes previos del esquema 1 requieren un `scan` nuevo para enviarse.
Se incorporaron [guías locales y feedback privado](docs/LOCAL_GUIDES_AND_FEEDBACK.md)
por análisis. El usuario eligió guías locales sin IA externa. Las explicaciones
conservan los puntos y cobertura originales y no interpretan evidencia como instrucciones.
El agente actual `0.4.1-preview.1` conserva la distribución ZIP portátil win-x64 con runtime
incluido, manifiesto SHA-256 y diagnóstico local sin acceder a credenciales.
[Distribución y prueba privada](docs/AGENT_PRIVATE_BETA.md) detalla la ejecución
manual, actualización, retiro y matriz de compatibilidad. La comprobación en otra
PC Windows 11 x64 con su dueño autorizado permanece pospuesta por el usuario;
una beta externa sigue pendiente. La prueba en el anfitrión no equivale a validar una PC sin SDK.
El usuario confirmó que por ahora solo tiene la PC actual y pidió avanzar en
software. Se adelantó la [comparación manual de análisis](docs/ANALYSIS_COMPARISON.md)
de 1.1: mismos propietarios/dispositivo, reglas y criterios compatibles, con
desconocidos e información nueva diferenciados de mejoras observadas.
Queda pendiente la prueba en otro equipo. El [control del historial](docs/ANALYSIS_HISTORY.md)
incorpora exportación JSON y borrado individual confirmado, incluyendo todos los
resultados y feedback. Una marca mínima con dispositivo, identificador de reporte
y fecha impide restaurarlo mediante reenvío; no conserva hash, puntos o comentario.
El agente interpreta `410`, conserva su credencial e informe local y solicita un
escaneo nuevo sin reintento automático. No se borran descargas, informes locales
o backups. Se incorporó la [exportación de datos de cuenta](docs/ACCOUNT_EXPORT.md)
desde Mi cuenta: perfil con nombre/correo y todos los equipos, informes/resultados,
feedback y marcas mínimas, incluidos revocados y registros antiguos sin dispositivo.
La lectura usa una instantánea coherente en PostgreSQL y DTO explícitos sin secretos
ni tablas internas de autenticación. Antes de publicar siguen los controles
de privacidad y operación: auditoría operativa, programación/copias externas,
recuperación real y conservación de contenido con una política explícita.
El [borrado de cuenta](docs/ACCOUNT_DELETION.md) exige la contraseña actual y
confirmación exacta, elimina los datos activos y cierra el acceso de sesiones
y dispositivos. Archivos locales, correos y copias tienen custodia independiente.
El [mantenimiento técnico](docs/RETENTION_MAINTENANCE.md) permite consultar y
purgar registros de autenticación inválidos antiguos por lotes, sin tocar contenido
o marcas mínimas. Su trabajo periódico permanece desactivado; los hashes de refresh
de sesiones activas se conservan completos para detectar reutilización.
Se incorporó [backup cifrado y ensayo aislado de recuperación](docs/DATABASE_BACKUPS.md).
El [inventario consolidado](docs/PENDING_FEATURES.md) distingue pendientes de
producción, funciones por etapa y la IA externa pospuesta por elección del usuario.
Se incorporó [actividad privada de seguridad](docs/SECURITY_ACTIVITY.md) desde
Mi cuenta, sin reconstrucción histórica. La consulta permite filtrar por acción
y días UTC y continuar a eventos anteriores, sin escribir datos o cargar todo el
historial. Se incorporó [monitoreo local](docs/OPERATIONAL_MONITORING.md)
de PostgreSQL y logs mínimos. [Sesiones activas](docs/ACCOUNT_SESSIONS.md) permite
consultar y cerrar accesos individuales desde Mi cuenta. El [cambio de contraseña](docs/ACCOUNT_PASSWORD.md)
permite usar la actual para definir una nueva, cerrar accesos web y registrar el cambio.
La [edición del nombre](docs/ACCOUNT_PROFILE.md) está disponible en Mi cuenta y
la navegación consulta el perfil actualizado sin renovar credenciales.
Se incorporaron [política inicial de contraseñas y capacidad de sesiones](docs/ACCOUNT_SECURITY_POLICY.md),
[reintentos SMTP](docs/MAIL_DELIVERY.md) y [verificación consolidada con CI preparado](docs/VERIFICATION.md).
El [cambio de correo](docs/ACCOUNT_EMAIL_CHANGE.md) exige confirmar ambas
direcciones y la contraseña actual antes de cerrar todas las sesiones web.
Cambio/reset de contraseña cancelan solicitudes pendientes y recuperación envía
un aviso posterior sin secretos; la entrega sigue dependiendo de la cola volátil.
El [procedimiento de recuperación](docs/RECOVERY_RUNBOOK.md) es una preparación:
no hay restauración real ejecutada ni objetivos operativos acordados.
Siguen pendientes auditoría operativa, políticas de
contenido, programación/copias externas y recuperación real con tratamiento de borrados.
El orden y los criterios
completos están en [ROADMAP.md](ROADMAP.md).

El cierre funcional local descrito en [RELEASE_1_0.md](docs/RELEASE_1_0.md)
incorpora Primeros pasos/acceso al agente, prioridades/historial y borradores de
políticas/contacto. El recorrido integrado con cuenta e informes ficticios pasó
desde registro hasta borrado y revocación de accesos; no ejecuta el agente real.
Siguen políticas finales, canal real de errores y preparación de publicación.
Los datos públicos del responsable siguen pendientes, con recordatorio solicitado.
Los pendientes operativos conservan su importancia antes de publicar, sin
desviar ahora el desarrollo a funciones de 1.1 o posteriores.

## Mantener el contexto

Antes de un nuevo cambio, consulta este archivo, la arquitectura y la etapa
correspondiente del roadmap. Al terminar una funcionalidad, registra su estado,
las pruebas realizadas y cualquier decisión nueva en los documentos
correspondientes. Un plan escrito no debe marcarse como implementación terminada.

La base anterior quedó en `b163da7`. El corte de avance «feat: completa el recorrido
local de la primera versión» guarda los bloques posteriores juntos por solicitud
del usuario: producto, controles de cuenta, herramientas operativas, pruebas y
documentación. [CHANGELOG.md](CHANGELOG.md) registra las verificaciones de cada
avance. Es un commit local; no supone push, release o despliegue.
