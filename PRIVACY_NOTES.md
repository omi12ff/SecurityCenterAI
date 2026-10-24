# Notas de privacidad e inventario de datos

Revisión documental: **24 de octubre de 2026**.

Las páginas `/privacy` y `/terms` describen este estado como borradores; faltan
responsable, país y contacto para finalizarlas. `/report-problem` prepara un
JSON en el navegador con sección, resumen, pasos, resultado esperado/observado
y fecha UTC. No agrega cuenta, URL, navegador, logs o archivos; no envía ni
persiste el reporte. El usuario revisa el texto libre antes de copiar/descargar,
y controla la custodia de esas copias. No se registra una aceptación contractual
al crear la cuenta. [Estado y controles](docs/PUBLIC_INFORMATION.md).

**Primeros pasos** muestra el progreso derivado del perfil y equipo propios, sin
agregar registros de preparación, analítica o eventos de descarga. El ZIP es
genérico: no contiene el código de vinculación, credencial o informes personales.
El usuario decide cuándo descargarlo y cuándo ejecutar `scan` o `send`; la web
no ejecuta esos comandos. [Datos y límites](docs/GETTING_STARTED.md).

La política de claves nuevas se evalúa localmente: no se envían candidatos a un
servicio de filtraciones ni se guardan en errores/logs. El límite de sesiones por
cuenta reutiliza datos existentes, sin añadir IP, ubicación o navegador. La cola
SMTP conserva destinatario/token en memoria con intentos y antigüedad acotados;
los reintentos no incorporan persistencia o seguimiento de destinatarios. El
diagnóstico NOOP local no envía mensajes. [Política](docs/ACCOUNT_SECURITY_POLICY.md)
y [correo](docs/MAIL_DELIVERY.md).

Este inventario técnico describe la web, el backend y la consola Windows. No es una política pública
de privacidad ni acredita cumplimiento legal. Las propuestas de la guía se
distinguen de los datos y mecanismos que contiene el repositorio.

## Datos de PostgreSQL

