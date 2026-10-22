# Registro de cambios

Este archivo distingue los cambios del árbol de trabajo de los commits
existentes. Las etapas del [roadmap](ROADMAP.md) no representan versiones
publicadas. No se ha creado un nuevo commit ni una release durante la puesta
en marcha documentada aquí.

## Sin publicar — detección de API activa al iniciar, 2026-10-21

- Corregido el arranque repetido de `scripts/dev.ps1`: identifica la API por
  ruta del proyecto y configuración, comprueba su salud y reutiliza la instancia
  activa sin intentar recompilar sus DLL bloqueadas por Windows.
- Añadido `-Restart` explícito: verifica nuevamente proceso y fecha de creación,
  detiene solo esa instancia y espera la liberación del puerto antes de compilar.
  Los puertos de otras aplicaciones y las instancias en otra dirección se
  detectan antes de iniciar herramientas o migraciones.
- La compilación se ejecuta como paso visible; EF y `dotnet run` utilizan
  `--no-build`. Los errores reales de compilación ya no quedan resumidos solo
  como un fallo de migraciones. `migrate` y `test` avisan si sus binarios están en uso.
- Verificados inicio repetido, protección de puerto ajeno incluso con
  `-Restart`, configuración ocupada en otro puerto y opciones incompatibles.
  El reinicio real compiló con cero advertencias/errores; la base ya estaba
  actualizada y el frontend existente se conservó. Sin cambios de esquema o C#.

## Sin publicar — ingesta manual y puntos confirmados, 2026-10-21

### Implementación

- Ingesta `POST /api/agent/analyses` con autenticación Device exclusiva y
  propietario/dispositivo derivados de la credencial. Esquema 2 con UUID de
  reporte estable y diez resultados versión 1; rechazo de estructura, fechas,
  reglas o versiones no admitidas. Cuerpo acotado a 64 KiB y límite
  predeterminado de seis envíos por minuto por IP.
- Catálogo del servidor para títulos, severidad, recomendaciones y pesos.
  Cálculo versión 1 de puntos confirmados y cobertura; desconocidos no suman
  puntos ni se interpretan como riesgos probados, no aplicables se excluyen.
  Sin resultados conocidos, el puntaje es `null`.
- Idempotencia por dispositivo/reporte con hash canónico; reintentar el mismo
  contenido devuelve lo guardado y contenido distinto con el mismo UUID
  produce conflicto. Fechas declaradas de captura separadas de la recepción.
- Esquema 2 en los informes locales, `scan -Send` y `send` para envío manual
  explícito. `scan` mantiene su funcionamiento local por defecto y los
  informes anteriores requieren un escaneo nuevo para enviarse. La credencial
  permanece protegida con DPAPI y ligada al destino de API.
- Resultados, puntos confirmados, cobertura y estados parciales en la web;
  conservación del historial legacy sin inventar cobertura o reglas. La
  migración amplía metadatos/estados y admite `Score=null`, sin borrar usuarios,
  dispositivos o análisis existentes.
- Guía `docs/ANALYSIS_INGESTION.md`, ADR-0015 e inventario de datos actualizados.
  El resultado sigue siendo limitado y autodeclarado: no hay atestación,
  remediación, servicio, envío automático o proveedor de IA.

### Validación del bloque

- Pasaron **82/82 pruebas del backend**, **127/127 del agente** y **26/26
  unitarias web**, sin omisiones, además del chequeo de tipos. Las suites .NET
  cubren los contratos, catálogo, puntaje, estados parciales, límites, DPAPI e
  integración con la API mediante EF InMemory.
- Pasaron **31/31 E2E** con PostgreSQL, Mailpit y web de producción reales.
  Incluyen recepción concurrente idempotente, metadatos no admitidos,
  credenciales separadas, aislamiento y carreras de envío con revocación,
  recuperación y revinculación. Todos desconocidos conserva puntaje nulo.
