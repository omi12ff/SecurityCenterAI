# Notas de privacidad e inventario de datos

Revisión documental: **21 de octubre de 2026**.

Este inventario técnico describe la web, el backend y la consola Windows. No es una política pública
de privacidad ni acredita cumplimiento legal. Las propuestas de la guía se
distinguen de los datos y mecanismos que contiene el repositorio.

## Datos de PostgreSQL

El modelo de EF Core contiene diez tablas de producto. La tabla siguiente usa
los nombres físicos de PostgreSQL: las once columnas de `users` están en español
y sus propiedades C# mantienen los nombres originales. Las equivalencias están
en [README.md](README.md#columnas-de-users). Las tablas nuevas de dispositivos,
reglas y hallazgos también usan columnas en español; las tablas anteriores
conservan sus nombres. La ingesta amplía análisis y resultados con metadatos,
estados y conteos, sin añadir una tabla de telemetría.
Las navegaciones no agregan columnas.

| Tabla | Campos persistidos | Finalidad |
| --- | --- | --- |
| `users` | `id_usuario`, `nombre_completo`, `correo_electronico`, `hash_contrasena`, `fecha_creacion_utc` | UUID de cuenta, nombre recortado, correo normalizado, hash de contraseña y creación UTC. Nombre hasta 100 caracteres y correo hasta 320; correo único. |
| `users` | `correo_verificado` | Estado de confirmación de la dirección de correo. No es una prueba de identidad personal. |
| `users` | `intentos_acceso_fallidos`, `fecha_fin_bloqueo_utc` | Contador de fallos y fin del bloqueo temporal de login. |
| `users` | `fecha_ultima_solicitud_recuperacion_utc`, `fecha_ultimo_envio_verificacion_utc` | Fechas para limitar envíos de recuperación y verificación. |
| `users` | `version_concurrencia` | UUID técnico para detectar actualizaciones concurrentes. |
| `user_sessions` | `Id`, `UserId`, `CreatedAtUtc`, `ExpiresAtUtc`, `RevokedAtUtc`, `ConcurrencyStamp` | Identificación de sesión y cuenta, vigencia, revocación y control de concurrencia. |
| `refresh_credentials` | `Id`, `UserSessionId`, `TokenHash`, `CreatedAtUtc`, `ExpiresAtUtc`, `ConsumedAtUtc`, `ConcurrencyStamp` | Hash SHA-256 del refresh, sesión asociada, fechas y control de consumo y concurrencia. El hash es único. |
| `account_action_tokens` | `Id`, `UserId`, `Purpose`, `TokenHash`, `CreatedAtUtc`, `ExpiresAtUtc`, `ConsumedAtUtc`, `ConcurrencyStamp` | Hash SHA-256 del token de correo, propósito, cuenta, vigencia y consumo. El hash es único y el propósito tiene un máximo de 32 caracteres. |
| `security_analyses` | `Id`, `UserId`, `Score`, `AnalyzedAtUtc`, `id_dispositivo` | Identificador, cuenta, puntos confirmados enteros opcionales, fecha de recepción de la ingesta y relación opcional a dispositivo. Los registros previos conservan sus valores y semántica histórica; no se les atribuye un dispositivo artificialmente. |
| `security_analyses` | `id_informe_agente`, `hash_informe`, `version_esquema`, `origen`, `version_agente` | UUID de reporte y hash SHA-256 canónico para idempotencia por dispositivo; esquema 2, origen `windows-agent` y versión declarada hasta 64 caracteres. No son atestación ni firma. Vacíos en legacy. |
| `security_analyses` | `fecha_inicio_utc`, `fecha_fin_utc`, `version_puntaje`, `porcentaje_cobertura`, `cantidad_correctas`, `cantidad_revisar`, `cantidad_desconocidas`, `cantidad_no_aplicables` | Captura declarada por cliente, cálculo versión 1, cobertura y cuatro conteos. Se conservan junto al puntaje; no se inventan para análisis previos. |
| `devices` | `id_dispositivo`, `id_usuario`, `nombre` | Identificadores y nombre asignado por el propietario, de 2 a 100 caracteres. Un dispositivo no revocado por cuenta. |
| `devices` | `fecha_creacion_utc`, `fecha_vinculacion_utc`, `fecha_ultima_conexion_utc`, `fecha_revocacion_utc`, `version_concurrencia` | Fechas de gestión, último heartbeat y UUID técnico de concurrencia. Contacto no significa equipo seguro u online. |
| `devices` | `plataforma`, `version_agente` | La ingesta registra plataforma `Windows` por el tipo de protocolo recibido; no se verifica remotamente. Versión de cliente declarada al vincular, enviar heartbeat o análisis, hasta 64 caracteres. No prueba la versión real del software. |
| `device_pairing_codes` | `id_codigo_vinculacion`, `id_dispositivo`, `hash_codigo`, `fecha_creacion_utc`, `fecha_vencimiento_utc`, `fecha_consumo_utc`, `fecha_revocacion_utc`, `version_concurrencia` | Código de un uso representado por hash SHA-256, relación al dispositivo, vigencia y control de consumo/revocación. |
| `device_credentials` | `id_credencial_dispositivo`, `id_dispositivo`, `hash_credencial`, `fecha_creacion_utc`, `fecha_vencimiento_utc`, `fecha_revocacion_utc`, `version_concurrencia` | Credencial opaca representada por SHA-256; fechas y control de revocación. No almacena el secreto en texto claro. |
| `security_rules` | `id_regla`, `version_regla`, `titulo`, `descripcion`, `fecha_creacion_utc` | Catálogo del servidor con clave compuesta de id (hasta 64 caracteres) y versión positiva; título hasta 200 y descripción hasta 4000. Diez reglas admitidas, sin comandos remotos. |
| `security_rules` | `severidad`, `recomendacion`, `peso_puntaje` | Severidad para resultados que requieren revisión, recomendación y peso del catálogo del servidor. Metadatos versionados, sin observaciones personales. |
| `security_findings` | `id_hallazgo`, `id_analisis`, `id_regla`, `version_regla`, `titulo`, `severidad`, `evidencia`, `recomendacion`, `fecha_deteccion_utc` | Resultado ligado a análisis y versión de regla. Título hasta 200; evidencia y recomendación hasta 4000 cada una. Evidencia del cliente y metadatos/recomendaciones del catálogo guardados como instantáneas. |
| `security_findings` | `estado` | `passed`, `attention`, `unknown` o `notApplicable` para resultados de ingesta. Vacío en hallazgos previos sin estado. No se deduce un estado favorable a partir de la severidad. |

`User.Analyses`, `SecurityAnalysis.User`, `UserSession.User`,
`RefreshCredential.Session` y `AccountActionToken.User` son navegaciones.
También lo son las relaciones de dispositivo con usuario, códigos y credenciales;
y las de análisis, hallazgo y regla. `SecurityAnalysis.Findings` es una colección,
no una columna adicional.
Las relaciones se almacenan en las claves foráneas indicadas. La tabla técnica
`__EFMigrationsHistory` registra las migraciones aplicadas, no un historial
de actividad de usuarios.

El renombrado de `users` no añade categorías de datos, no altera el contenido
de las cuentas ni cambia el JSON de la API, los tokens o su retención.

No se guardan IP ni User-Agent en estas entidades. La versión del cliente se
recibe explícitamente como `agentVersion`, sin leerla de User-Agent. Los limitadores consultan
la IP de la conexión en memoria. Tampoco se almacenan contraseñas de cuenta,
refresh tokens o tokens de correo en texto claro en PostgreSQL; sus portadores
sí existen temporalmente en solicitudes, cookies o mensajes según el flujo.

## Solicitudes, respuestas y perfil

| Flujo | Datos que utiliza | Resultado |
| --- | --- | --- |
| Registro | Nombre, correo y contraseña. La confirmación de contraseña solo se valida en la web. | Guarda el perfil y hash, crea sesión y devuelve access token, refresh token, vencimientos y perfil con `emailVerified`. Solicita correo de verificación. |
| Login | Correo y contraseña. | Verifica hash y bloqueo, crea sesión y devuelve tokens y perfil. Los errores son genéricos. |
| Perfil | Bearer con sesión activa. | `/api/auth/me` consulta los datos actuales del usuario; el claim de correo verificado puede haber quedado antiguo. |
| Dashboard | Bearer con sesión activa y UUID de usuario. | Puntaje más reciente, total y hasta cinco análisis propios con identificador, puntaje y fecha. No genera un análisis. |
| Renovación y logout | Refresh opaco; el cierre global utiliza Bearer. | Rota la credencial o revoca sesiones. No elimina cuenta ni análisis. |
| Recuperación | Correo para solicitar enlace; token y nueva contraseña para restablecer. | La solicitud responde de forma genérica. El reset consume el token y revoca las sesiones; exige volver al login. |
| Verificación | Sesión para reenviar correo; token del enlace para confirmar. | Confirma la dirección y consume el token, sin acreditar identidad civil. |
| Salud | Sin cuenta requerida. | `/health` devuelve `healthy` fijo; no verifica base o correo. |
| Dispositivos del propietario | Sesión de usuario, nombre e identificador del dispositivo. | Lista y consulta propios, crea, emite código o revoca. Crear y emitir códigos exige correo verificado en la base. |
| Vinculación del cliente | Código de un uso y `agentVersion`. | Consume el código y devuelve una credencial opaca de dispositivo, solo al canjearlo. La respuesta contiene el secreto; no debe registrarse o compartirse. |
| Heartbeat | Credencial `Device` en Authorization y `agentVersion` en el cuerpo. | Actualiza fecha de contacto y versión declarada. No recoge hardware, archivos, estado de Windows ni resultados de verificaciones. |
| Ingesta de análisis | Credencial Device, esquema 2, UUID estable del reporte, versión del cliente, fechas y diez estados/evidencias con regla/version. | Crea análisis y diez resultados con metadatos del catálogo, puntos confirmados y cobertura. Un reintento idéntico devuelve lo guardado; no duplica el historial. Cuenta y dispositivo proceden de la credencial. |
| Detalle de dispositivo | Bearer del propietario y UUID del equipo. | Devuelve hasta 20 análisis propios recientes y 100 hallazgos por análisis; la lista general devuelve hasta 100 dispositivos. Son consultas acotadas, sin paginación ni generación de resultados. |

Next.js procesa formularios mediante Server Actions y consulta la API desde
el servidor. Sus estados de error no devuelven contraseñas. No hay endpoint
de edición de nombre o correo ni eliminación de cuenta.

## Cookies y tokens

El JWT firmado con HMAC SHA-256 contiene identificador de usuario `sub`,
`email`, `name`, identificador de token `jti`, sesión `sid` y estado
`email_verified`, además de emisor, audiencia y vencimiento. Su contenido
se puede leer: la firma no cifra esos datos.

El access token dura 15 minutos y la API admite 30 segundos de tolerancia de
reloj; además consulta que la sesión siga activa en la base. La vigencia de la
sesión y del refresh está limitada a 30 días. El refresh es aleatorio, opaco y
rotatorio; los hashes consumidos se conservan para detectar reutilización,
que revoca la sesión asociada.

Next.js guarda access y refresh en `security_center_session` y
`security_center_refresh`. Ambas cookies tienen `HttpOnly`,
`SameSite=Lax`, `Path=/` y `Secure` en producción. No se usa
`localStorage`. La cookie de access vence con el JWT y la de refresh con
su credencial; logout elimina ambas y solicita revocación al backend.

Los tokens de correo son de un uso: verificación, 24 horas; recuperación,
30 minutos. Se envían como enlaces `?token=...`. Las páginas requieren
confirmación POST, usan `Referrer-Policy: no-referrer` y `no-store`.
Los tokens pueden quedar en el historial o herramientas del navegador y en
los mensajes: no compartir enlaces ni capturas. La expiración o consumo no
borra automáticamente esos mensajes ni los registros de base.

Los códigos de vinculación y credenciales de dispositivo usan 32 bytes
aleatorios y hash SHA-256. El código se muestra al propietario al emitirlo,
dura diez minutos y se envía en el cuerpo del canje. El token de dispositivo
se devuelve al canjearlo y dura 90 días; se presenta en
`Authorization: Device <credencial>`, separado del JWT del usuario. No se
añade a las cookies de sesión del navegador ni se almacena en PostgreSQL en
texto claro. La consola Windows guarda su copia y la URL de destino protegidas
con DPAPI CurrentUser en LocalAppData; el detalle local está debajo.

Regenerar invalida códigos anteriores, pero la credencial actual permanece
vigente hasta el canje nuevo. Revocar el dispositivo invalida ambos tipos de
secreto. Restablecer la contraseña también los revoca, sin eliminar dispositivo
o historial; logout-all solo cierra sesiones web. La revocación no borra sus
filas y un dispositivo pendiente sigue ocupando el límite de cuenta.

## Datos locales del agente Windows

El agente de consola funciona de forma manual. `pair` recibe un código en una
entrada interactiva oculta y envía código y versión a la API. La contraseña,
el JWT y el refresh de la cuenta no se solicitan. El código no se conserva en
el archivo de credencial. `heartbeat` solo envía la versión declarada y usa la
credencial para actualizar el último contacto; no sube el informe del sistema.
`scan` mantiene el informe local salvo `-Send`; `send` sube el último informe
compatible al destino guardado con la credencial. Son acciones manuales y
explícitas, sin recopilación o envío en segundo plano.

| Ubicación o flujo | Datos | Conservación y límites |
| --- | --- | --- |
| `%LOCALAPPDATA%\SecurityCenterAI\Agent\credential.enc` | Credencial de dispositivo, UUID, vencimiento y URL de la API dentro del contenido protegido por DPAPI CurrentUser. | Fuera del repositorio y su carpeta OneDrive. Persiste entre ejecuciones hasta reemplazarla o ejecutar `forget`; DPAPI no impide que procesos del mismo usuario la descifren. |
| `scan` en memoria | Estado seleccionado de Defender, tres perfiles de firewall, Secure Boot, TPM, cifrado del volumen del sistema, servicio Windows Update, UAC y SMB1 del componente servidor. | Consultas de solo lectura. Se excluyen salida bruta de cmdlets, nombres de usuario/equipo, identificadores de hardware, historial, archivos y claves de recuperación. |
| `%LOCALAPPDATA%\SecurityCenterAI\Agent\reports\scan-….json` | Esquema 2, UUID estable, versión del agente/reglas, fechas UTC, estados, severidad, evidencia seleccionada y recomendaciones de las diez comprobaciones. | JSON local en texto claro generado al ejecutar `scan`; solo se envía con `scan -Send` o `send`. No hay purga automática; el usuario controla su borrado y cualquier copia que comparta. |
| `status` | Metadatos locales de la credencial, sin mostrar su secreto. | No consulta la API ni acredita que la credencial siga autorizada remotamente. |

`forget` borra la credencial local, pero no revoca su posible copia en otro lugar,
no elimina el registro remoto del dispositivo y no borra reportes. La revocación
remota se realiza en `/devices`. Los informes contienen estados de seguridad:
revísalos antes de compartirlos. El informe está minimizado, pero no es anónimo
por el solo hecho de no incluir nombre o correo. La ruta de Windows puede
contener un nombre de perfil y conviene ocultarla en capturas.

La consola no habilita tareas programadas, arranque automático, telemetría ni
escucha comandos del servidor. `scan` puede funcionar sin vincular una cuenta.
Los permisos de consulta y la disponibilidad de módulos cambian entre equipos;
los datos desconocidos no se rellenan con valores favorables.
La [guía del agente](docs/WINDOWS_AGENT.md) enumera campos y criterios por regla.

El envío selecciona identificador/versión de regla, estado, evidencia y fecha;
no necesita subir los textos de ayuda del JSON local. El servidor produce los
títulos, severidad, recomendaciones y pesos con su catálogo. Evidencia y
estados siguen siendo declaraciones del cliente, sin atestación. Se reciben
hasta 64 KiB y se conservan fecha de captura y de recepción separadas. Se
aceptan reportes de hasta siete días; esa ventana no determina el plazo de
retención, que sigue sin definirse. El hash canónico sirve para comparar
reintentos y no anonimiza ni cifra la evidencia. Los informes del esquema 1
no se migran o envían automáticamente: requieren un `scan` nuevo.

## SMTP y Mailpit local

Mailpit recibe los correos de desarrollo en loopback: SMTP `1025` y bandeja
web `8025`. Los mensajes incluyen destinatario, remitente, asunto, texto y
enlace con el token portador. No son simplemente diagnósticos anónimos; quien
acceda a la bandeja puede usar un enlace vigente.

Antes de SMTP, la API conserva temporalmente destinatario, token y propósito
en una cola en memoria de hasta 100 mensajes. No es una tabla ni una cola
durable: reiniciar el proceso o fallar SMTP puede perder la entrega; los hashes
guardados y el tiempo de espera para reenviar permanecen en la base.

La aplicación usa `Email:Host`, `Port`, `From`, `Username`,
`Password` y `EnableSsl`. En Development/Testing se exige host loopback;
fuera de esos entornos, SMTP autenticado con TLS y frontend HTTPS. Todavía no
hay proveedor de producción elegido: al incorporarlo deberá documentarse el
tratamiento de destinatarios, mensajes, metadatos y retención del proveedor.

Compose fija Mailpit `v1.31.5`, sin volumen persistente, y configura un máximo
de 200 mensajes. No hay integración que borre el correo al consumir su token ni
un plazo de retención por cuenta. La captura local no es un backup de mensajes.
Usar cuentas ficticias, mantener los puertos locales y revisar su almacenamiento
antes de utilizar datos reales.

## Almacenamiento, registros y herramientas

| Elemento | Estado actual | Límite |
| --- | --- | --- |
| PostgreSQL | Volumen `postgres_data`, puerto `127.0.0.1:5432`; detener el contenedor conserva los datos. | El proyecto no configura cifrado del volumen ni backups automáticos. |
| Secretos locales | `.env` ignorado por Git; helper carga cuatro variables en el proceso y restaura el entorno anterior al salir. | Archivo de texto claro, sin cifrado propio. No compartirlo ni registrar sus valores. |
| Web | `API_BASE_URL` solo en servidor y tokens en las cookies descritas. | No necesita clave JWT ni contraseña de PostgreSQL. |
| Logs | Niveles generales `Information` y `Microsoft.AspNetCore: Warning`; errores con `ProblemDetails`. Next.js omite del log de entrada de desarrollo las rutas con tokens de correo. | No hay política completa de redacción, acceso, retención o auditoría. Un proxy o hosting puede registrar IP, URL y otros metadatos. |
| Límites | IP en memoria, contador de login y fechas de envío en la cuenta. | La IP de Next.js es compartida en las llamadas intermediadas. No existe inventario de direcciones IP persistido por la aplicación. |
| Pruebas | Cuentas ficticias únicas; la suite E2E elimina sus registros por correo exacto y borrado en cascada. | Interrumpir una suite puede dejar cuentas o mensajes de prueba. Las trazas, vídeos y capturas automáticas están deshabilitados. |
| Herramientas del entorno | Editor, extensiones, Docker Desktop y SDK tienen diagnósticos y telemetría propios. | Sus configuraciones no equivalen a una política del producto y no están controladas por estas entidades. |

No hay integración de analítica de uso de producto, proveedor de IA, pagos ni
almacenamiento de archivos personales. Esto no describe servicios externos
que el desarrollador use fuera de la aplicación.

## Retención, borrado y backups

No hay tarea de purga ni plazo de retención implementados para cuentas,
dispositivos, análisis, hallazgos, sesiones o tokens. Vencer, consumir o revocar marca un estado;
no elimina el registro. La conservación de hashes de refresh permite detectar
reutilización, pero todavía requiere una política de limpieza acotada.

No hay endpoints de exportación o borrado individual. Las relaciones configuran
cascada al eliminar físicamente una cuenta; esto no es un mecanismo disponible
para el usuario. Logout, revocación y reset no eliminan el historial.

No hay política de copias ni restauración automatizada. Una exportación manual
puede conservar datos después de borrarlos de la base y requiere custodia y
borrado propios. Eliminar el volumen destruye la base local completa y no
equivale a eliminar una cuenta específica. Antes de datos reales deben acordarse
plazos y responsables para base, mensajes, logs y copias.

## Estado actual y propuestas futuras

| Tema | Actual | Propuesta pendiente |
| --- | --- | --- |
| Consentimiento | No hay registro de consentimiento ni preferencias de recopilación. | Informar por módulo qué se recoge, qué sale del equipo, finalidad y borrado. |
| Equipo Windows | Consola manual con diez comprobaciones y evidencia seleccionada. El envío explícito incorpora UUID de reporte, versiones, fechas y estados/evidencias; no hay inventario de hardware, drivers o aplicaciones. | Distribución, operación y revisión de compatibilidad del agente. |
| Archivos y navegación | Sin cargas de capturas, mensajes, archivos ni historial de navegación. | Selección explícita, minimización, límites y retención antes de habilitarlas. |
| IA | Sin proveedor ni prompts/conversaciones enviados o almacenados. | Elegir proveedor, contexto reducido, autorización y retención antes de integrar. |
| Control de la cuenta | Perfil, correo, recuperación, revocación de sesiones y dispositivos. | Borrado y exportación de datos y preferencias de recopilación. |
| Hallazgos | Ingesta de diez resultados, con estados explícitos, puntos confirmados y cobertura, consultables por propietario. El resultado se identifica como limitado y autodeclarado. | Retención, borrado/exportación y revisión de nuevas categorías antes de ampliar las comprobaciones. |
| Métricas | Sin seguimiento de activación, retorno o resolución. | Definir finalidad, campos mínimos y retención antes de instrumentar. |

La guía limita la futura recopilación de contraseñas del equipo o archivos
personales. La contraseña de la cuenta de Security Center AI sí se recibe
actualmente para registro, login y recuperación, y se persiste como hash.

## Fuentes del repositorio

[Entidades](src/SecurityCenterAI.Domain/Entities),
[modelo EF](src/SecurityCenterAI.Infrastructure/Persistence/AppDbContext.cs),
[contratos](src/SecurityCenterAI.Api/Contracts/AuthContracts.cs),
[SMTP](src/SecurityCenterAI.Api/Services/AccountEmailSender.cs),
[sesión web](src/SecurityCenterAI.Web/src/lib/session.ts),
[Compose](docker-compose.yml) y [controles de seguridad](SECURITY.md).