El modelo de EF Core contiene quince tablas, incluidas feedback, actividad,
cambio de correo, cuotas y marcas de análisis eliminados. La tabla siguiente usa
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
| `analysis_feedback` | `id_comentario`, `id_usuario`, `id_analisis`, `valoracion`, `comentario`, `version_guia`, `fecha_creacion_utc`, `fecha_actualizacion_utc` | Valoración privada editable, comentario opcional de hasta 1000 caracteres y versión de guía. Una por usuario/análisis, sin historial de ediciones. Se elimina con el análisis. |
| `deleted_analysis_reports` | `id_dispositivo`, `id_informe_agente`, `fecha_eliminacion_utc` | Marca mínima por dispositivo/reporte para rechazar reingesta con `410`. Sin hash, puntos, evidencia o comentario. FK al dispositivo con borrado físico en cascada; sin purga automática mientras exista. |
| `security_activity` | `id_evento`, `id_usuario`, `tipo_evento`, `fecha_evento_utc` | Actividad privada de catorce acciones completadas, sin texto libre, IP, secretos o identificadores de sesiones/dispositivos/informes. Se conserva mientras exista la cuenta, sin purga automática; FK con cascada ante borrado de cuenta. |
| `link_message_usage` | `id_usuario`, `dia_utc`, `consultas_realizadas`, `version_concurrencia` | Estado del límite combinado de enlaces/mensajes: una fila del último día usado por cuenta. No contiene entradas, hashes o resultados. Se sustituye en un uso posterior y se elimina con la cuenta. |

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
| Edición del nombre | Bearer, sesión activa y nombre del propietario. | Actualiza solo el nombre y versión de concurrencia; mantiene otros datos y accesos. La navegación lee el perfil guardado, sin renovar JWT. Sin nueva recopilación o evento de seguridad; exportaciones futuras incluyen el nombre actual. Cambio de correo separado con ambas confirmaciones. [Alcance](docs/ACCOUNT_EMAIL_CHANGE.md). |
| Dashboard | Bearer con sesión activa y UUID de usuario. | Puntaje más reciente, total y hasta cinco análisis propios con identificador, puntaje y fecha. No genera un análisis. |
| Renovación y logout | Refresh opaco; el cierre global utiliza Bearer. | Rota la credencial o revoca sesiones. No elimina cuenta ni análisis. |
| Cambio autenticado de contraseña | Bearer, contraseña actual/nueva y confirmación. | Actualiza hash, cierra sesiones web e invalida recuperación pendiente en la misma transacción que el evento mínimo. Sin nuevos campos personales; aviso posterior sin secretos o enlace. Mantiene contenido y agentes; cola/correo tienen custodia independiente. [Uso](docs/ACCOUNT_PASSWORD.md). |
| Sesiones activas | Bearer con sesión activa; consulta filtrada por dueño y cierre confirmado de una sesión propia. | Muestra solo id técnico, fechas y bandera de sesión actual, 50 por página. Sin IP, navegador o ubicación. El cierre individual agrega el evento mínimo existente, sin id de sesión en el historial. No borra credenciales inmediatamente; quedan sujetas al mantenimiento técnico. [Alcance](docs/ACCOUNT_SESSIONS.md). |
| Recuperación | Correo para solicitar enlace; token y nueva contraseña para restablecer. | La solicitud responde de forma genérica. El reset consume el token y revoca las sesiones; exige volver al login. |
| Verificación | Sesión para reenviar correo; token del enlace para confirmar. | Confirma la dirección y consume el token, sin acreditar identidad civil. |
| Salud | Sin cuenta requerida; readiness limitado por IP. | `/health` confirma respuesta HTTP; `/health/ready` ejecuta SELECT 1 y publica solo disponible/no disponible para PostgreSQL, sin datos de conexión. No verifica correo o esquema. |
| Dispositivos del propietario | Sesión de usuario, nombre e identificador del dispositivo. | Lista y consulta propios, crea, emite código o revoca. Crear y emitir códigos exige correo verificado en la base. |
| Vinculación del cliente | Código de un uso y `agentVersion`. | Consume el código y devuelve una credencial opaca de dispositivo, solo al canjearlo. La respuesta contiene el secreto; no debe registrarse o compartirse. |
| Heartbeat | Credencial `Device` en Authorization y `agentVersion` en el cuerpo. | Actualiza fecha de contacto y versión declarada. No recoge hardware, archivos, estado de Windows ni resultados de verificaciones. |
| Ingesta de análisis | Credencial Device, esquema 2, UUID estable del reporte, versión del cliente, fechas y diez estados/evidencias con regla/version. | Crea análisis y diez resultados con metadatos del catálogo, puntos confirmados y cobertura. Un reintento idéntico devuelve lo guardado; no duplica el historial. Cuenta y dispositivo proceden de la credencial. |
| Exportación individual | Sesión Bearer del propietario e identificadores de dispositivo/análisis. | JSON de versión 1 con dispositivo id/nombre, análisis completo y feedback propio. No agrega campos de cuenta o secretos; texto libre intacto. |
| Exportación de datos de cuenta | Sesión Bearer del propietario. | JSON de perfil con correo/nombre, todos los dispositivos, análisis/resultados, feedback y marcas mínimas propios. Excluye registros internos de autenticación y hashes. |
| Borrado individual | Sesión Bearer, dispositivo/análisis propios y confirmación verdadera. | Elimina análisis, todos los resultados y feedback; guarda solo dispositivo/reporte/fecha si había ReportId. No borra descargas, informes locales o backups. |
| Detalle de dispositivo | Bearer del propietario y UUID del equipo. | Devuelve hasta 20 análisis propios recientes y 100 hallazgos por análisis; la lista general devuelve hasta 100 dispositivos. Son consultas acotadas, sin paginación ni generación de resultados. |

Next.js procesa formularios mediante Server Actions y consulta la API desde
el servidor. Sus estados de error no devuelven contraseñas. Se puede editar
el nombre; el cambio de correo exige ambas confirmaciones
y reautenticación. La cuenta puede eliminarse con contraseña actual
y confirmación expresa desde Mi cuenta.

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

## Revisión de enlaces y mensajes