- El build de producción de Next.js y la compilación Release de la solución
  pasaron; esta última sin advertencias ni errores. EF no detectó cambios de
  modelo pendientes tras la migración.
- Aplicada `20261021192739_AgentAnalysisIngestion` tras una copia de seguridad
  local fuera del repositorio. Se conservó el índice de dispositivo existente,
  quedaron cinco migraciones aplicadas y diez reglas con peso total 100.
- La comparación de filas completas confirmó que el usuario, dispositivo y
  credencial anteriores quedaron idénticos. La limpieza dejó cero cuentas
  E2E, análisis y hallazgos de prueba; las reglas del catálogo sí permanecen.
- Revisadas vistas a 360 y 1440 px con datos sintéticos, sin desborde ni errores
  de página. La evidencia HTML se mostró como texto, sin ejecución. No se
  enviaron las comprobaciones personales del usuario durante las pruebas.
- La instancia de QA utilizó límites ampliados únicamente en su proceso;
  los límites predeterminados y sus pruebas 429 se conservaron. Se detuvieron
  los servidores temporales en 3001/5179 y se reinició la API de desarrollo
  para cargar las rutas nuevas, conservando la web del usuario en 3000.

## Sin publicar — agente Windows de consola, 2026-10-21

### Implementación

- Añadido `SecurityCenterAI.Agent`, consola .NET 8 Windows manual con `pair`,
  `heartbeat`, `status`, `scan`, `forget` y `help`, más `scripts/agent.ps1`.
- Vinculación mediante entrada interactiva oculta y código de un uso; sin
  contraseña, JWT o refresh de la cuenta y sin código como argumento CLI.
  Credencial, URL de la API y metadatos protegidos con DPAPI CurrentUser en
  LocalAppData, fuera del repositorio y su OneDrive. Sin tokens en salida.
- Cliente HTTP con URL raíz validada, HTTPS salvo localhost/127.0.0.1/[::1]
  exactos, redirecciones deshabilitadas, límites de respuesta y timeout.
  La credencial queda ligada a su API. No hay reintento automático del canje.
- Diez comprobaciones locales versionadas y de solo lectura: Defender antivirus,
  tiempo real y firmas; perfiles efectivos del firewall; Secure Boot; TPM;
  BitLocker del volumen del sistema; servicio Windows Update; EnableLUA; SMB1
  del servidor. Se selecciona evidencia y se distinguen datos desconocidos o no
  aplicables de una comprobación favorable. Sin remediación o elevación automática.
- Informe JSON local al solicitar `scan`, con estados, evidencia, severidad y
  recomendación. Sin ingesta, puntaje ni análisis nuevos en la web. El heartbeat
  solo envía versión; la plataforma no se reporta en este protocolo.
- `forget` elimina únicamente la credencial local. La revocación remota sigue
  en la web; los informes locales no se borran automáticamente.
- Documentados comandos, criterios, permisos, DPAPI y fuentes Microsoft en
  `docs/WINDOWS_AGENT.md`, inventario de privacidad y ADR-0014. El siguiente
  bloque del roadmap será la ingesta autenticada y el puntaje determinista.
- Actualizados los textos de dashboard, dispositivos y código para indicar que
  la consola está disponible y que los informes permanecen locales. Sin cambios
  de contrato o ingesta.

### Validación del bloque

- Compilación del agente aprobada sin advertencias ni errores.
- Compilación final de toda la solución aprobada con
  `dotnet build SecurityCenterAI.sln -c Release --no-restore`, sin advertencias
  ni errores.
- Pasaron **84/84 pruebas del agente**, sin omisiones, con configuración
  `AgentQa`: validación de destinos, HTTP acotado, protección y lectura DPAPI,
  informes locales y reglas con datos controlados. Incluyen tres pruebas de
  integración con `WebApplicationFactory`, controladores reales y EF InMemory
  para canje, heartbeat, separación de credenciales y revocación/reemisión.
  No sustituyen un canje de consola interactiva sobre PostgreSQL real.
