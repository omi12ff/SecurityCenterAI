# Seguridad de Security Center AI

Revisión documental: 26 de octubre de 2026.

Este documento describe el backend, la web de desarrollo existente y los controles
pendientes. La guía maestra define objetivos del producto; sus requisitos no
equivalen a funciones implementadas. Hay una primera web de autenticación y
dashboard y una prueba de concepto del agente Windows de consola, con ingesta
manual y puntos confirmados limitados a diez reglas. El adaptador de explicación
de IA por análisis está desactivado por defecto. En esta instalación el usuario
habilitó la configuración con una clave de producto y la ruta explícita
`openrouter/openrouter/free`. Una prueba real del adaptador produjo diez
explicaciones válidas a partir de estados ficticios, sin usar el informe personal.
La consulta se solicita con botón junto al aviso de datos, sin casilla. Los
controles de publicación siguen pendientes.

## Descarga privada del agente

Primeros pasos exige sesión y deriva el avance solo de los datos propios que
autoriza la API. La descarga vuelve a consultar el perfil para rechazar sesiones
revocadas y correos no verificados. El navegador no elige rutas, versiones o URLs.
Un ZIP configurado por el operador se comprueba contra su checksum antes de
servirlo desde el mismo descriptor, con tamaño acotado y cabeceras de adjunto,
no-store, no-referrer y nosniff. Los errores no incluyen rutas o excepciones.

La carpeta y ambos archivos deben ser controlados por el operador; una suma no
equivale a una firma de editor. No hay limitador específico de descargas o
distribución pública aprobada. Los comandos visibles no incluyen códigos ni
se ejecutan desde la web. [Controles y límites](docs/GETTING_STARTED.md).

## Reportar una vulnerabilidad