`/tools/scam-check` procesa únicamente el enlace/mensaje que el usuario decide
enviar después de eliminar datos privados y confirmar. El contenido llega al
servidor de Security Center AI para reglas locales; no se abre el destino ni se
comparte con IA externa. No se guardan entrada, hash, reporte o historial de
consultas. El resultado muestra observaciones fijas y dominios/IP, sin rutas,
parámetros o credenciales de URL. Puede permanecer en memoria durante la petición
y en el navegador mientras esté visible; limpiar contenido retira esa vista.

La tabla `link_message_usage` guarda una fila por cuenta con `id_usuario`,
`dia_utc`, `consultas_realizadas` y `version_concurrencia`: último día usado y
cantidad para hacer cumplir el límite diario combinado. Un uso del siguiente
día sustituye el estado anterior; no acumula filas diarias. Se conserva mientras
exista la cuenta o hasta su sustitución; borrar cuenta elimina la fila en cascada.
No se reinicia al cambiar contraseña/correo o iniciar otra sesión. El plazo de
operación pública requiere revisión; es estado técnico, no analítica de producto.

La exportación incluye únicamente día/cantidad propios o null. Los backups
cifrados contienen el contador y conservan custodia independiente. Logs de la
API no incluyen cuerpos; Next excluye la ruta del log entrante de desarrollo.
Antes de publicar deben revisarse proxies/telemetría y políticas, no solo estas
exclusiones. [Contrato, límites y reglas](docs/LINK_MESSAGE_CHECKS.md).

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
| Logs | API con lista cerrada de categorías y JSON mínimo: plantilla de ruta, estado, duración, id aleatorio por petición y código de fallo; sin cuerpos, cabeceras, scopes, SQL o excepción. Next.js omite del log de entrada de desarrollo las rutas con tokens de correo. | No crea archivos o tabla de solicitudes. Terminal/hosting pueden conservar la salida; acceso, plazos y alertas siguen pendientes. Otros servicios, scripts y fallos anteriores al arranque tienen alcance propio. [Alcance](docs/OPERATIONAL_MONITORING.md). |
| Límites | IP en memoria, contador de login y fechas de envío en la cuenta. | La IP de Next.js es compartida en las llamadas intermediadas. No existe inventario de direcciones IP persistido por la aplicación. |
| Pruebas | Cuentas ficticias únicas; la suite E2E elimina sus registros por correo exacto y borrado en cascada. | Interrumpir una suite puede dejar cuentas o mensajes de prueba. Las trazas, vídeos y capturas automáticas están deshabilitados. |
| Herramientas del entorno | Editor, extensiones, Docker Desktop y SDK tienen diagnósticos y telemetría propios. | Sus configuraciones no equivalen a una política del producto y no están controladas por estas entidades. |

No hay integración de analítica de uso de producto, proveedor de IA, pagos ni
almacenamiento de archivos personales. Esto no describe servicios externos
que el desarrollador use fuera de la aplicación.

## Retención, borrado y backups

El [mantenimiento técnico](docs/RETENTION_MAINTENANCE.md) incorpora vista previa y
purga opcional, desactivada por defecto. Con los valores iniciales, sesiones y
credenciales de dispositivo vencidas o revocadas son elegibles tras 30 días;
tokens de cuenta y códigos de vinculación inválidos tras 7 días. El plazo cuenta
desde expiración, consumo o revocación según la categoría, no desde creación.
Cada aplicación limita las filas por categoría y exige habilitación. Los refresh
se conservan durante toda la sesión activa, incluidos consumidos para detectar
reutilización; solo se eliminan por cascada con una sesión cerrada antigua.

No hay purga automática de cuentas, dispositivos, análisis, hallazgos, feedback
o marcas mínimas. Vencer, consumir o revocar por sí solo no elimina una fila.
La actividad de seguridad tampoco se purga con sesiones/tokens. Su política de
conservación y eventual limpieza queda pendiente; se elimina con la cuenta.
Los plazos técnicos iniciales no sustituyen una política de conservación de
contenido y copias acordada para producción. No se aplicó purga a la base personal
durante las pruebas de este bloque; la aplicación se verificó en una base aislada.