- Pasaron **46/46 pruebas de regresión del backend** en `AgentQa`. Las pruebas
  DPAPI se ejecutaron en Windows real; no se afirma validación de esa suite
  en Linux ni compatibilidad de la consola fuera de Windows.
- `scripts/agent.ps1 -Action scan` ejecutado en Windows real sin elevar permisos:
  terminó con código 0 y diez resultados, conservando estados desconocidos cuando
  faltaban permisos o datos. Se generó el JSON local fuera de OneDrive y se
  comprobó esquema 1, diez identificadores de regla únicos y ausencia de los
  campos personales excluidos del informe.
- Este bloque no añade migraciones ni cambia la API. No se canjeó ni revocó el
  dispositivo personal del usuario durante la comprobación local y no existe
  una credencial local suya creada por estas pruebas.
- `help` y `status` del helper terminaron con código 0; el estado sin credencial
  local se presentó correctamente.
- Repetidas las **21/21 unitarias web** y el chequeo de tipos con resultado
  correcto tras los cambios de texto. No se repitieron el build de producción o
  los 24 E2E del bloque anterior para esos textos; sus resultados históricos
  se conservan debajo.

Las instancias existentes de API en 5169 y web en 3000 se conservaron; no se
iniciaron servidores de QA propios de este bloque. Las 24 E2E del bloque
anterior que aparecen debajo son evidencia histórica y no validación de la consola.

## Sin publicar — dispositivos y base de análisis, 2026-10-21

Bloque implementado y comprobado localmente. Se incorporan gestión de
dispositivos, vinculación temporal y credenciales propias, junto con el modelo de reglas y hallazgos.
Al cierre de esta etapa todavía no existía un agente Windows, ingesta de
resultados ni cálculo de puntajes; la consola se incorporó en el bloque posterior
documentado arriba.
Los resultados de los bloques anteriores se conservan debajo y no acreditan
por sí solos la validación de este cambio.

- Añadidas web `/devices` y rutas del propietario para listar, crear, consultar,
  emitir código y revocar. Crear y emitir código exige correo verificado en BD;
  un índice parcial limita a uno no revocado por cuenta.
- Añadidos código de vinculación de un uso de diez minutos y credencial opaca
  propia de 90 días, ambos con hashes SHA-256. La credencial utiliza scheme
  `Device`, limitado a heartbeat y separado de Bearer de cuenta.
- Regenerar invalida códigos previos; el canje nuevo sustituye la credencial
  mediante transacción. Revocar dispositivo invalida códigos y credenciales.
  Reset también los revoca sin borrar el dispositivo; logout-all sigue actuando
  solo sobre sesiones web.
- Añadidas reglas con id y versión positiva, y hallazgos con referencia compuesta,
  severidades acotadas e instantáneas de evidencia y recomendación. Lecturas de
  hasta 100 dispositivos, 20 análisis por detalle y 100 hallazgos por análisis.
- Generada `20261021123143_DevicesAndFindingFoundation`: cinco tablas nuevas y
  relación opcional `security_analyses.id_dispositivo`, conservando los análisis
  legacy. Se aplicó a PostgreSQL tras una copia local de seguridad, sin eliminar
  tablas o datos en `Up`; quedaron cuatro migraciones aplicadas.
- La comparación de la cuenta previa coincidió antes y después; los cero
  análisis anteriores permanecieron sin cambios. Se revisaron claves foráneas
  e índices y se comprobaron PostgreSQL y Mailpit saludables.
- Corregida durante la revisión una carrera de vinculación/reemisión con reset:
  esas operaciones avanzan el marcador de concurrencia del usuario, en lugar
  de escribir su mismo valor. La carrera pair/reset quedó cubierta por los E2E
  con PostgreSQL descritos debajo.
