# Roadmap

Actualizado: 2026-10-26. [Inventario consolidado de pendientes](docs/PENDING_FEATURES.md).

La planificación sigue la [Guía Maestra de Security Center AI](docs/Guia_Maestra_Security_Center_AI_Actualizada.pdf), documento 1.0
del 2026-07-27. Su número de versión identifica el documento; el producto
todavía no tiene un lanzamiento público 1.0. Las versiones de esta página son
etapas previstas y no implican releases ni fechas de entrega confirmadas.

## Estado actual: base local y funciones hacia 1.0

**Foco acordado el 2026-10-24:** completar las funciones de la **1.0 gratuita**
según la página 7 de la guía. Las pruebas en otra PC/VM quedan pospuestas por
el usuario; no bloquean avanzar en software ni se consideran aprobadas. El
[plan de cierre 1.0](docs/RELEASE_1_0.md) distingue funciones, compatibilidad y
condiciones de publicación. Drivers, escritorio y monetización mantienen sus etapas.

La base del backend funciona en desarrollo local. Durante la sesión del
2026-10-20 se verificaron compilación, seis pruebas automatizadas y el flujo
HTTP de registro, login y dashboard con PostgreSQL. Esto registra una
verificación realizada; no garantiza que la API esté ejecutándose al leer este
archivo. También se implementó la web de registro, login y dashboard: sus 16
pruebas unitarias, siete E2E con PostgreSQL real, chequeo de tipos y build de
producción pasaron. Se revisó la UI en escritorio y móvil.
El 2026-10-21 se comprobó el bloque de cuentas y sesiones: 30 pruebas del
backend, 17 unitarias de web y 17 E2E con PostgreSQL y SMTP reales aprobados.
El bloque posterior de dispositivos amplió esas suites a 46 pruebas del backend,
21 unitarias web y 24 E2E, con tipos y build correctos. Esta base prepara el
cliente Windows. El bloque posterior añadió una consola .NET 8 con diez
comprobaciones locales de solo lectura, vinculación y heartbeat; su validación
se registra por separado en el changelog. La ingesta manual añade el envío
explícito, puntos confirmados, cobertura e historial por dispositivo; su
validación tiene un registro propio, sin reutilizar como evidencia las pruebas
de bloques anteriores.
La beta privada 0.5 todavía está incompleta. Los comandos de ejecución están en
[README.md](README.md).

