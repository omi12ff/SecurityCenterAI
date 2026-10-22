# Contexto de Security Center AI

Última revisión: **2026-10-21**. Este archivo sirve para retomar el proyecto y
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

Tenemos la **base interna 0.1** y la web de cuentas y dashboard hacia la **beta
0.5**, que sigue incompleta. Tras el primer frontend se incorporaron
sesiones revocables, renovación, verificación de correo, recuperación
y límites de intentos. El agente de consola añade diez comprobaciones de Windows
y envío explícito a la API; la web presenta resultados, puntos confirmados y
cobertura por dispositivo. Las
verificaciones de cada bloque se conservan en [CHANGELOG.md](CHANGELOG.md).

| Área | Estado comprobable en el repositorio |
| --- | --- |
| API | ASP.NET Core en .NET 8, con proyectos API, Domain e Infrastructure. |
| Registro y login | Validación, correo normalizado, contraseña con hash, JWT asociado a sesión y bloqueo temporal por intentos. |
| Persistencia | PostgreSQL y EF Core: usuarios, análisis, sesiones y tokens de cuenta; migraciones versionadas. |
| Dashboard | Endpoint protegido que devuelve el último puntaje, el total y hasta cinco análisis recientes del usuario. |
| Swagger | Configurado para el entorno Development. |
| Estado de la API | `/health` devuelve una respuesta fija; no comprueba la base de datos. |
| Desarrollo local | Docker Compose para PostgreSQL y Mailpit; `scripts/dev.ps1` para iniciar, migrar o probar. |
| Pruebas del backend | 82/82 aprobadas el 2026-10-21 con EF InMemory, reloj controlable y correo de prueba; complementadas por E2E sobre PostgreSQL y SMTP reales. |
| Frontend Next.js | Next.js 16.4.2, React 19.3.1 y TypeScript 5.9.3; registro, login, dashboard y gestión de cuenta en español. |
| Sesión web | Access y refresh tokens en cookies `HttpOnly`, `SameSite=Lax`, `Secure` en producción; renovación rotatoria y revocación en el backend. |
| Correo y recuperación | SMTP; Mailpit local, verificación de correo y recuperación mediante tokens de un uso. |
| Límites | Rate limiting en memoria por IP, espera entre correos y bloqueo temporal por fallos de login. |
| Pruebas del frontend | 26/26 unitarias, tipos, build y 31/31 E2E aprobados; web, API, PostgreSQL y SMTP/Mailpit reales para los E2E. |
| Dispositivos | Web `/devices`, correo verificado, código de un uso, credencial propia y revocación. Un dispositivo no revocado por cuenta. |
| Agente Windows | Consola .NET 8 de uso manual: vinculación, credencial DPAPI CurrentUser, heartbeat, estado local y olvido de credencial. Sin instalador o servicio. |
| Comprobaciones locales | Diez reglas de solo lectura con estados explícitos, evidencia seleccionada e informe JSON local al solicitar `scan`; `scan -Send` o `send` solicitan el envío manual. |
| Pruebas del agente | 127/127 aprobadas en Windows, con DPAPI real, consultas/HTTP controlados, lectura y envío de informes e integración con API/EF InMemory; separadas de las pruebas E2E PostgreSQL. |
| Reglas y hallazgos web | Ingesta con credencial Device, esquema 2, reintentos idempotentes y diez resultados por reporte. El servidor decide títulos, recomendaciones, severidad y pesos con su catálogo. |
| Puntaje | Puntos confirmados deterministas, cobertura y cuatro conteos de estado. Desconocido no equivale a correcto ni a riesgo comprobado; sin resultados conocidos el puntaje es `null`. |
| IA y detector de enlaces/mensajes | Pendientes. No hay un proveedor de IA integrado. |
| Alertas, automatización y suscripciones | Pendientes, según el roadmap. |

`SecurityAnalysis` incorpora una relación opcional a dispositivo para conservar
los análisis previos. `POST /api/agent/analyses` recibe observaciones acotadas
del cliente y crea el análisis en el dispositivo de su credencial. No ejecuta
cmdlets remotos ni prueba la veracidad del informe. Los datos de cuenta se
devuelven en registro y login; aún no existe un endpoint para editar el perfil.

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
operación del correo, retención, auditoría y despliegue.

La [ingesta manual](docs/ANALYSIS_INGESTION.md) conecta esquema 2, fechas,
reglas versionadas, puntos confirmados y cobertura con el historial del equipo.
Los informes previos del esquema 1 requieren un `scan` nuevo para enviarse.
El siguiente bloque funcional será diseñar explicaciones sobre la evidencia
disponible y preparar una beta con usuarios; el proveedor de IA no está elegido.
El orden y los criterios
completos están en [ROADMAP.md](ROADMAP.md).

## Mantener el contexto

Antes de un nuevo cambio, consulta este archivo, la arquitectura y la etapa
correspondiente del roadmap. Al terminar una funcionalidad, registra su estado,
las pruebas realizadas y cualquier decisión nueva en los documentos
correspondientes. Un plan escrito no debe marcarse como implementación terminada.

Los cambios de preparación del entorno, frontend y documentación están pendientes de
commit al momento de esta revisión. [CHANGELOG.md](CHANGELOG.md) diferencia esos
cambios de los antecedentes del commit inicial.