- Pasaron **46/46 pruebas del backend**, sin omisiones: 30 anteriores y 16
  nuevas para verificación en BD, aislamiento, códigos, hash, expiración,
  reemisión, heartbeat, revocación, reset y detalle de resultados almacenados.
  Usan InMemory, reloj controlable y captor de correo; no validan índices
  parciales ni carreras relacionales por sí solas.
- Pasaron **21/21 unitarias web**, el chequeo de tipos y el build de producción
  con `/devices` y `/devices/[id]`.
- Pasaron **24/24 E2E** con PostgreSQL y Mailpit reales: 17 anteriores y siete
  nuevos. Cubren el límite concurrente de un dispositivo, canje único con dos
  solicitudes, separación Device/Bearer, aislamiento, revocación por reset y
  carrera pair/reset con un reintento del reset cuando hubo conflicto.
- La UI de dispositivos se comprobó a 360 px: creación, reemisión, cancelación
  y confirmación de revocación y ausencia de análisis. Las consultas de hallazgos
  ligados a versiones de regla se comprobaron con datos sintéticos en EF InMemory;
  los E2E nuevos de dispositivos mantuvieron su historial vacío. Ninguno de esos
  datos representa una verificación de Windows.
- En una comprobación adicional se creó una cuenta temporal y una regla,
  análisis y hallazgo sintéticos en PostgreSQL real. El detalle web mostró los
  resultados y la evidencia con `<script>` como texto, sin ejecución ni errores.
  Se revisaron capturas a 360 y 1440 px, sin desborde ni códigos visibles.
- Los fixtures terminaron su limpieza sin error. Se configuró
  `PLAYWRIGHT_NO_COPY_PROMPT=1`, además de mantener trazas, vídeos y capturas
  automáticas apagados, para reducir la aparición de códigos visibles en
  artefactos de fallos.
- La comprobación SQL final conservó la cuenta original con sus valores
  idénticos, cero cuentas E2E y cero dispositivos, códigos, credenciales,
  reglas, hallazgos o análisis de prueba; permanecen cuatro migraciones aplicadas.
  EF tampoco detectó cambios de modelo pendientes.
- La QA utilizó 3001/5179, separados de las instancias del usuario en 3000/5169,
  que se conservaron. Las instancias de QA se detuvieron y esos puertos quedaron
  sin listeners. En esa entrega se indicó reiniciar una API previa para cargar
  rutas y mapeos nuevos; era una instrucción para instancias antiguas, no un
  fallo persistente. Después el usuario confirmó la creación y consulta de su
  dispositivo. Este cambio no publica la aplicación.
- La instancia de QA usa umbrales de solicitudes ampliados solo en su proceso
  para ejecutar los E2E. No modifica los límites predeterminados del producto;
  las comprobaciones 429 del backend son evidencia separada de esa suite.

## Sin publicar — columnas de users en español, 2026-10-21

- Implementado el renombrado de las once columnas de `users` mediante mapeos EF,
  conservando propiedades y contratos existentes.
- Generada y revisada `20261021120952_RenameUserColumnsToSpanish`: tanto `Up`
  como `Down` usan once renombrados de columna y uno del índice único de correo,
  `IX_users_Email` / `IX_users_correo_electronico`. No recrea columnas o tablas
  ni modifica los valores de las cuentas.
- Las propiedades C# y contratos JSON mantienen sus nombres. No se traducen
  la tabla `users` ni las columnas de las otras tablas.
- Añadida la tabla de equivalencias y explicaciones para DBeaver en el README;
  actualizados arquitectura, inventario de privacidad y ADR-0012.
- Aplicada `20261021120952_RenameUserColumnsToSpanish` a PostgreSQL tras una
  copia temporal de seguridad. La comparación de los once valores de la cuenta
  previa coincidió antes y después, sin publicar sus datos.