| Componente | Estado comprobado |
| --- | --- |
| API .NET 8 | Estructura API, Domain e Infrastructure; Swagger y `/health`. |
| Autenticación | Registro, login, JWT con sesión, verificación de correo, recuperación y controles de intentos. |
| PostgreSQL / EF Core | Usuario, análisis, sesiones, credenciales de renovación y tokens de cuenta; migraciones versionadas. |
| Dashboard del backend | Devuelve el último puntaje guardado, total y cinco análisis recientes del usuario autenticado. |
| Desarrollo local | SDK fijado, herramienta local de EF y script para iniciar, migrar y probar. |
| Pruebas del backend | 390 aprobadas, incluidas 118 de IA; las 40 PostgreSQL omitidas de la suite habitual conservan su corrida aparte en bases temporales. Dependencias de pruebas parcheadas y auditoría NuGet sin alertas conocidas. [Evidencia](docs/VERIFICATION.md). |
| Frontend Next.js | Registro, login, dashboard, cuenta y formularios de verificación y recuperación en español. |
| Sesión web | Cookies `HttpOnly` de access y refresh, renovación rotatoria, logout y cierre global con revocación en la API. |
| Correo local | Mailpit y cola cifrada persistente en PostgreSQL, con reservas/reintentos recuperables. DPAPI del usuario Windows de la API. SMTP externo, claves del alojamiento y operación pendientes. |
| Pruebas de la web | 103 unitarias aprobadas, tipos y build comprobados. Suite E2E revisada con base, API y web propias; IA con transporte ficticio. [Resultados y repetición](docs/VERIFICATION.md). |
| Dispositivos y base de resultados | Vinculación, credencial propia, revocación e historial de resultados por dispositivo, con autorización separada de cuenta y cliente. |
| Agente Windows y verificaciones locales | Consola con diez comprobaciones, credencial DPAPI, heartbeat y envío explícito. Distribución portátil ZIP win-x64 con runtime incluido, diagnóstico local y manifiesto de integridad; sin servicio ni instalador. |
| Pruebas del agente | 145/145 aprobadas nuevamente en la corrida consolidada del 2026-10-26: DPAPI, consultas controladas, envío, lectura y `410`. Las diez sondas reales conservan las comprobaciones manuales de bloques previos. |
| Ingesta y puntos confirmados | Esquema 2, identificador de reporte idempotente por dispositivo, validación de diez reglas, catálogo del servidor, cobertura y resultados parciales; evidencia de pruebas en el changelog. |
| Explicaciones | Guías locales v1 y feedback privado. Explicación de IA opcional por análisis, desactivada por defecto. El usuario habilitó la configuración con clave de producto y `openrouter/openrouter/free`; API reiniciada y habilitada para una prueba explícita desde la interfaz, con una respuesta real sintética de diez comprobaciones y sin usar el informe personal. Mantiene puntos/cobertura y datos reducidos. [Alcance](docs/AI_EXPLANATIONS.md). |
| Enlaces y mensajes | Revisión explícita con reglas locales limitadas, sin abrir destinos. Cuota diaria combinada por cuenta, persistida en PostgreSQL; no guarda contenido o resultados. |
| Presentación pública | Landing local para visitantes, registro, recorrido, datos y FAQ. Sesiones existentes conservan acceso directo al dashboard. Políticas/contacto públicos pendientes. |
| Primeros pasos | Progreso propio de correo/equipo/vinculación/análisis; ZIP privado configurado, integridad comprobada y guía de comandos manuales. No ejecuta el agente desde la web. |
| Prioridades e historial | Último informe separa revisión y desconocidos; enlaces a comprobaciones, tabla con equipo/cobertura y anteriores plegables por equipo. Reutiliza lecturas autorizadas, sin recalcular resultados. |
| Información y ayuda | Privacidad/términos en borrador, enlaces antes del registro y preparación local de errores, sin envío o aceptación contractual ficticios. Responsable/país/correo y canal público pendientes. |
| Comparación de análisis | Dos informes del mismo equipo, diferencias de estado y evidencias; puntos/cobertura solo con criterios compatibles. Adelanto de esta parte de 1.1. |
| Control del historial | Exportación JSON de un análisis y feedback propio; borrado individual confirmado, marca mínima de eliminación y rechazo `410` del mismo reporte. La validación de este bloque se registra por separado. |
| Exportación de cuenta | JSON de perfil, todos los equipos e informes, feedback y marcas mínimas propios; incluye historial antiguo sin dispositivo y equipos revocados. |
| Mantenimiento técnico | Vista previa y purga confirmada por lotes de autenticación antigua; trabajo periódico desactivado por defecto. Conserva contenido y hashes de sesiones activas. |
| Backups locales | Copia manual cifrada, inventario de la misma instantánea y restauración temporal. `rehearse` comprueba invalidación de accesos y conservación del contenido solo en esa copia. Programación, copia externa y recuperación real pendientes. |
| Actividad privada de seguridad | Catorce tipos de acciones completadas; filtros de acción/días UTC y continuación hacia eventos anteriores, 50 por consulta. Exportación completa sin filtros. Se elimina con la cuenta. Auditoría externa y monitoreo siguen pendientes. |

El dashboard consulta análisis almacenados. El agente puede comprobar diez
aspectos de Windows y producir un informe local. Al solicitar el envío, la API
guarda el análisis y diez resultados, calcula puntos confirmados y cobertura y
la web permite consultarlos. El resultado es limitado y autodeclarado por el
cliente: no constituye una certificación de la PC. Las
pruebas habituales del backend usan EF Core InMemory o pruebas locales sin base;
las suites PostgreSQL de retención y backups usan bases temporales explícitas.
La suite E2E de la web utiliza
PostgreSQL real por separado.

## Etapas del producto