El propietario puede exportar un análisis individual y su feedback como JSON o
eliminarlo con confirmación desde **Entender este análisis**. El borrado elimina
análisis, resultados y todo su feedback de los datos activos, conservando una
marca mínima dispositivo/reporte/fecha cuando existía `reportId`. No conserva
el hash, puntos, evidencia o comentario. La marca evita restauración por reintento
del mismo reporte, no es anónima y no tiene purga mientras exista el dispositivo.
La actividad privada agrega solo un evento genérico por cuenta de análisis
eliminado, sin su UUID, dispositivo, reporte o contenido. No permite reconstruirlo.
Revocar no elimina físicamente el dispositivo ni la marca; su borrado físico
elimina la marca por cascada. Los análisis legacy sin reporte no generan marca.

Desde **Mi cuenta** puede descargarse un JSON de perfil y todo el contenido
propio del producto: dispositivos, análisis/resultados, feedback y marcas mínimas.
Incluye equipos revocados y registros antiguos sin dispositivo. Excluye tablas
internas de autenticación, hashes y secretos; no es una copia íntegra de la base
ni un mecanismo de restauración. [Contenido y límites](docs/ACCOUNT_EXPORT.md).
La copia incluye también toda la actividad propia: UUID de entrada, acción y
fecha UTC. La pantalla permite consultar 50 por vez, filtrar acción/días UTC y
continuar hacia eventos anteriores; ambos accesos requieren sesión. Los filtros
no cambian la exportación ni se almacenan en una tabla nueva. Las URLs del
historial pueden conservarlos en el navegador; la ruta usa no-referrer y se
excluye del log entrante de Next en desarrollo.

La eliminación de cuenta está disponible desde **Mi cuenta**, con contraseña
actual y confirmación exacta. Elimina físicamente el perfil y sus dispositivos,
sesiones, tokens, análisis, resultados, feedback y marcas mínimas por cascada.
No conserva una marca nueva de cuenta eliminada. Registrar nuevamente el correo
crea otra identidad sin el historial anterior. Logout, revocación y reset no
eliminan el historial. [Alcance del borrado](docs/ACCOUNT_DELETION.md).
La actividad de esa cuenta también desaparece y no se retiene una entrada nueva
de cuenta borrada. Los backups o exportaciones anteriores mantienen custodia propia.

Hay [backup manual cifrado con ensayo aislado](docs/DATABASE_BACKUPS.md). Incluye
el conjunto de la base, con hashes y registros de autenticación; se cifra con una
contraseña independiente y se guarda en LocalAppData, fuera de OneDrive/repositorio.
El ensayo restaura únicamente en una base nueva temporal y la retira; no reactiva
los datos o accesos en la API. No hay calendario, copia externa ni sustitución
automatizada de la base activa. Los plazos y la operación de copias siguen pendientes.
Una exportación manual
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
| Control de la cuenta | Perfil, correo, recuperación, revocación de accesos, exportación y borrado de cuenta con contraseña/confirmación. | Preferencias de recopilación y retención/operación de copias. |
| Hallazgos | Ingesta de diez resultados, consulta, exportación JSON y borrado individual con feedback y marca mínima. El resultado se identifica como limitado y autodeclarado. | Retención automática, tratamiento de copias y revisión de nuevas categorías antes de ampliar las comprobaciones. |
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

## Feedback privado de análisis (2026-10-22)

Se guarda valoración, comentario opcional (1000 caracteres), versión de guía,
usuario/análisis y fechas UTC. La aplicación lo muestra solo al propietario;
administradores de la base y copias pueden leerlo. No se envía a IA externa ni
se recopilan métricas de uso nuevas. Se reemplaza al editar, sin historial de
versiones del comentario. No incluir secretos. La exportación individual incluye
el feedback propio; eliminar un análisis borra todo su feedback junto con sus
evidencias. Una copia descargada o un backup no se borra mediante esa acción.
La retención automática de contenido sigue pendiente; el borrado de cuenta tiene el alcance
definido en [su guía](docs/ACCOUNT_DELETION.md).
[Detalle](docs/LOCAL_GUIDES_AND_FEEDBACK.md).

## Distribución y diagnóstico del agente