- Verificadas la clave primaria, la unicidad del correo y las referencias desde
  análisis, sesiones y tokens de cuenta a `users.id_usuario`. El índice único
  conserva su función con el nombre `IX_users_correo_electronico`.
- EF no detectó cambios de modelo pendientes. Pasaron **30/30 pruebas del
  backend y 17/17 E2E** sobre las columnas en español; los fixtures SQL se
  adaptaron a los nombres nuevos. No cambiaron las fuentes de la web ni se
  realizó otro build de frontend para este renombrado.
- La consulta SQL final confirmó una cuenta previa con sus once valores
  idénticos al estado anterior, cero cuentas E2E y las tres migraciones aplicadas.
- Se detuvieron las instancias de QA en 3001/5179. Se conservaron las del usuario
  en 3000/5169; la API anterior debe reiniciarse con el helper para cargar el
  mapeo nuevo. Los resultados del bloque de cuentas que siguen son antecedentes
  separados de esta comprobación.

## Sin publicar — bloque de cuentas y sesiones, 2026-10-21

Este bloque amplía la primera web. El código y el recorrido local de cuentas
están implementados y comprobados. Los resultados del 20 de octubre que aparecen
después son antecedentes separados de la validación nueva que se detalla aquí.

### Cambios de cuentas

- Añadidos modelos para sesiones, credenciales de renovación y tokens de cuenta,
  con hashes, fechas de consumo/revocación y marcadores de concurrencia.
- Generada la migración `20261021114159_AccountSessionsAndRecovery`: agrega seis
  columnas a `users` y tres tablas, sin eliminar cuentas o análisis. Aplicarla
  requiere nueva autenticación para las sesiones previas y deja sus correos
  pendientes de verificar.
- Ampliado el usuario con estado de verificación de correo, intentos de login,
  bloqueo temporal y fechas para limitar envíos.
- Definido el contrato de autenticación con access y refresh tokens, ambos
  vencimientos y `emailVerified` en el perfil.
- Asociados los access tokens a sesiones mediante `sid`, con comprobación en la
  base por solicitud. Vigencia actual de 15 minutos; logout y cierre global
  revocan sesiones. El refresh opaco rota y conserva un límite absoluto de
  sesión de 30 días; su reutilización revoca la sesión correspondiente.
- Incorporado envío SMTP de verificación y recuperación mediante enlaces de un
  uso; validación de SMTP local en desarrollo y SMTP autenticado/TLS fuera de él.
- Verificación con vencimiento de 24 horas y recuperación de 30 minutos.
  Un nuevo enlace invalida los anteriores del mismo propósito. El reset invalida
  enlaces de recuperación pendientes y revoca las sesiones, sin autologin.
- Añadidos bloqueo de login de 15 minutos tras cinco fallos, hash de
  comparación para cuentas inexistentes, mínimo de 300 ms en recuperación,
  espera de 60 segundos por cuenta/tipo de correo y límites en memoria por IP.
- Añadida cola de correo volátil con capacidad de 100 mensajes, worker SMTP y
  advertencias genéricas sin destinatarios ni tokens. Sin entrega durable.
- Agregado Mailpit `v1.31.5` local: SMTP 1025 y web 8025 ligados a loopback,
  hasta 200 mensajes y sin volumen persistente. El helper inicia ambos servicios.
- Ampliado el helper con `-Configuration` (Debug por defecto, aplicado también a
  EF y pruebas) y `-Port` (5169 por defecto). Fija Development en el entorno del
  proceso y restaura su valor previo para evitar tratar la migración local como
  un arranque de producción y permitir una API de QA separada.
- Añadidas rutas web de cuenta, recuperación, reset, verificación y renovación,
  con cookies `HttpOnly` de access y refresh. La pantalla de cuenta permite
  reenviar verificación y cerrar todos los accesos; no lista sesiones individuales.
- Añadidos `no-referrer` y `no-store` para las páginas con tokens de correo,
  y exclusión de esas rutas en el log de entrada de desarrollo de Next.js.