En la [sección Security del repositorio](https://github.com/omi12ff/SecurityCenterAI/security),
utiliza **Report a vulnerability** si GitHub ofrece esa opción. Su disponibilidad
depende de que los mantenedores hayan habilitado los reportes privados; este
documento no confirma que lo estén.

Si esa opción no aparece y no se ha acordado otro canal privado, abre únicamente
un issue sin información sensible para solicitar uno. No publiques credenciales,
tokens, datos personales, volcados de la base ni instrucciones que permitan
explotar una vulnerabilidad pendiente. No hay un correo de seguridad ni plazos
de respuesta establecidos en este repositorio.

En el canal privado acordado, incluye la versión o commit, el componente
afectado, el impacto observado y pasos mínimos reproducibles con datos ficticios.
Sanea capturas y registros antes de compartirlos. Si se expuso una credencial,
cambiarla o revocarla es necesario aunque se elimine el mensaje o archivo.

## Controles existentes

| Área | Comportamiento implementado | Alcance y límites |
| --- | --- | --- |
| Contraseñas | Hash mediante `PasswordHasher<User>`, recuperación y cambio autenticado. Nuevas claves: mínimo 15 puntos Unicode, máximo 128 unidades UTF-16 y lista local inicial de valores previsibles; sin consulta externa, recorte o composición obligatoria. Login acepta claves anteriores. | Lista inicial pequeña; faltan corpus amplio de filtraciones, MFA y revisión integral antes de publicar. [Política y límites](docs/ACCOUNT_SECURITY_POLICY.md). |
| Entrada de autenticación | Se validan campos obligatorios, formato de correo y nombre de 2 a 100 caracteres. El correo se recorta, se convierte a minúsculas y tiene un índice único. | El registro informa si un correo ya existe. Falta revisar abuso y enumeración de cuentas antes de exponerlo públicamente. |
| JWT y revocación | HMAC SHA-256; validación de firma, emisor, audiencia y expiración, más consulta de la sesión `sid` en la base en cada solicitud autenticada. | Access token de 15 minutos y tolerancia de reloj de 30 segundos. Exige clave de al menos 32 caracteres; debe ser aleatoria. El token está firmado y es legible, sin cifrado. Revocar la sesión invalida sus JWT. |
| Sesiones activas | Consulta Bearer por propietario, páginas de 50 y cierre confirmado con evento atómico. Diez habilitadas por cuenta, configurable 1–100, coordinadas por versión de usuario; refresh conserva su lugar. | No expone credenciales/IP, cancela solicitudes ya autorizadas o revoca equipos. Reducir el límite no expulsa accesos existentes. Proxies y operación distribuida requieren revisión. [Uso](docs/ACCOUNT_SESSIONS.md). |
| Nombre del perfil | Bearer por propietario, sesión revalidada, bloqueo/versión y DTO de un campo. Nombre limitado, sin controles, representado como texto en la web. | No modifica correo, contraseña o accesos. No renueva tokens ni amplía su vigencia; la navegación consulta el perfil guardado. Sin evento nuevo de seguridad. [Alcance](docs/ACCOUNT_PROFILE.md). |
| Cambio de contraseña | Bearer activo, contraseña actual, confirmación, límite específico y contador/bloqueo compartido. Fila de usuario bloqueada, sesión revalidada y escritura atómica de hash, cierre de sesiones, recuperación pendiente y evento. | Clave nueva distinta y política local 15–128; lista inicial sin corpus completo de filtraciones. Aviso posterior de mejor esfuerzo; agentes conservan acceso. [Uso](docs/ACCOUNT_PASSWORD.md). |
| Renovación | Refresh opaco aleatorio de 32 bytes, persistido mediante SHA-256, con rotación y vigencia de 30 días. | La reutilización de un refresh consumido revoca su sesión completa. También puede ocurrir por solicitudes concurrentes: la web debe coordinar renovaciones y pedir login si la sesión se revoca. La purga opcional conserva todos los hashes de sesiones activas y solo los elimina por cascada al quitar sesiones cerradas antiguas. |
| Mantenimiento técnico | Vista previa, aplicación manual confirmada, lotes por categoría y bloqueo asesor transaccional en PostgreSQL. Worker desactivado por defecto. | Solo sesiones/tokens/códigos/credenciales cerrados o inválidos después del plazo. No purga perfiles, contenido ni marcas de informes eliminados. Los plazos iniciales requieren revisión para producción; [operación](docs/RETENTION_MAINTENANCE.md). |
| Sesión del navegador | Server Actions y cookies de access y refresh con `HttpOnly`, `SameSite=Lax`, `Path=/` y `Secure` en producción. No se usa `localStorage`. Inventario, cierre individual y global se tramitan en la API. | El parser solo extrae metadatos; .NET valida firma y estado de sesión. Conservar las protecciones de origen de las Server Actions y revisar proxies y TLS antes de publicar. |
| Correo | Tokens aleatorios, hashes de un uso y cola cifrada persistente, atómica con la operación de cuenta. Reservas PostgreSQL, reintentos, vigencia y revalidación de enlaces. Claves fuera del repo bajo DPAPI del usuario Windows de la API. | No garantiza entrega final o exactamente una vez. Otra identidad/máquina/alojamiento requiere claves accesibles y protegidas acordadas. SMTP local Mailpit; fuera de desarrollo exige autenticación/TLS. Sin proveedor elegido. [Límites](docs/MAIL_DELIVERY.md). |
| Abuso de autenticación | Bloqueo de cuenta de 15 minutos tras 5 fallos; respuestas genéricas, hash de comparación para login inexistente y mínimo de 300 ms para recuperación. Espera de 60 segundos por cuenta y tipo de correo. Rate limiting por IP: global 120/minuto, autenticación 60/minuto, correo 40/minuto. | El mínimo de tiempo reduce diferencias; no garantiza tiempos idénticos. Los límites por IP viven en memoria y se reinician con el proceso. Next.js comparte su IP entre usuarios. No se confía en `X-Forwarded-For`; faltan estrategia de proxies y varias instancias. |
| Comunicación web/API | `API_BASE_URL` solo se usa en el servidor Next.js. El dashboard se consulta con Bearer y `cache: no-store`; la clave de firma permanece en .NET. | El desarrollo usa HTTP en localhost. El despliegue requiere TLS, revisión de los orígenes y las protecciones de las acciones del servidor. La cookie no sustituye la autorización de la API. |
| Dashboard | Requiere autenticación y un `sub` que pueda interpretarse como UUID. Las consultas filtran por ese usuario. | Devuelve el puntaje más reciente, el total y hasta 5 análisis propios. No genera análisis ni puntajes. Los recursos de dispositivos tienen su autorización propia. |
| Dispositivos | Operaciones del propietario con Bearer y comprobación de correo verificado; límite de un dispositivo no revocado por cuenta. Código de vinculación de un uso y diez minutos. | La posesión del código permite vincular el cliente. No incluirlo en URLs o registros compartidos. Un registro pendiente también ocupa el lugar disponible. |
| Credencial de cliente | Credencial opaca propia de 90 días y esquema `Device`, con revocación y comprobación en la base. Autoriza heartbeat e ingesta del dispositivo al que pertenece. | No sustituye Bearer del usuario ni acredita el estado del PC. Regenerar código mantiene la credencial anterior hasta el canje; revocar dispositivo la invalida de inmediato. |
| Custodia del agente | Código leído con entrada oculta; credencial y destino protegidos con DPAPI CurrentUser en LocalAppData, fuera del repositorio. Sin secretos por argumentos o salida. | DPAPI vincula la protección al usuario de Windows; otro proceso de ese usuario puede descifrarla. No protege una sesión comprometida ni sustituye la revocación remota. |
| Cliente HTTP del agente | HTTPS obligatorio salvo HTTP en localhost o IP loopback exactos; redirecciones deshabilitadas, respuestas y tiempos acotados. | La excepción HTTP es para desarrollo local. No se deshabilita la validación de certificados. No hay reintento automático de canje de un uso. |
| Comprobaciones de Windows | Diez consultas locales predeterminadas y de solo lectura, con evidencia seleccionada y estados de desconocido/no aplicable. | Sin remediación, comandos remotos, servicio o elevación automática. Un permiso insuficiente nunca prueba un resultado favorable. Los JSON locales están en texto claro y no son una auditoría completa. |
| Ingesta de resultados | Esquema 2, diez reglas/versiones conocidas sin duplicados, fechas acotadas, cuerpo de hasta 64 KiB y límite predeterminado de 6 envíos/minuto por IP. Identidad de cuenta/dispositivo obtenida de Device. | El cliente no elige propietario, puntaje, títulos, severidad, recomendaciones o pesos. La evidencia y estado son declaraciones del cliente: no hay atestación del binario, del hardware ni de Windows. |
| Reintentos y puntaje | Identificador de reporte único por dispositivo y hash canónico para reintentos idénticos; contenido distinto con el mismo id produce conflicto. Catálogo del servidor y cálculo versión 1 con desconocidos/no aplicables explícitos. | Idempotencia no es firma, cifrado ni prueba de origen físico. Puntos confirmados y cobertura son criterios limitados; no representan una certificación ni probabilidad de protección. |
| Secretos de desarrollo | `.gitignore` excluye `.env` y `appsettings.Local.json`; `.dockerignore` excluye secretos locales, dependencias y artefactos del contexto Docker. `scripts/dev.ps1` lee los cuatro valores admitidos de `.env` y prepara variables del proceso para Compose y .NET. | `.env` es un archivo local de texto, sin cifrado del proyecto. La exclusión de Git no elimina secretos ya versionados. .NET no carga `.env` automáticamente y no hay carga explícita de `appsettings.Local.json`. |
| PostgreSQL local | Compose publica el puerto como `127.0.0.1:5432:5432` y usa un volumen persistente. | Limita la exposición del puerto al equipo local; otros procesos locales y las redes internas de Docker tienen su propio acceso. No hay configuración de cifrado de datos o backups automáticos en el proyecto. |
| Backup manual | Archivo cifrado AES-256-GCM con contraseña, permisos de usuario/SYSTEM y verificación de restauración en una base temporal. | Incluye hashes y registros internos de autenticación. No hay copia remota, programación ni restauración sobre el servicio activo. Contraseña independiente de `.env`; [custodia y límites](docs/DATABASE_BACKUPS.md). |
| Ensayo de recuperación | `rehearse` verifica la copia y retira sesiones, tokens, códigos/credenciales, solicitudes y cola solo en su temporal, revocando equipos y renovando versiones. Huellas de contenido y transacción con rollback. Rechaza destinos/esquemas no revisados. | La temporal se elimina. No es recuperación activa, conciliación de borrados futuros, rotación de contraseñas/secretos o sandbox de SQL malicioso. [Alcance](docs/RECOVERY_REHEARSAL.md). |
| Actividad privada | Catorce acciones completadas se registran en la misma escritura/transacción. Consulta Bearer por propietario, filtros validados y continuación por un evento propio dentro de la selección. Sin datos libres o secretos; EF rechaza cambios/borrados salvo al eliminar la cuenta. | Hasta 50 por consulta y exportación completa sin filtros. No cubre intentos fallidos ni todas las operaciones. SQL directo/administradores pueden alterar la tabla; falta auditoría externa, conservación y monitoreo. [Alcance](docs/SECURITY_ACTIVITY.md). |
| Cambio de correo | Correo actual verificado, contraseña al solicitar/finalizar, tokens de propósito separado y confirmación de ambas direcciones. Fila de usuario bloqueada, sesión revalidada y escritura atómica con cierre web/evento. | No es MFA o recuperación sin acceso al correo actual. Confirmar una dirección no cambia el perfil. Avisos posteriores de mejor esfuerzo; agentes conservan acceso. [Uso](docs/ACCOUNT_EMAIL_CHANGE.md). |
| HTTP y errores | Hay redirección HTTPS fuera de `Development`, errores genéricos y correlación por id generado por el servidor. Swagger se habilita en `Development`. CORS admite el origen configurado en `FrontendUrl`. | El helper utiliza el perfil HTTP local. Esto no acredita TLS de producción. CORS no sustituye la autorización. |
| Monitoreo y logs | Readiness PostgreSQL acotado y logs mínimos. Diagnóstico local de cola: consulta agregada READ ONLY con contadores/fechas, sin payloads, claves, SMTP o endpoint nuevo. | Observación puntual, sin prueba de worker activo/entrega. Sin métricas persistentes, alertas externas, backups o revisión completa de esquema/permisos. Acceso local requiere configuración administrativa; terminal tiene custodia propia. [Detalles](docs/OPERATIONAL_MONITORING.md). |

## Enlaces y mensajes

Herramienta Bearer con correo verificado, consentimiento y entrada acotada.
Reglas sin backtracking, sin abrir URL/DNS o consultar servicios externos. El
resultado no contiene credenciales, ruta, parámetros o texto del mensaje y no
certifica seguridad/fraude. HTML se trata como texto y nunca se ejecuta.

Cuota diaria del backend, persistente por dueño y compartida entre sesiones, con
bloqueo/versiones y rollback; no proviene del cuerpo. El contador participa del
borrado de cuenta y exportación mínima; no se almacenan reportes o entradas.
API y web usan cabeceras privadas y logs sin contenido. Concurrencia/fallos se
ensayan en PostgreSQL y una prueba usa un listener local para comprobar que el
destino no recibe conexiones. [Contrato y alcance](docs/LINK_MESSAGE_CHECKS.md).

## Explicación opcional de IA

La solicitud exige Bearer de cuenta, sesión activa, correo verificado, pertenencia
del dispositivo/análisis y confirmación explícita. El GET de disponibilidad no
consulta al proveedor. No hay entrada de conversación, prompt o destino libre.
El contexto se construye en el servidor desde puntos/cobertura, estados y catálogo
de diez reglas; excluye evidencia libre, identificadores personales/de informes,
fechas, feedback, archivos y secretos. La API nunca entrega la clave al navegador.

Respuesta JSON con campos y tamaños acotados, diez reglas conocidas sin duplicados
y texto escapado en la web. Las referencias y acciones manuales proceden de las
guías locales; no se aceptan enlaces o comandos del modelo. Un esquema válido no
garantiza veracidad: la interfaz mantiene la advertencia y los desconocidos.
No escribe resultados, cambia el puntaje o ejecuta cambios en Windows. La guía
local sigue disponible ante desactivación, fallo, cuota o salida incompatible.

Modelo y gateway se fijan en la configuración del servidor. El bloque inicial
rechaza el identificador ambiguo `openrouter/free` y acepta el nombre calificado
`openrouter/openrouter/free` o modelos con prefijo `openrouter/` y sufijo `:free`.
La corrección del 2026-10-26 rechaza el prefijo preliminar `or/`, modelos pagos y
rutas `auto`. Se requieren también permisos de modelo/conexión en OmniRoute,
sin fallback hacia otras conexiones. Credencial de producto separada de desarrollo;
configuración y clave DPAPI CurrentUser fuera del repositorio. No se registra
prompt, respuesta o secreto en los logs de aplicación. `noLog` en el gateway no
determina la conservación del proveedor: revisar sus condiciones antes de habilitar.

Cinco intentos por propietario/día UTC por defecto y concurrencia 4 global/1 por
propietario, en memoria. Un intento fallido puede consumir cuota; reiniciar la API
restablece estos límites. No son controles coordinados de producción para varias
instancias. [Contrato y configuración](docs/AI_EXPLANATIONS.md).

## Validación local

Las pruebas cubren autenticación y aislamiento de datos; el bloque de cuentas
añade casos de tokens, correo, renovación y revocación. Los resultados concretos
y su alcance están en [CHANGELOG.md](CHANGELOG.md). Las pruebas en memoria y
los E2E locales con PostgreSQL no constituyen una auditoría ni una validación
de despliegue de producción.

## Desarrollo local

- Configura una contraseña de PostgreSQL fuerte y una clave JWT aleatoria en
  `.env`, siguiendo [README.md](README.md). No pegues esos valores en issues,
  capturas ni registros compartidos.
- `appsettings.json` y Compose contienen valores de ejemplo o respaldo conocidos.
  No sirven como secretos para un despliegue. El arranque directo de la API no
  ejecuta la carga de `.env` que realiza el helper.
- Conserva PostgreSQL y la API en el entorno local mientras se resuelven los
  controles de publicación. Usa datos ficticios para pruebas y revisa los
  registros antes de compartirlos.
- Cambiar la contraseña en `.env` después de crear el volumen no modifica la
  contraseña del usuario ya existente en PostgreSQL. Planifica ese cambio en la
  base antes de actualizar la configuración de conexión.
- No compartas el volumen ni sus exportaciones: contienen cuentas y hashes.
  Evita borrar el volumen para solucionar una conexión; esa operación elimina
  los datos almacenados.
- La web usa `API_BASE_URL` para localizar la API. Mantén la clave de firma JWT
  y las credenciales de PostgreSQL fuera de su configuración. Evita compartir
  cookies o respuestas de autenticación: el JWT contiene datos de cuenta y
  permite el acceso mientras sea válido.
- Mailpit solo debe exponerse en loopback. Sus mensajes contienen tokens de
  recuperación o verificación; no son logs inocuos. Usa cuentas ficticias y no
  publiques su bandeja ni los enlaces. Consulta su tratamiento y retención en
  [PRIVACY_NOTES.md](PRIVACY_NOTES.md).
- Si la API no responde al logout, la web borra sus cookies e informa del cierre
  local. La revocación remota queda sin confirmar; una copia de los tokens puede
  seguir vigente. El cierre global requiere que la API complete la solicitud.
- Reset de contraseña revoca también códigos y credenciales de dispositivo;
  conserva su registro y el límite de cuenta. Logout-all solo actúa sobre sesiones
  web. Para invalidar al cliente sin cambiar la contraseña, revoca el dispositivo.
- Las pruebas E2E usan cuentas únicas y deshabilitan trazas, vídeos y capturas
  automáticas. Las capturas de revisión visual solo se habilitan mediante
  `SC_AI_VISUAL_REVIEW=1`, con cuentas ficticias y sin tokens. Esa revisión es
  opcional. Conserva esas precauciones si agregas pruebas de autenticación.

## Trabajo pendiente antes de publicar

| Área | Trabajo pendiente |
| --- | --- |
| Cuenta | MFA opcional, política de contraseñas comprometidas, alertas de acceso y revisión de enumeración y tiempos de respuesta. El cambio autenticado, verificación y recuperación básicos no completan la respuesta a una cuenta comprometida. |
| Sesiones | Monitoreo de reutilización, política operativa de limpieza y revisión integral de varias instancias/proxies. El límite por cuenta ya tiene prueba concurrente en PostgreSQL. Reautenticación para futuras acciones especialmente sensibles. |
| Correo | Cola cifrada y recuperación tras reinicio incorporadas. Pendientes selección/operación de SMTP, dominio/remitente, custodia de claves para el alojamiento, capacidad, alertas y conservación. Mailpit sigue local. |
| API y despliegue | Validación y autorización por cada recurso nuevo, límites de solicitudes, configuración TLS comprobada, revisión del origen CORS, hosts y errores según el entorno. |
| Secretos | Retirar la dependencia de valores de demostración en despliegues, definir custodia y rotación y revisar el historial si hubo exposición. Cambiar la clave JWT invalida los tokens firmados con la anterior y requiere coordinación. |
| Datos | Aplicar mínimo privilegio a la cuenta de base de datos, definir cifrado del almacenamiento, conservación de contenido y operación de copias externas/recuperación real. Existe backup manual cifrado con ensayo aislado y mantenimiento técnico opcional de autenticación. Véase [PRIVACY_NOTES.md](PRIVACY_NOTES.md). |
| Registros y operación | Ampliar la actividad privada implementada a auditoría operativa, intentos fallidos y otras acciones; definir protección frente a SQL/administradores, acceso/conservación de logs, monitoreo, alertas y respuesta. Los logs mínimos implementados no sustituyen esos procesos. |
| Dependencias y validación | Mantener revisiones de dependencias, migraciones y permisos; ampliar pruebas de autenticación, tokens y acceso a datos según los riesgos de cada cambio. |
| Agente Windows | Instalador y binarios firmados, actualización y distribución seguras, funcionamiento con diferentes permisos/ediciones, custodia y retención de informes, observabilidad y respuesta ante credenciales comprometidas. La consola actual es una prueba de concepto manual. |
| Reportes | Acordar y comprobar el canal privado de vulnerabilidades, responsables y proceso de seguimiento antes de recibir usuarios externos. |

## Referencias de diseño consultadas

Se contrastó el diseño con las guías oficiales de OWASP sobre
[recuperación de contraseña](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html),
[gestión de sesiones](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html),
[autenticación](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
y [CSRF](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html).
Sirven como referencias de revisión: uso único, expiración, invalidación, cookies,
protección de origen y límites requieren pruebas del código y de su despliegue.
Esta consulta no constituye certificación ni acredita que todos sus controles
estén implementados.

El bloque de dispositivos también se contrasta con OWASP
[Authorization](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html)
y [REST Security](https://cheatsheetseries.owasp.org/cheatsheets/REST_Security_Cheat_Sheet.html):
el tipo de credencial, el propietario y la transición deben validarse en cada
operación. El protocolo de vinculación propio no se presenta como OAuth.

## Controles para componentes futuros

El bloque de dispositivos incorpora credenciales propias, vinculación y revocación.
La consola Windows protege su copia con DPAPI y ejecuta consultas acotadas; faltan
firma, instalador y actualización segura. Un endpoint heartbeat no acredita la
seguridad del PC. `forget` elimina únicamente la copia local: si se perdió el
control de la credencial, revoca el dispositivo desde la web. Los informes
persisten hasta borrarlos explícitamente. Los permisos y límites de cada regla
están en [docs/WINDOWS_AGENT.md](docs/WINDOWS_AGENT.md).

La explicación opcional actual conserva reglas deterministas para hallazgos y
puntajes, cobertura y estados desconocidos, con el contexto reducido descrito
arriba. La vigilancia y autonomía futuras requieren alcance explícito y
autorizaciones por plan/operación; no están implementadas. No se comprobó
inferencia externa de IA ni hay remediación automática.

La ingesta acepta observaciones de hasta siete días, duración de escaneo de
hasta treinta minutos, tolerancia futura de cinco minutos y fechas UTC. Guarda
captura y recepción por separado. Estos límites no hacen que el informe sea
auténtico o esté actualizado en el momento de leerlo. Un cliente con una
credencial válida puede declarar datos falsos; el resultado debe identificarse
como autodeclarado. El alcance y el protocolo están en
[docs/ANALYSIS_INGESTION.md](docs/ANALYSIS_INGESTION.md).

## Referencias del repositorio

- [Arranque, JWT y manejo de errores](src/SecurityCenterAI.Api/Program.cs).
- [Emisión de tokens](src/SecurityCenterAI.Api/Services/TokenService.cs).
- [Autenticación](src/SecurityCenterAI.Api/Controllers/AuthController.cs) y
  [dashboard](src/SecurityCenterAI.Api/Controllers/DashboardController.cs).
- [Persistencia y relaciones](src/SecurityCenterAI.Infrastructure/Persistence/AppDbContext.cs).
- [Compose local](docker-compose.yml) y [helper de desarrollo](scripts/dev.ps1).
- [Sesión web](src/SecurityCenterAI.Web/src/lib/session.ts),
  [Server Actions](src/SecurityCenterAI.Web/src/app/actions.ts) y
  [cliente de API](src/SecurityCenterAI.Web/src/lib/api.ts).

## Información pública y reporte local de errores

Privacidad y términos son borradores accesibles sin sesión, con datos del
responsable/contacto pendientes. El registro no guarda aceptación contractual
de esos textos. Las páginas de información/ayuda usan `no-referrer` y `no-store`.

El formulario de errores no envía solicitudes con su contenido ni accede a
perfil, cookies, informes o logs. Campos cerrados y acotados, texto escapado,
revisión explícita y vista previa antes de copiar o descargar el JSON. Editar
invalida la confirmación. Sin JavaScript preparar queda deshabilitado; los
controles no tienen `name` para evitar serialización nativa de su contenido.
No hay endpoint o receptor de tickets. La revisión del usuario no garantiza
anonimización: el texto libre y las copias locales requieren custodia propia.
[Datos y límites](docs/PUBLIC_INFORMATION.md).

## Guías locales y valoración privada

Las explicaciones usan plantillas y referencias HTTPS fijas de Microsoft; la
evidencia no selecciona instrucciones o enlaces. React muestra evidencia y
comentarios como texto. Los endpoints exigen sesión Bearer y propiedad de
cuenta/dispositivo/análisis; Device no da acceso. El feedback admite tres
valores, comentario de 1000 caracteres, cuerpo de 8 KiB y rechaza campos extra.
El índice usuario/análisis evita duplicados y los envíos concurrentes se
reintentan. No hay publicación de comentarios ni acceso entre cuentas.
Los límites de retención, administradores y beta se detallan en
[guías y feedback](docs/LOCAL_GUIDES_AND_FEEDBACK.md).

## Paquete portátil de pruebas

El paquete incluye únicamente la publicación del agente y su ayuda/launchers;
no incluye credenciales, informes ni `.env`. Se genera en una carpeta temporal
única y valida los archivos admitidos antes de crear el ZIP y sus hashes. Las
entregas existentes no se sobrescriben. El manifiesto no se firma: un checksum
acompañante no autentica por sí solo el origen de un binario.

La ejecución básica usa `.exe`/`.cmd`; el wrapper PowerShell es opcional. No se
cambian políticas persistentes, no se solicita elevación ni se agregan servicios
o tareas. `doctor` solo consulta requisitos básicos y no accede al vault, red o
reportes. Una base local de datos vacía/relativa se rechaza antes de crear estado.
La compatibilidad en otras PC y la distribución firmada siguen pendientes;
no se deben desactivar Defender o SmartScreen para sortear bloqueos.
[Operación y límites](docs/AGENT_PRIVATE_BETA.md).

## Comparación de informes

La consulta de comparación requiere Bearer de cuenta y verifica propietario,
dispositivo y ambos análisis. Las selecciones ajenas reciben 404; Device no
autoriza lectura. No modifica ni persiste resultados. Reutiliza los límites de
cuenta existentes y mantiene evidencia escapada y versiones explícitas; reglas
ambiguas o incompatibles no se convierten en cambios comprobados.

## Exportación y eliminación de un análisis

Ambas operaciones exigen Bearer de cuenta y propiedad conjunta de usuario,
dispositivo y análisis; Device no las autoriza. Un identificador ajeno o borrado
recibe `404`. La exportación usa DTO explícitos y adjunto JSON `no-store` con
`nosniff`: no agrega correo de cuenta, credenciales o hashes internos. La
evidencia y el comentario podrían contener información escrita por el cliente
o usuario y requieren revisión antes de compartir el archivo.

DELETE requiere confirmación verdadera, rechaza campos extra y admite hasta
1 KiB. El borrado transaccional elimina análisis, resultados y todo el feedback
asociado. Cuando hay `reportId`, conserva exclusivamente dispositivo/reporte/fecha
como marca de eliminación; las versiones de concurrencia de usuario y dispositivo
evitan que un reintento simultáneo restaure el mismo identificador. Con una
credencial válida, su reenvío recibe `410`. La marca no contiene hash, puntaje,
evidencia o comentario y no se purga automáticamente mientras exista el dispositivo.

El agente `0.4.1-preview.1` interpreta `410` y pide un nuevo `scan`, conservando
credencial e informe local. Borrar un análisis no cambia el PC, revoca al equipo
o elimina copias, descargas o backups; tampoco es eliminación completa de cuenta.
La autorización no controla el uso de una copia ya descargada.
[Alcance y contrato](docs/ANALYSIS_HISTORY.md).

## Exportación de los datos de cuenta

`GET /api/account/export` obtiene el propietario exclusivamente del Bearer con
sesión activa. No admite seleccionar otra cuenta mediante la ruta; `Device` no
autoriza la descarga. Una lista explícita de campos incluye perfil y contenido
propios y excluye hashes, secretos y registros internos de autenticación. El
correo y nombre de perfil se incluyen deliberadamente en esta exportación.

PostgreSQL usa una instantánea `RepeatableRead` para consultar sin tracking
perfil, equipos, informes, feedback y marcas. La operación no escribe registros.
El límite `AccountExport` predeterminado es tres solicitudes por minuto/IP,
además del límite global. La respuesta es un adjunto JSON `no-store` y `nosniff`;
la intermediación web conserva el Bearer en el servidor y agrega `no-referrer`.

La copia descargada contiene datos de perfil, estados de seguridad y texto libre.
Su uso y custodia quedan bajo control del usuario. El servidor prepara el JSON en
memoria y no persiste un archivo; historiales muy grandes necesitan una revisión
de capacidad antes de habilitar una operación externa.
[Contenido, uso y límites](docs/ACCOUNT_EXPORT.md).

## Eliminación de cuenta

`DELETE /api/account` exige Bearer de sesión activa, contraseña actual y
confirmación exacta `ELIMINAR`. No recibe un UUID de propietario. Rechaza campos
adicionales y cuerpos de más de 2 KiB. Los fallos de contraseña utilizan el
contador y bloqueo compartidos con login; `AccountDeletion` limita además a tres
solicitudes/minuto/IP por defecto.

En PostgreSQL se bloquea la fila del propietario dentro de una transacción y
se revalidan la sesión y contraseña. Las cascadas eliminan perfil, sesiones,
tokens, dispositivos/credenciales, informes, feedback y marcas mínimas. Los
reintentos ante conflictos son acotados. Las cuentas ajenas y reglas se conservan.

La Server Action no devuelve contraseñas en errores y limpia cookies tras un
`204`. Los archivos locales, correos/colas y backups tienen custodia independiente.
La restauración de un backup anterior necesita tratar los borrados posteriores;
no hay un registro externo que los reaplique automáticamente.
[Contrato y alcance](docs/ACCOUNT_DELETION.md).