| Versión | Objetivo | Alcance previsto | Estado |
| --- | --- | --- | --- |
| 0.1 | Base interna | Backend, PostgreSQL, autenticación, Swagger y dashboard básico. | Base local verificada y documentación organizada; continúa la evolución interna. |
| 0.5 | Beta privada | Frontend, dispositivos, hallazgos, puntaje y primeras verificaciones. | Recorrido manual agente/API/web, guías locales, feedback y paquete portátil para pruebas supervisadas. Pendientes compatibilidad en otras PC y preparación operativa de una beta externa. |
| 1.0 | Lanzamiento público gratuito | Web pública, agente Windows, historial y detector básico de enlaces y mensajes. | Foco actual de desarrollo. Landing y detector incorporados; faltan cierre del recorrido, políticas/contacto y preparación de publicación. Compatibilidad externa pospuesta. |
| 1.1 | Automatización | Análisis programados, alertas, comparación de resultados y verificación de soluciones. | Comparación manual de resultados adelantada. Programación, alertas y verificación de soluciones siguen pendientes. |
| 1.2 | Drivers y salud | Errores PnP, drivers faltantes, firmas, versiones y recomendaciones desde fuentes oficiales. | Pendiente. |
| 1.3 | Escritorio | Icono de bandeja, interfaz local, notificaciones y actualizador seguro; visión de vigilancia y autonomía acotadas por plan y aprobaciones. | Pendiente; la consola actual no automatiza análisis ni acciones. |
| 1.5 | Familia | Varios miembros, panel familiar y dispositivos compartidos. | Pendiente. |
| 2.0 | Monetización | Planes pagos, límites por plan, facturación y soporte prioritario. | Pendiente. |
| Futuro | Móvil y negocios | Aplicación acompañante, pequeños negocios y nuevas plataformas. | Exploratorio, sin compromiso de implementación. |

## Próximas prioridades

La documentación, la web, cuentas y dispositivos quedaron
comprobados localmente. El historial E2E utilizó registros sintéticos almacenados en
PostgreSQL. El agente de consola aporta diez comprobaciones y envío manual;
este avance no completa la beta privada y no publica la web.

1. **Implementado en la prueba de concepto:** diez verificaciones iniciales de
   Windows con evidencia seleccionada, severidad y permisos documentados; consola
   con código de vinculación, credencial protegida y heartbeat. Los resultados
   permanecen locales salvo envío explícito; [alcance y uso](docs/WINDOWS_AGENT.md).
2. **Incorporado:** ingesta autenticada e idempotente, cálculo determinista con
   desconocidos y no aplicables explícitos, cobertura y consulta de resultados
   por dispositivo. [Protocolo e interpretación](docs/ANALYSIS_INGESTION.md).
   Comprobaciones efectivas de este bloque en [CHANGELOG.md](CHANGELOG.md).
3. **Incorporado:** guías locales v1 con evidencia, referencias de Microsoft y
   feedback privado editable por análisis. La decisión inicial de no integrar IA
   externa se amplía el 2026-10-25 con explicación opcional, por consulta confirmada,
   sin inferencia externa comprobada y desactivada por defecto. [Guías](docs/LOCAL_GUIDES_AND_FEEDBACK.md),
   [IA opcional](docs/AI_EXPLANATIONS.md).
4. **Incorporado:** distribución portátil `win-x64`, runtime incluido, diagnóstico
   básico sin acceso a credenciales y protocolo de pruebas supervisadas con matriz
   de compatibilidad. La comprobación en otras PC y con usuarios autorizados queda
   pendiente; no se habilita una beta externa. [Distribución y operación](docs/AGENT_PRIVATE_BETA.md).
5. **Pendiente de disponer de otro equipo:** primera prueba supervisada en otra
   PC Windows 11 x64. El usuario solo dispone de la PC actual y pidió adelantar
   otro bloque de software. Se conservan la matriz y el protocolo; no se han
   enviado invitaciones ni publicado servicios.
6. **Incorporado como adelanto de 1.1:** comparación manual de dos informes del
   mismo equipo con estados, evidencias y diferencias de puntos/cobertura cuando
   corresponden. No automatiza análisis ni confirma reparaciones.
   [Uso y límites](docs/ANALYSIS_COMPARISON.md).
7. **Incorporado:** exportación JSON y borrado individual del análisis con su
   feedback, autorización de propietario y confirmación. Una marca mínima evita
   reenvíos del mismo reporte; agente `0.4.1-preview.1` interpreta `410` y pide un
   escaneo nuevo. No borra archivos locales o backups ni implementa eliminación
   total de cuenta. [Uso y límites](docs/ANALYSIS_HISTORY.md).