- Actualizada la documentación del estado, flujos, inventario de datos y riesgos;
  registrados ADR-0010 y ADR-0011 sin borrar los antecedentes de ADR-0009.

### Validación de este bloque

- El 2026-10-21 pasaron **30/30 pruebas del backend**, sin omisiones: las seis
  anteriores y 24 nuevas para uso único, vencimiento y propósito de tokens,
  hashes, rotación y reutilización de refresh, revocación y cierre global,
  aislamiento de cuentas, access token vencido y sesión ajena, bloqueo, espera
  entre correos y 429 con `Retry-After`, incluso con `X-Forwarded-For` falsificado.
- Esas pruebas usan EF InMemory, `TimeProvider` controlable y un capturador de
  correo de prueba. No acreditan SQL, migraciones ni entrega SMTP reales.
- Pasaron 17/17 unitarias de la web, incluido el límite de 128 caracteres para
  contraseñas, y el chequeo de tipos. Se completó el build de producción sobre
  una copia temporal y su repetición final con las últimas fuentes, incluido
  el enlace directo del dashboard a cuenta y su texto estático.
- Se aplicó correctamente `20261021114159_AccountSessionsAndRecovery` a
  PostgreSQL real, tras una copia de seguridad local temporal fuera del
  repositorio. Antes de migrar había una cuenta previa y ningún análisis.
- Mailpit `v1.31.5` inició saludable y con ambos puertos ligados a loopback.
- Pasaron **17/17 E2E** con Chromium, web de QA en 3001, API en 5179,
  PostgreSQL y SMTP/Mailpit reales. Cubren los recorridos anteriores y correo
  de verificación/recuperación de un uso, reenvío, revocación y cierre global
  entre navegadores, renovación de cookies y aislamiento. Incluyen carreras
  reales de PostgreSQL para refresh, verificación y reset.
- Esos E2E no sustituyen las pruebas de límites 429 o de reloj controlado del
  backend. Sus fixtures terminaron la limpieza sin error, limitando cuentas y
  mensajes a los destinatarios únicos de cada prueba.
- La consulta SQL final conservó la cuenta previa y dejó cero cuentas E2E,
  análisis, sesiones, credenciales de refresh y tokens de acción. Los fixtures
  eliminaron los mensajes propios; no se borraron otros destinatarios.
- La revisión de navegador a 360 px de recuperación, reset y verificación devolvió
  200 sin cookies, sin desborde horizontal y sin errores de página. Se revisó la
  captura de recuperación. El dashboard enlaza directamente a cuenta y no usa
  `email_verified` del JWT antiguo para presentar un estado de correo actualizado.
- Se detuvieron las instancias de QA en 3001/5179. Las del usuario en 3000/5169
  quedaron intactas y PostgreSQL/Mailpit saludables. La API anterior necesita
  reiniciarse para cargar estos cambios; después se requiere login nuevo porque
  los JWT previos no tienen `sid` ni refresh.

Esta sección no afirma que los servidores estén activos ni que se haya publicado
la aplicación. Los controles descritos se contrastaron con el código del bloque;
su validación sobre servicios reales se registra por separado.

## Sin publicar — 2026-10-20

### Desarrollo local y documentación

- Agregado `global.json`: SDK base `8.0.400`, actualización dentro de su banda
  mediante `latestPatch` y exclusión de versiones preliminares.
- Agregado el manifiesto local `.config/dotnet-tools.json` con `dotnet-ef`
  `8.0.11`, alineado con EF Core del proyecto.
- Agregado `scripts/dev.ps1` con acciones `start`, `migrate` y `test`.
  El inicio espera a PostgreSQL, restaura herramientas, aplica migraciones e
  inicia la API con el perfil HTTP de desarrollo.
- El script importa los valores de `.env` sin ejecutar su contenido, construye
  la conexión mediante `DbConnectionStringBuilder` y configura los secretos
  solo en el entorno del proceso, restaurándolo al terminar.
