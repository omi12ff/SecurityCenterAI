# Seguridad de Security Center AI

Revisión documental: 21 de octubre de 2026.

Este documento describe el backend, la web de desarrollo existente y los controles
pendientes. La guía maestra define objetivos del producto; sus requisitos no
equivalen a funciones implementadas. Hay una primera web de autenticación y
dashboard y una prueba de concepto del agente Windows de consola, con ingesta
manual y puntos confirmados limitados a diez reglas. Un proveedor de IA y los
controles de publicación siguen pendientes.

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
| Contraseñas | Registro y login usan `PasswordHasher<User>` de ASP.NET Core Identity. La base guarda `PasswordHash`; la respuesta no lo incluye. Recuperación con token de un uso y revocación de las sesiones al restablecerla. | No hay MFA ni cambio de contraseña autenticado independiente de la recuperación. La política de longitud y contraseñas comprometidas requiere revisión antes de publicar. |
| Entrada de autenticación | Se validan campos obligatorios, formato de correo y nombre de 2 a 100 caracteres. El correo se recorta, se convierte a minúsculas y tiene un índice único. | El registro informa si un correo ya existe. Falta revisar abuso y enumeración de cuentas antes de exponerlo públicamente. |
| JWT y revocación | HMAC SHA-256; validación de firma, emisor, audiencia y expiración, más consulta de la sesión `sid` en la base en cada solicitud autenticada. | Access token de 15 minutos y tolerancia de reloj de 30 segundos. Exige clave de al menos 32 caracteres; debe ser aleatoria. El token está firmado y es legible, sin cifrado. Revocar la sesión invalida sus JWT. |
| Renovación | Refresh opaco aleatorio de 32 bytes, persistido mediante SHA-256, con rotación y vigencia de 30 días. | La reutilización de un refresh consumido revoca su sesión completa. También puede ocurrir por solicitudes concurrentes: la web debe coordinar renovaciones y pedir login si la sesión se revoca. No hay purga automática de estos registros. |
| Sesión del navegador | Server Actions y cookies de access y refresh con `HttpOnly`, `SameSite=Lax`, `Path=/` y `Secure` en producción. No se usa `localStorage`. Logout y cierre global se tramitan en la API. | No hay listado ni revocación individual de otras sesiones en la web. El parser solo extrae metadatos; .NET valida firma y estado de sesión. Conservar las protecciones de origen de las Server Actions y revisar proxies y TLS antes de publicar. |
| Correo | Tokens aleatorios, hashes SHA-256, propósito separado, vencimiento y uso único. Verificación: 24 horas. Recuperación: 30 minutos. Enlaces desde la URL configurada del frontend; envío por cola en memoria. | Cola volátil de 100 mensajes, sin reintento durable ni garantía de entrega. Mailpit local; fuera de Development/Testing se requiere SMTP autenticado y TLS. No hay proveedor de producción elegido. El buzón contiene enlaces sensibles. |
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
| HTTP y errores | Hay redirección HTTPS fuera de `Development`, `ProblemDetails` y manejador de excepciones. Swagger se habilita en `Development`. CORS admite el origen configurado en `FrontendUrl`. | El helper utiliza el perfil HTTP local. Esto no acredita TLS de producción ni una política completa de registros. CORS no sustituye la autorización. |

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
| Cuenta | MFA opcional, cambio autenticado de contraseña, política de contraseñas comprometidas, alertas de acceso y revisión de enumeración y tiempos de respuesta. Verificación y recuperación básicas no completan la respuesta a una cuenta comprometida. |
| Sesiones | Monitoreo de reutilización, política de limpieza y retención, límites de sesiones y validación de concurrencia y comportamiento en varias instancias. Reautenticación para futuras acciones especialmente sensibles. |
| Correo | Selección y operación de SMTP, cola durable o recuperación ante fallos, notificaciones de cambio de contraseña, dominio/remitente, control de volumen y conservación de mensajes. Mailpit es una herramienta local de pruebas. |
| API y despliegue | Validación y autorización por cada recurso nuevo, límites de solicitudes, configuración TLS comprobada, revisión del origen CORS, hosts y errores según el entorno. |
| Secretos | Retirar la dependencia de valores de demostración en despliegues, definir custodia y rotación y revisar el historial si hubo exposición. Cambiar la clave JWT invalida los tokens firmados con la anterior y requiere coordinación. |
| Datos | Aplicar mínimo privilegio a la cuenta de base de datos, definir cifrado, retención, borrado de cuenta y análisis, backups protegidos y restauración comprobada. Véase [PRIVACY_NOTES.md](PRIVACY_NOTES.md). |
| Registros y operación | Definir auditoría de accesos y acciones, filtrado de secretos y datos personales, acceso y retención de logs, monitoreo, alertas y recuperación de incidentes. La configuración actual de niveles de logging no implementa esos procesos. |
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

La futura IA debe recibir únicamente el contexto necesario y autorizado, con
reducción de datos, límites y trazabilidad. La guía propone reglas deterministas
para hallazgos y puntajes, explicaciones basadas en evidencia y ausencia de
comandos arbitrarios. El cálculo actual sigue diez reglas versionadas, muestra
cobertura y conserva los desconocidos; todavía no hay proveedor de IA integrado.

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