8. **Incorporado:** exportación de datos de cuenta desde **Mi cuenta**: perfil,
   equipos, todos los informes y resultados, feedback y marcas mínimas. Sin los
   límites visuales del historial; incluye equipos revocados y registros antiguos.
   [Contenido y exclusiones](docs/ACCOUNT_EXPORT.md).
9. **Incorporado:** borrado de cuenta con contraseña actual y confirmación exacta,
   eliminación de los datos activos y cierre del acceso de sesiones/dispositivos.
   Las copias, correos y archivos locales tienen un alcance independiente.
   [Uso y límites](docs/ACCOUNT_DELETION.md).
10. **Incorporado:** mantenimiento de sesiones, tokens de cuenta, códigos de
   vinculación y credenciales antiguas, con vista previa, purga manual confirmada
   y trabajo periódico desactivado por defecto. Preserva hashes de refresh de
   sesiones activas, contenido y marcas mínimas. [Uso y pruebas aisladas](docs/RETENTION_MAINTENANCE.md).
11. **Incorporado:** backup manual cifrado y ensayo de recuperación, con contraseña
   ingresada en terminal, instantánea coherente e inventario de tablas/esquema.
   Solo restaura en bases temporales y las retira; no sustituye la base activa.
   [Uso y límites](docs/DATABASE_BACKUPS.md).
12. **Incorporado:** actividad privada de accesos, recuperación, dispositivos y
   borrado de análisis, persistida junto con las operaciones. Mi cuenta muestra
   hasta 50 eventos por consulta y exporta todos. No cubre intentos fallidos, exportaciones
   o auditoría externa inalterable. [Uso y alcance](docs/SECURITY_ACTIVITY.md).
13. **Incorporado:** readiness de PostgreSQL de solo lectura, liveness independiente
   y logs mínimos con id generado por el servidor. Incluye `scripts/status.ps1`;
   agrega diagnóstico local de cola, avisos por cantidad/edad y JSON de contadores.
   No comprueba entrega SMTP ni activa alertas externas. [Uso](docs/OPERATIONAL_MONITORING.md).
14. **Incorporado:** listado paginado de sesiones web propias y cierre individual
   confirmado desde Mi cuenta, incluida la actual. Conserva otros accesos y la
   autorización independiente de equipos. [Uso y límites](docs/ACCOUNT_SESSIONS.md).
15. **Incorporado:** cambio autenticado de contraseña desde Mi cuenta, cierre de
   sesiones web, invalidación de recuperación pendiente, evento y aviso de correo
   posterior al cambio. [Uso y límites](docs/ACCOUNT_PASSWORD.md).
16. **Incorporado:** edición del nombre en Mi cuenta y consulta del perfil actual
   en la navegación, manteniendo tokens, sesiones y otros datos. El cambio de
   correo se incorporó en un flujo separado con doble confirmación y reautenticación.
   [Nombre](docs/ACCOUNT_PROFILE.md) y [correo](docs/ACCOUNT_EMAIL_CHANGE.md).
17. **Incorporado:** política inicial de contraseñas nuevas, lista local acotada
   y capacidad de sesiones por cuenta probada con concurrencia real. Conserva
   claves y accesos existentes. [Alcance](docs/ACCOUNT_SECURITY_POLICY.md).
18. **Incorporado:** reintentos SMTP temporales, descarte acotado y diagnóstico
   NOOP sin mensaje. Cola cifrada durable incorporada; entrega externa y operación
   permanecen pendientes. [Uso](docs/MAIL_DELIVERY.md).
19. **Incorporado:** verificación local consolidada y definición Windows de CI
   con acciones fijadas y permisos mínimos. Workflow validado con actionlint;
   primera corrida alojada pendiente, sin push. [Uso](docs/VERIFICATION.md).
20. **Incorporado:** cambio de correo con confirmación de ambas direcciones,
   contraseña al solicitar/finalizar, cancelación y cierre web. Recuperación de
   contraseña agrega aviso mínimo y cancela solicitudes de correo pendientes.
   [Uso y límites](docs/ACCOUNT_EMAIL_CHANGE.md).