- Actualizado `README.md` con requisitos, extensiones y comandos de ejecución
  que limitan `ExecutionPolicy Bypass` al proceso iniciado.
- Agregados `PROJECT_CONTEXT.md`, `ARCHITECTURE.md`, `ROADMAP.md`, `DECISIONS.md`,
  `SECURITY.md`, `PRIVACY_NOTES.md` y `CHANGELOG.md` para conservar el estado real,
  las decisiones, los datos tratados y los pendientes del proyecto.
- Ampliado `README.md` con el índice documental, la conexión desde DBeaver y
  soluciones a problemas habituales de ejecución.
- Conservada una copia sin modificaciones de la guía maestra original en
  `docs/Guia_Maestra_Security_Center_AI_Actualizada.pdf`.
- Agregado `.dockerignore` para excluir secretos locales, dependencias y
  resultados de compilación/pruebas del contexto de construcción Docker.

### Primer bloque de frontend hacia 0.5

- Agregada la aplicación `src/SecurityCenterAI.Web` con Next.js 16.4.2 App
  Router, React 19.3.1 y TypeScript 5.9.3; requiere Node.js 24.
- Implementadas páginas de registro, login y dashboard en español, adaptables
  al ancho disponible y conectadas a los datos de la API. Hay estados de carga,
  error y ausencia de análisis; no se inventan puntajes ni hallazgos.
- Agregadas Server Actions para registro, login y logout, y un cliente de API
  exclusivo del servidor. `API_BASE_URL` apunta por defecto a
  `http://localhost:5169`; la web se abre en `http://localhost:3000`.
- Guardado del JWT en cookie `security_center_session` con `HttpOnly`,
  `SameSite=Lax`, `Secure` en producción y vencimiento limitado por el token.
  La web no usa `localStorage` ni recibe la clave de firma de .NET.
- El dashboard se consulta desde Next.js con Bearer y .NET autoriza cada
  solicitud. El parser de la web solo extrae claims y vencimiento, sin verificar
  la firma. Logout elimina la cookie local; no revoca el JWT en el backend.
- Agregados comandos npm `dev`, `build`, `start`, `typecheck`, `test` y
  `test:e2e`, y documentado el inicio local con backend y frontend en terminales
  separadas. `typecheck` ejecuta `next typegen` antes de `tsc --noEmit` para
  funcionar también en una copia recién instalada.
- Registrada la decisión ADR-0009 y actualizado el inventario de datos y
  controles de sesión, conservando los antecedentes de la etapa documental.

### Correcciones del backend

- Ajustados los atributos de validación de los parámetros de los records de
  registro y login, para que ASP.NET Core valide las solicitudes correctamente.
- Deshabilitado el mapeo automático de claims entrantes de JWT con
  `MapInboundClaims = false`, conservando `sub` para identificar al usuario
  que consulta el dashboard.
- Limitada la publicación del puerto de PostgreSQL a `127.0.0.1:5432` en
  Docker Compose para el desarrollo local.
- Limitada la redirección HTTPS a entornos distintos de `Development`, para
  evitar el aviso de puerto HTTPS no configurado al usar el perfil HTTP local.
- Corregida una discrepancia en la base local: la columna `users."Name"`
  figuraba como `"NOMBRE"`. Se restauró el nombre esperado dentro de una
  transacción, conservando tablas, datos e historial de migraciones. La migración
  inicial ya era correcta; se agregó la explicación al apartado de problemas
  frecuentes del README.

### Pruebas y verificaciones realizadas

El 2026-10-20, durante la sesión de puesta en marcha:

- Se prepararon las herramientas locales de desarrollo, incluido Docker
  Desktop, y se verificó la compilación del backend.
- Se aplicó la migración inicial y se comprobó el flujo HTTP de registro,
  inicio de sesión y dashboard usando PostgreSQL en Docker.