El ZIP no incluye datos de cuenta, credenciales ni informes. La carpeta del
programa se mantiene separada del estado DPAPI y los informes locales existentes.
El diagnóstico `doctor` muestra versión del agente/runtime, sistema/build,
arquitecturas, elevación y disponibilidad de requisitos. No incluye hostname,
usuario, rutas personales, identificadores de dispositivo, claves o estados de
protección; no lo guarda ni envía. `doctor --json` permite que el usuario capture
voluntariamente ese resumen. No implica consentimiento para compartir informes
de `scan`, que tienen un alcance y tratamiento diferentes.

## Comparación manual

La comparación reutiliza exclusivamente los análisis guardados del mismo usuario
y dispositivo. No crea un nuevo registro, métrica, correo ni transferencia a IA.
La URL de selección incluye identificadores de informes; las rutas de dispositivos
mantienen `no-referrer` y `no-store` en la web. La evidencia se muestra como texto
y el acceso usa sesión de cuenta, no credencial de agente.

## Exportación individual y marca de eliminación

El JSON descargable tiene `exportVersion=1`, fecha UTC de generación, UUID y
nombre de dispositivo, DTO completo del análisis/resultados y feedback propio
(valoración, comentario, versión de guía y actualización), o `null` si falta.
No agrega identificadores/perfil de cuenta, correo, secretos o hashes internos.
El texto libre se conserva: un comentario o evidencia podría contener datos
personales aportados previamente. No debe presentarse como exportación anónima.

Se entrega con `no-store` pero el navegador crea una copia local en texto claro.
La aplicación no administra su custodia o borrado ni el de copias compartidas,
capturas, informes del agente y backups. El borrado individual solo afecta los
datos activos y conserva la marca mínima descrita; no cambia Windows ni borra
la credencial local. El agente actualizado pide un escaneo nuevo ante `410` y
no reenvía automáticamente el informe eliminado.
[Uso y límites](docs/ANALYSIS_HISTORY.md).

## Descarga de datos de cuenta

El cambio de correo agrega una fila técnica por usuario en `email_change_requests`:
correos actual/nuevo, dos hashes de tokens, id y fechas/versión. No guarda claves
o tokens en claro. Finalizar/cancelar/cambiar o recuperar contraseña retira esa
solicitud; borrar la cuenta elimina la fila en cascada. Si solo vence, el
mantenimiento la considera siete días después y sigue desactivado por defecto.
Vencimiento no equivale a borrado físico. [Uso](docs/ACCOUNT_EMAIL_CHANGE.md).

El historial solo agrega `email.changed`, sin direcciones. Los mensajes de
confirmación llevan tokens a sus respectivos destinatarios; los avisos posteriores
al cambio de correo o recuperación no llevan enlaces/credenciales. No se cambió
de proveedor ni se habilitó envío fuera del capturador local. Mailpit, backups y
colas conservan su custodia independiente. Exportación incluye solo metadatos
de la solicitud pendiente, sin hashes o material de entrega.

La exportación desde **Mi cuenta** contiene deliberadamente identificador,
nombre y correo del perfil, fecha de creación y estado de verificación, además
de todos los equipos e informes propios. Incluye hallazgos completos y feedback,
los equipos revocados, los análisis antiguos sin dispositivo y las marcas mínimas
de eliminación. Estas marcas no restauran evidencia, puntos o comentarios borrados.

La API lee una instantánea coherente de PostgreSQL sin modificar registros.
No se exportan tablas de sesiones, renovación, tokens de correo, vinculación o
credenciales del agente; tampoco hashes o versiones técnicas de concurrencia.
El archivo sí puede contener datos personales escritos en evidencia o comentarios.

La ruta web autenticada intermedia desde el servidor, con `no-store`,
`no-referrer` y adjunto JSON. No guarda un archivo de exportación en el servidor.
Las solicitudes comparten la IP de Next.js y están limitadas por `AccountExport`.
El usuario controla la conservación y uso del JSON descargado, que está en texto
claro. El borrado individual de la aplicación no retira esa copia.
[Contenido, exclusiones y uso](docs/ACCOUNT_EXPORT.md).