21. **Incorporado:** revisión privada del historial de seguridad con filtros de
   acción/días UTC y continuación por evento propio, sin límite total de 50.
   Consultas de solo lectura y exportación completa independiente de los filtros.
   [Uso y alcance](docs/SECURITY_ACTIVITY.md). Auditoría operativa global sigue pendiente.
22. **Incorporado para 1.0:** revisión básica de enlaces y mensajes con consentimiento,
   reglas versionadas y límite diario persistente por cuenta. No abre destinos o
   guarda contenido/resultados. [Uso y límites](docs/LINK_MESSAGE_CHECKS.md).
23. **Incorporado para 1.0:** landing de visitantes con registro, recorrido, datos y
   límites claros. No publica el servicio ni anuncia soporte Windows sin validar.
24. **Incorporado para 1.0:** Primeros pasos, progreso con datos propios y descarga
   privada del ZIP seleccionado por el servidor, con sesión/correo verificados e
   integridad comprobada. [Recorrido y límites](docs/GETTING_STARTED.md).
25. **Incorporado para 1.0:** prioridades del dashboard separadas por estado,
   enlaces a cada comprobación y revisión de navegación del historial compacto.
   [Uso y límites](docs/DASHBOARD_PRIORITIES.md).
26. **Incorporado para 1.0:** privacidad/términos en borrador y preparación local
   de errores sin envío. [Estado y controles](docs/PUBLIC_INFORMATION.md).
27. **Incorporado:** recorrido integrado de registro a borrado de cuenta, con
   protocolo de agente e informes ficticios. [Alcance](docs/RELEASE_VALIDATION.md).
28. **Siguiente foco:** completar datos públicos/políticas/canal de recepción y
   preparación de publicación. Responsable, país y contacto siguen sin definir,
   con recordatorio solicitado. Otra PC/VM continúa pospuesta. [Plan 1.0](docs/RELEASE_1_0.md).
29. Antes de un despliegue externo, resolver los controles de cuentas pendientes:
   política de contraseñas, MFA, operación del correo, retención, auditoría y límites
   con proxies o varias instancias. El recorrido básico local ya está comprobado.
   Conservación, copias externas/programación, recuperación real y auditoría
   operativa siguen pendientes; [procedimiento inicial](docs/RECOVERY_RUNBOOK.md).
   Probar en otra PC/VM queda explícitamente pospuesto hasta disponer de ese entorno.
30. **Incorporado:** ensayo aislado de invalidación de sesiones, enlaces, agentes,
   solicitudes y cola de correo restaurados, con conservación por huellas y
   rollback. Esquema revisado obligatorio; base temporal retirada. No concilia
   borrados posteriores ni restablece el servicio. [Uso](docs/RECOVERY_REHEARSAL.md).

## Criterios para la beta y el lanzamiento

Para considerar logrado el primer análisis real:

- El agente se vincula y puede revocarse sin almacenar la contraseña del usuario.
- Una verificación fallida permite conservar los resultados parciales útiles.
- Cada hallazgo contiene evidencia, severidad, recomendación y regla aplicada.
- El dashboard muestra el puntaje y los resultados del dispositivo correcto.
- El cálculo y la validación de resultados tienen pruebas automatizadas.
- Se documentan los datos recopilados y no se envían archivos personales fuera
  del alcance aprobado.

El lanzamiento público gratuito 1.0 requiere landing, operación de
cuentas y correo preparada para producción, un dispositivo por cuenta, análisis
manual, historial básico, detector de enlaces y mensajes con límites diarios,
explicaciones en español, privacidad, términos y canal de reporte de errores.
Landing, detector y Primeros pasos ya están incorporados localmente; el estado
de cada requisito y sus pendientes está en el [checklist 1.0](docs/RELEASE_1_0.md).
La puesta en marcha local actual no equivale a una validación para producción.

Las etapas posteriores deben mantener evidencia verificable, permisos mínimos
y privacidad. Los drivers se recomiendan desde fuentes oficiales, y los planes
pagos se incorporan después de validar uso recurrente, estabilidad y costos.
Cada avance debe reflejarse en [CHANGELOG.md](CHANGELOG.md), con las pruebas
realizadas y las limitaciones pendientes.