- Pasaron seis casos automatizados: protección de contraseñas; registro,
  login y consulta de los últimos cinco análisis propios; dos casos de entrada
  inválida; y dos casos de acceso al dashboard sin token o con token inválido.
- Se incorporaron las dependencias de pruebas
  `Microsoft.AspNetCore.Mvc.Testing` y `Microsoft.EntityFrameworkCore.InMemory`.
  Los casos automatizados usan una base en memoria y no requieren Docker.
- Se preparó un `.env` local con secretos de desarrollo. Ese archivo está
  excluido por `.gitignore`; sus valores no forman parte del repositorio ni
  de este registro.
- Pasaron 16 pruebas unitarias del frontend con `node:test`: validación de
  credenciales, manejo de entradas malformadas, ausencia de contraseñas en
  estados de error y extracción de metadatos y vencimiento del JWT.
- Pasó el chequeo de tipos y el build de producción del frontend.
- Pasaron siete E2E Playwright con Chromium y servicios locales reales:
  registro, dashboard vacío, logout, login, acceso sin cookie, firma falsa,
  errores de formulario, duplicados, credenciales incorrectas, ancho móvil e
  historial. El caso de historial insertó siete registros sintéticos en
  PostgreSQL y verificó el total y los últimos cinco resultados. Las cuentas
  temporales y sus análisis se eliminaron mediante el fixture, por correo exacto
  y borrado en cascada.
- Se revisó la UI en escritorio y móvil y se ajustó la distribución de la
  tarjeta en pantallas pequeñas. Después del ajuste se repitieron correctamente
  el build y el caso E2E móvil, y se revisó su imagen final. Las trazas, vídeos y capturas automáticas
  están deshabilitados; las capturas de revisión son opcionales mediante
  `SC_AI_VISUAL_REVIEW=1`, con cuentas ficticias y sin tokens.

Estas verificaciones corresponden a esa sesión y no afirman que la API siga
activa. Los seis casos del backend usan InMemory; se complementaron con una
comprobación HTTP manual sobre PostgreSQL y después con siete E2E automatizados
del recorrido web sobre esa base real. Esto no acredita operación de producción.

En la revisión final también se comprobó el mensaje de servicio no disponible
con el backend detenido. No quedaron cuentas E2E en PostgreSQL y se detuvieron
los servidores web y API iniciados para las pruebas.

Después de reparar el nombre de columna en la base local, se repitieron las
seis pruebas del backend y los siete E2E con resultado correcto. La compilación
del backend usó una carpeta temporal para evitar interferir con la API activa.
Una instancia temporal en `Development` respondió por HTTP sin el aviso de
puerto HTTPS; se detuvo al terminar esa comprobación. La API ya iniciada requiere
reiniciarse para incorporar el cambio de redirección.

### Pendiente

Se conserva la base interna 0.1 y se agregó el primer bloque web hacia 0.5.
La beta privada sigue incompleta. Faltan mejoras de cuentas y sesiones,
agente Windows, dispositivos, verificaciones, hallazgos, cálculo real de puntaje
e IA. El dashboard consulta análisis
almacenados; no ejecuta un análisis de la computadora. Las comprobaciones
locales no publican la aplicación ni acreditan un despliegue de producción.

## Registro inicial del repositorio — 2026-07-25

Commit existente: `71aeab4` — `Initialize Security Center AI backend`.

- Estructura .NET 8 con proyectos API, Domain e Infrastructure.
- Registro, login, hash de contraseñas y JWT.
- Entidades básicas de usuario y análisis, PostgreSQL y migración inicial.
- Dashboard protegido, Swagger y endpoint `/health`.
- Configuración inicial de Docker, documentación y una prueba de contraseñas.

La fecha corresponde al commit de Git. La guía maestra se fechó el 2026-07-27
y denominó su documento «1.0»; eso no identifica una release del producto.
