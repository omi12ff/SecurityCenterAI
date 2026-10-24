# Registro de cambios

Las etapas del [roadmap](ROADMAP.md) y las entradas «Sin publicar» no representan
releases públicas. La base de cuentas, agente e ingesta quedó en `b163da7`.
El corte de avance «feat: completa el recorrido local de la primera versión»
consolida los bloques posteriores: guías/feedback, distribución portátil,
comparación e historial, controles de cuenta, detector, landing, Primeros pasos,
prioridades, borradores y reporte local, mantenimiento, backups, monitoreo y CI.
Los resultados y notas de «sin commit» de cada entrada describen su momento de
implementación; este corte los guarda juntos por solicitud del usuario.
No hay push, tag, release pública o despliegue. Las políticas finales,
operación externa y compatibilidad ampliada mantienen sus pendientes.

## Sin publicar — información pública y cierre local 1.0, 2026-10-24

- `/privacy` y `/terms` accesibles antes del registro, con estado de borrador,
  datos pendientes y descripción de los flujos reales. Registro enlaza los textos
  sin guardar una aceptación contractual. Responsable, país y correo siguen sin
  definir; el recordatorio solicitado se conserva en el checklist 1.0.
- `/report-problem` permite preparar, revisar, copiar y descargar un JSON local.
  Solo incluye sección, resumen, pasos, resultado esperado/observado y fecha UTC.
  Exige revisión; editar invalida vista previa y confirmación. Sin endpoint,
  envío, persistencia o recopilación automática de cuenta, URL, logs o archivos.
  Sin JavaScript no habilita preparación/envío nativo de los campos.
- Enlaces de información/ayuda en landing, registro/login, layouts privados y
  errores. Se corrigieron textos antiguos sobre el agente, el inventario de quince
  tablas y la edición del perfil. La landing describe el servicio de actualización;
  no anuncia detección de reinicios pendientes que el agente no implementa.
- Los dos enlaces de la tarjeta al historial del equipo usan navegación completa
  para volver a consultar datos actuales tras envíos externos del agente. El
  recorrido detectó navegación intermitente que permanecía en la lista; se
  comprobaron el título del equipo y el enlace «Ver equipo e historial».
- **96/96 unitarias web**, tipos y build de producción aprobados. **Seis E2E
  seleccionados aprobados juntos**: tres de información/reportes, dos de landing
  y el recorrido completo desde registro/verificación hasta borrado de cuenta.
  Este último comprueba ZIP/hash, protocolo de agente, dos informes sintéticos,
  guías/feedback, prioridades, comparación, exportaciones, marca de borrado/410,
  detector/cuota y revocación de accesos. No ejecuta ni escanea con el agente real.
  Páginas nuevas revisadas visualmente en 360 px; capturas ficticias ignoradas.
- **251 pruebas habituales del backend y 145 del agente aprobadas de nuevo** en
  `ReleaseClosureQa`. Las **23 PostgreSQL** omitidas en la corrida habitual
  aprobaron aparte: 16 de cuentas/actividad, 4 de retención y 3 de backup, en bases
  aisladas retiradas por los helpers. No se repitió toda la suite E2E histórica o
  CI remoto, ni se validó otra PC/VM.
- Un entorno dev quedó respondiendo 500 durante una corrida anterior y se reinició;
  las seis comprobaciones finales aprobaron después. Build y navegación se
  verificaron por separado. Next.js regeneró su `AGENTS.md` y referencias de tipos;
  se leyeron las guías instaladas antes de ajustar la navegación.
- La revisión automática rechazó iniciar una web temporal de producción en 3011
  con `blocked by policy`, sin más detalle. No se reintentó por otro mecanismo;
  la API temporal 5179 se detuvo. La evidencia final distingue build de producción
  de E2E contra desarrollo, que permaneció como entorno de prueba.
- Sin cambios nuevos de C#, esquema o paquete del agente en este cierre.
  Datos personales preservados y fixtures de cuenta/correo retirados. No hay
  commit, release o despliegue; políticas finales, receptor real de errores,
  operación pública y compatibilidad externa siguen pendientes.
- [Información y ayuda](docs/PUBLIC_INFORMATION.md),
  [recorrido y límites de la verificación](docs/RELEASE_VALIDATION.md).

## Sin publicar — prioridades y revisión del historial, 2026-10-24

- **Resumen → Por dónde empezar:** hasta tres comprobaciones para revisar,
  ordenadas por severidad del catálogo, y hasta tres desconocidas en un grupo
  separado que no les asigna prioridad por severidad. Indica totales y enlaces
  directos a cada comprobación de la guía; no recalcula puntos o estados.
- Expone fecha/equipo del último informe y su carácter histórico si el equipo
  está revocado. Registros anteriores, diez desconocidos y criterios no
  aplicables conservan sus límites; no se convierten en una aprobación del equipo.
- El detalle autorizado del informe exacto y la lista de equipos se consultan
  en paralelo, reutilizando la API actual. Una lectura adicional fallida conserva
  el resumen con aviso; una sesión rechazada renueva antes de mostrar los datos.
  La proyección de prioridades no copia evidencia, recomendaciones o feedback.
- Botón Actualizar resumen. Detalle duplicado de puntos/cobertura/conteos plegable
  para mantener visibles las prioridades, con espaciado y cabecera móvil revisados.
- Tabla de cinco registros muestra equipo, fuente, puntos, cobertura, estados y
  acceso revocado; informa la cantidad respecto al total y enlaza cada guía.
- Historial del equipo: más reciente abierto, anteriores plegables y apertura al
  seguir `#analysis-<id>`. Conserva límites de veinte informes/cien hallazgos,
  comparación y controles de exportación/borrado; exportación de cuenta sigue completa.
- **91/91 unitarias web**, tipos y compilación de producción aprobados. Seis casos
  nuevos comprueban orden, separación de estados, estabilidad, ausencia de
  evidencia en la proyección, no aplicables y datos anteriores.
- **Nueve E2E seleccionados aprobados juntos:** dashboard/ingesta, acceso,
  registros anteriores, separación de prioridades, aislamiento, revocación,
  informe desconocido/no aplicable/correcto y navegación de historial compacto.
  Los tres casos nuevos se repitieron tras la revisión visual, y el caso de
  prioridades tras ajustar la cabecera móvil. Capturas sintéticas de 360/1440 px
  revisadas localmente y excluidas de Git.
- Un 429 inicial de envíos se resolvió respetando Retry-After con un reintento
  acotado en el fixture. No se elevaron límites o cambiaron controles de la API.
- Sin cambios de backend, esquema, agente o paquetes. No se repitieron suites
  .NET/PostgreSQL, toda la suite E2E histórica o CI remoto. Datos personales
  preservados; cuentas/correo ficticios retirados. Sin commit o publicación.
- Siguen borradores de políticas/contacto y verificación integrada; permanece
  el recordatorio de responsable/país/correo y la prueba externa pospuesta.
- [Uso y límites](docs/DASHBOARD_PRIORITIES.md).

## Sin publicar — Primeros pasos y acceso al agente, 2026-10-24

- Nueva ruta privada `/getting-started`, accesible desde Resumen y Dispositivos.
  El dashboard vacío ofrece Empezar con mi equipo sin cambiar los destinos de
  registro/login. Consulta perfil y equipo propios para orientar al siguiente paso.
- Progreso de correo, registro, acceso vigente y análisis recibido del agente.
  Descargar, doctor/status o heartbeat no se interpretan como un análisis.
  La revocación conserva el historial pero exige preparar otro acceso activo.
- Guía de extracción, requisitos, vinculación con entrada oculta, scan local,
  revisión del informe y send explícito. Comandos copiables sin código/credencial,
  instrucciones para otra terminal y límites de Windows 11 x64 visibles.
- Descarga del ZIP seleccionado por `AGENT_PACKAGE_PATH`, fuera de public:
  revalidación remota de sesión y correo, formato cerrado, tamaño máximo 128 MiB,
  checksum contiguo/nombre/cabecera/SHA-256 y comprobación por bloques. Entrega el
  mismo descriptor desde el inicio, con cierre al finalizar/cancelar y cabeceras
  privadas. Rechaza queries, archivos inconsistentes y rutas elegidas por el cliente.
- `AGENT_API_URL` separa el origen accesible al agente de la dirección interna
  de la API; valida raíz HTTPS o HTTP loopback y evita sintaxis de shell.
  Paquete o destino ausente dejan mensajes fijos y permiten preparar la cuenta.
- `.env.local` ignorado selecciona el ZIP existente **0.4.1-preview.1** y localhost.
  No se recompiló el agente, modificó el .env de PostgreSQL, agregó una migración
  o creó una release pública. La carpeta del ZIP/checksum debe ser confiable;
  el hash no reemplaza firma de editor o controles de distribución externa.
- **85/85 unitarias web**, tipos y build de producción aprobados; ocho casos
  nuevos comprueban integridad, archivos faltantes, límites, cancelación y comandos.
- **Cinco E2E de Primeros pasos/landing aprobados juntos** contra desarrollo:
  correo pendiente, aislamiento, estados reales, heartbeat, informe sintético,
  revocación, móvil 360 px, descarga/hash y rechazo de sesión revocada/rutas.
  **Un E2E adicional aprobado** contra la compilación de producción en puerto
  temporal, con paquete ausente y destino inválido: mensajes sin rutas internas,
  copiar comando y retorno a Primeros pasos tras renovación. Servidor temporal detenido.
- Cuenta, equipos y contenido personales conservados; fixtures E2E y correo de
  prueba retirados. No se repitieron backend, agente, suites PostgreSQL o toda la
  suite E2E histórica, al reutilizar contratos existentes sin cambios de servidor.
- Siguiente bloque: prioridades del dashboard y revisión del historial. Políticas,
  términos y reporte de errores siguen pendientes de responsable/país/contacto,
  con recordatorio acordado. Pruebas en otra PC/VM continúan pospuestas.
- [Recorrido, configuración y límites](docs/GETTING_STARTED.md).

## Sin publicar — foco 1.0, enlaces/mensajes y landing, 2026-10-24

- **Prioridad del usuario:** completar la 1.0 gratuita de la guía; prueba en otra
  PC/VM explícitamente pospuesta por falta de entorno. Se revisaron las dieciséis
  páginas del PDF original, sin modificarlo. Nuevo checklist distingue software,
  compatibilidad, datos del responsable y condiciones antes de publicación.
- **Resumen → Enlaces y mensajes:** revisión explícita de HTTP/HTTPS o mensaje,
  con consentimiento, correo verificado y reglas locales `local-signals-v1`.
  Muestra observaciones y pasos para contrastar, sin veredictos de fraude/seguridad
  o porcentajes de confianza. Posibles falsos positivos/omisiones visibles.
- Estructura de dirección: HTTP, usuario antes de @, IP, punycode, puerto y dos
  acortadores identificados por dominio exacto. Texto: urgencia, claves/códigos,
  premios, pagos y formato invisible; hasta cinco direcciones por mensaje con
  conteo de omitidas. Regex sin backtracking; 2048/6000 unidades UTF-16 y 32 KiB.
- No abre destinos, DNS, redirecciones o reputación. No guarda entrada, hash o
  resultado; devuelve observaciones fijas y dominios/IP sin rutas/parámetros o
  credenciales. La web usa texto, sin enlaces de destino clicables. Limpiar/editar
  retira resultados anteriores de la vista; no integra IA externa.
- **Cuota:** 25 consultas combinadas por cuenta/día UTC por defecto, configurable
  1–1000. Backend bloquea/revalida usuario y sesión y confirma contador/versión en
  transacción; entre sesiones/instancias comparte PostgreSQL. No consume cuota
  por entrada inválida; una respuesta perdida después del commit puede consumirla.
- Migración `20261024042930_LinkMessageDailyUsage` aplicada: una fila por cuenta
  con último día/cantidad, columnas en español y cascada; no acumula historial
  diario. Exportación v1 agrega metadatos; backups incluyen la tabla. Downgrade
  se detiene con filas para no perder cuotas sin un plan; no se ejecutó.
- Copia técnica DPAPI previa, cifrada de **48.886 bytes**, fuera del repositorio,
  usuario/SYSTEM y descifrado/SHA-256 comprobados. Ligada al perfil Windows actual.
  EF informa que no quedan cambios del modelo pendientes.
- **Landing de visitantes:** presentación, registro/login, recorrido, datos y FAQ;
  conserva acceso directo al dashboard/renovación de sesiones existentes. Estado
  de desarrollo y alcance Windows 11 x64 explícitos. Sin analítica o recursos
  externos nuevos. No publica servicio/paquete ni anuncia soporte Windows 10.
- **251 pruebas habituales del backend aprobadas** en `ScamQa`; veintitrés
  PostgreSQL omitidas explícitamente. **Dieciséis de cuentas/actividad y tres de
  backups aprobadas aparte**, incluidos ocho envíos disputando tres consultas,
  rollback de contador/versión, cascada y restauración de la nueva tabla.
- Diez casos habituales nuevos verifican reglas, límites, consentimiento,
  aislamiento, cambio de día/sesión, exportación y cascada. Un listener loopback
  comprueba que el enlace revisado no recibe una conexión. Sin contenido privado
  en las respuestas/exportación; DTO web acepta también hosts locales válidos.
- **77 unitarias web**, tipos y build de producción aprobados. **Cinco E2E de
  landing/detector aprobados juntos**, con móvil de 360 px, registro, raíz con
  sesión, correo sin verificar, señales, ausencia de enlaces abiertos, exportación
  mínima, limpieza, límite y cambio de día UTC. No se repitió toda la suite histórica.
- API habitual actualizada con **0 errores y 0 advertencias**. Nueve tablas
  personales conservan conteos/huellas; cero cuentas ficticias, bases temporales
  o contadores de herramientas restantes tras limpiar fixtures. Sin cambio del
  perfil, clave o vínculo personal. No se repitieron agente, retención o CI remoto.
- Plan 1.0, contexto, arquitectura, privacidad, seguridad y ADR-0033/0034
  actualizados. Se solicitaron responsable, país y contacto público para completar
  privacidad/términos/canal de errores. Onboarding y prioridades del dashboard
  conservan el siguiente lugar. Sin commit/push o despliegue.
- [Checklist 1.0](docs/RELEASE_1_0.md), [uso y límites del detector](docs/LINK_MESSAGE_CHECKS.md).

## Sin publicar — filtros y continuación de actividad privada, 2026-10-24

- **Mi cuenta → Ver actividad de seguridad:** selección por acción y días
  completos UTC, continuación a eventos anteriores y vuelta a más recientes.
  Conserva 50 resultados por consulta y las horas visibles en America/Asuncion;
  ya no exige exportar toda la cuenta para consultar una entrada anterior.
- Filtros se conservan al avanzar y se restablecen también en los campos al
  limpiarlos. Aplicar una selección nueva retira la continuación anterior.
  Distingue cuenta vacía, consulta sin coincidencias, filtros inválidos y fallos.
  La exportación sigue incluyendo toda la cuenta/actividad, sin estos filtros.
- API GET con `action`, `from`, `to` y `before` opcionales; fechas estrictas,
  orden único fecha/UUID y `nextBefore` aditivo. Resuelve el evento dentro del
  dueño y los filtros; evento ajeno, inexistente o fuera de selección recibe
  el mismo 400. Rechaza parámetros conocidos repetidos y no refleja sus valores.
- Continúa con búsqueda por fecha/UUID menores y materializa hasta 51 resultados,
  sin offset o escrituras. Cada consulta tiene su propia lectura, sin instantánea
  persistente de todo el recorrido. Usa el índice existente; capacidad con grandes
  volúmenes y auditoría operativa global siguen pendientes.
- Sin migración, columnas, categorías de datos o cambios de conservación. Mantiene
  Bearer, propietario de claims, límites y cabeceras privadas. Next excluye la
  ruta del log entrante de desarrollo para no imprimir filtros/continuación.
- **241 pruebas habituales del backend aprobadas** en `ActivityQa`; veintiuna
  PostgreSQL omitidas explícitamente en esa corrida. **Catorce de cuentas/actividad
  aprobadas aparte**, con un ensayo nuevo que comparte el escenario de paginación:
  125 entradas con fecha empatada, nueva entrada entre consultas, orden/ausencia
  de repeticiones, aislamiento, selección vacía y bordes de día UTC. Dos casos
  habituales nuevos incluyen filtros inválidos sin eco o nuevas escrituras.
- **72 unitarias web**, tipos y build de producción aprobados. **Tres E2E de
  actividad aprobados juntos** al finalizar: privacidad, continuación 50/15,
  exportación completa, filtros de acción/días, limpieza de campos, errores y
  pantalla de 360 px. La primera corrida detectó un selector ambiguo con las
  opciones nuevas y una etiqueta implícita del desplegable; se acotó el selector
  a la lista y se asociaron explícitamente etiquetas y campos antes de repetir.
- API habitual actualizada con **0 errores y 0 advertencias**. API/web responden
  HTTP 200. Conteos/huellas de nueve tablas personales conservados y cero cuentas
  ficticias o bases temporales de pruebas restantes. `next-env.d.ts` restaurado
  mediante generación normal de tipos y chequeo posterior correcto.
- Roadmap, privacidad, arquitectura, pendientes y ADR-0032 actualizados. No se
  repitieron agente, retención/backups, toda la suite E2E histórica o CI remoto
  en este bloque. Sin commit/push.
- [Uso, filtros, contrato y límites](docs/SECURITY_ACTIVITY.md).

## Sin publicar — cambio confirmado de correo y aviso de recuperación, 2026-10-24

- **Mi cuenta → Cambiar correo electrónico:** dirección nueva repetida,
  contraseña actual y confirmación de acceso a ambas bandejas. Requiere correo
  actual verificado. Los dos enlaces exigen un POST explícito y no cambian el
  perfil por sí solos. El usuario actualiza las confirmaciones y finaliza con
  contraseña y confirmación de cierre web; puede cancelar antes.
- Solicitud de treinta minutos por defecto, reemplazo con enlaces anteriores
  invalidados y enfriamiento compartido de sesenta segundos. Tokens aleatorios
  separados por propósito, únicamente hashes almacenados. Una dirección no se
  reserva: un conflicto al finalizar conserva la cuenta original.
- Bloqueo/versiones del usuario y transacción coordinan contraseña, sesiones,
  enlaces pendientes y evento `email.changed`. Finalizar cierra todas las
  sesiones web; conserva nombre, clave, contenido, equipos y credenciales de
  agentes. Se intenta avisar a ambas direcciones después del commit.
- **Recuperación de contraseña:** nuevo aviso posterior sin enlace o secreto;
  fallo del aviso no revierte la recuperación. Cambio y recuperación cancelan
  solicitudes de correo pendientes. Recuperación mantiene la invalidación de
  credenciales/códigos de agentes ya existente.
- API privada con límites, cuerpos acotados, contraseña/bloqueo compartidos,
  revalidación de sesión y respuestas fijas. Confirmaciones públicas con propósito,
  uso único y vencimiento. La web retira el token de la URL y usa no-store y
  no-referrer. Logs sin direcciones, cuerpos, tokens o excepciones SMTP.
- Migración `20261024031825_ConfirmedEmailChange` aplicada: tabla con columnas en
  español y una solicitud por usuario; catálogo de catorce acciones. No modifica
  columnas de `users`. Downgrade se detiene ante solicitudes o eventos nuevos;
  no se ejecutó un downgrade. EF no detecta cambios del modelo pendientes.
- Exportación v1 agrega metadatos propios de la solicitud, sin identificadores
  internos o secretos. Borrado de cuenta incluye la fila. Retención opcional usa
  siete días tras vencimiento, con revalidación por id; worker sigue desactivado.
  Backups/ensayo aislado incluyen la nueva tabla y evento.
- Antes de migrar se creó y verificó un backup DPAPI del perfil Windows actual,
  cifrado de **44.790 bytes**, fuera del repositorio y con permisos restringidos.
  Es una copia local ligada a ese perfil, no un backup portátil con contraseña.
- **239 pruebas habituales del backend aprobadas** en `EmailQa`; veinte ensayos
  PostgreSQL omitidos explícitamente en esa corrida y **los veinte aprobados
  aparte**: trece de cuentas/actividad, cuatro de retención y tres de backups.
  Nueve casos habituales nuevos cubren permisos, enlaces, reemplazo/cancelación,
  expiración, exportación, cascadas, retención y fallos de aviso.
- Cuatro ensayos nuevos con PostgreSQL verifican rollback al fallar el evento,
  disputa por una dirección, finalización frente a cambio de contraseña y frente
  a recuperación por el correo anterior. Las carreras se repiten tres rondas;
  solo una operación puede completar. El nuevo ensayo de retención distingue
  id de solicitud y clave de usuario, sin borrar filas recientes.
- **69 unitarias web**, tipos y build de producción aprobados. **Cinco E2E
  seleccionados aprobados juntos**: tres nuevos de correo/recuperación y dos
  anteriores de contraseña. Incluyen ambas bandejas Mailpit, ausencia de efecto
  al abrir enlaces, cierre de cookies/sesiones, login nuevo, cancelación, aviso
  sin enlaces/secretos y pantalla de 360 px.
- API habitual compilada con **0 errores y 0 advertencias** y actualizada;
  API/web/Mailpit responden HTTP 200. Conteos/huellas de nueve tablas personales
  conservados, cero solicitudes pendientes, cuentas ficticias o bases temporales
  de pruebas restantes. Sin cambio de cuenta, clave o vínculo personales.
- Documentación, pendientes y ADR-0031 actualizados. No se repitieron pruebas
  del agente, toda la suite E2E histórica, ensayo limpio o CI remoto en este
  bloque. Correo sigue en cola volátil y desarrollo captura solo en Mailpit.
  Sin commit/push.
- [Uso, contratos y límites](docs/ACCOUNT_EMAIL_CHANGE.md).

## Sin publicar — política de cuentas, correo y verificaciones, 2026-10-24

- **Contraseñas nuevas:** mínimo 15 puntos Unicode, máximo histórico de 128
  unidades UTF-16 y lista local inicial de 23 valores previsibles, sin consulta
  externa o reglas de composición. Registro, reset y cambio autenticado la
  aplican; login/contraseña actual conservan compatibilidad anterior. Un rechazo
  preserva hash, enlaces, sesiones y eventos. Sin migración o cambio de claves
  personales. La lista inicial no es un corpus completo de filtraciones.
- **Sesiones por cuenta:** diez habilitadas por defecto, configurable 1–100.
  Login correcto sin capacidad recibe 409 sin emitir tokens o expulsar accesos;
  refresh conserva el lugar. Conteo/alta coordinan versión del usuario. La web
  muestra el máximo y explica cierre/recuperación para liberar capacidad.
- **Correo:** interfaz de transporte, tres intentos totales por defecto ante
  fallos SMTP temporales y espera acotada. Rechazos permanentes no se repiten;
  material antiguo se descarta y la cancelación interrumpe espera/envío. Sigue
  siendo una cola en memoria, con pérdida/duplicados posibles, sin garantía de
  entrega final. Logs solo agregan código/número de intento.
- **Diagnóstico:** `status.ps1` comprueba saludo/NOOP solo en loopback, sin mensaje
  o credenciales. Se comprobaron disponibilidad y fallo en un puerto sin escucha,
  con salida 1. Readiness HTTP conserva su alcance PostgreSQL independiente.
- **Verificación local:** nuevo `verify.ps1`, configuración separada y restauración
  de `next-env.d.ts`/variable QA en `finally`. PostgreSQL/E2E son opciones explícitas;
  rechaza una compilación usada por la API o direcciones E2E externas/ambiguas.
- **CI preparado:** workflow Windows 2022, Node 24, SDK de `global.json` y `npm ci`;
  acciones fijadas a SHA y token de lectura, sin persistir credenciales. Pasa
  actionlint 1.7.12; herramienta local instalada fuera del repo con SHA-256 del
  archivo verificado. Sin commit/push o ejecución alojada; primera corrida remota
  pendiente. No incorpora PostgreSQL/E2E a ese job.
- `verify.ps1 -Configuration CompleteQa -IncludePostgres` completó **230 pruebas
  habituales del backend**, **145 del agente**, **66 unitarias web**, tipos y
  build de producción. Quince PostgreSQL omitidas explícitamente en la corrida
  habitual y **las quince aprobadas aparte**: nueve de cuentas/actividad, tres
  de retención y tres de backups, con fixtures/CLI aislados y limpieza de bases.
- Once casos habituales nuevos verifican rechazos sin efectos, compatibilidad
  anterior, espacios, recuperación, capacidad, SMTP temporal/permanente,
  cancelación, antigüedad y exclusión de material de envío en logs. Un ensayo
  PostgreSQL nuevo ejecuta tres rondas de ocho logins concurrentes disputando
  dos lugares y comprueba sesiones, refresh y eventos sin altas excedentes.
- **Nueve E2E seleccionados verificados** con PostgreSQL/Mailpit: cambio de clave,
  perfil, sesiones y tres nuevos de política/capacidad. La primera corrida quedó
  8/9 por un selector que también encontraba el anunciador de Next; se acotó al
  formulario y el caso repetido aprobó. No se repitió toda la suite E2E histórica.
- Ensayo limpio en carpeta temporal fuera de OneDrive, con 335 archivos candidatos
  y sin `.env`, node_modules o compilados previos: `npm ci` y helper `CiQa`
  completados. No prueba Windows Server alojado ni otra PC. API habitual compiló
  con **0 errores y 0 advertencias** y permanece activa.
- Conteos/huellas de nueve tablas personales coinciden con el estado previo;
  cero cuentas ficticias o bases temporales restantes. API/PostgreSQL/web y NOOP
  local disponibles. No se cambió la cuenta, contraseña o vínculo personales.
- La revisión automática rechazó el borrado recursivo de la carpeta de ensayo
  con motivo `blocked by policy`; quedó conservada fuera del repositorio y sin
  `.env`. Ubicación y alcance en la guía de verificación. No se intentó otro
  mecanismo de borrado.
- Documentación, pendientes y ADR-0029/0030 actualizados; procedimiento inicial
  de recuperación documentado sin restaurar la base activa, elegir RPO/RTO o
  completar la reconciliación de borrados/accesos. Sin commit.
- [Política](docs/ACCOUNT_SECURITY_POLICY.md), [correo](docs/MAIL_DELIVERY.md),
  [verificación y CI](docs/VERIFICATION.md), [preparación de recuperación](docs/RECOVERY_RUNBOOK.md).

## Sin publicar — edición del nombre del perfil, 2026-10-23

- **Mi cuenta → Tu perfil** permite cambiar el nombre, con validación, espera,
  confirmación y errores. Recorta extremos, conserva caracteres internacionales
  y espacios interiores, exige 2–100 y rechaza controles. El correo sigue pendiente.
- POST privado con DTO de un campo, propietario/sesión de claims, revalidación
  dentro de la operación y bloqueo/versión de usuario. Solo cambia nombre/versión;
  no copia otros campos del cuerpo. Guardar el mismo valor no genera escritura.
- Dashboard consulta el perfil guardado en paralelo con análisis; Dispositivos
  ya lo hacía. El nombre aparece al cargar/navegar incluso con JWT anterior,
  sin renovar tokens, extender vencimientos o cerrar sesiones. Sin migraciones,
  nuevos campos, avisos de correo o ampliación del catálogo de seguridad.
- **219 pruebas habituales del backend aprobadas** en `ProfileQa`; dos nuevas
  centradas en autorización, aislamiento, campos adicionales, sesión invalidada
  y preservación de datos/accesos. **Catorce PostgreSQL omitidas explícitamente**
  en esa corrida. **Ocho ensayos de actividad/cuentas aprobados aparte**, con
  uno nuevo de tres rondas de cambio de nombre concurrente con contraseña.
- Tipos y build de producción web aprobados con salida QA separada. **Dos E2E
  nuevos aprobados juntos**: nombre en perfil/dashboard/dispositivos y otro
  navegador, cookies/vencimientos conservados y sesiones habilitadas; móvil de
  360 px, errores y texto parecido a HTML representado sin ejecutarlo. No se
  repitieron unitarias web, agente, backups, retención o toda la suite E2E histórica.
- API habitual actualizada con **0 errores y 0 advertencias**, sin nuevas
  migraciones. Documentación, pendientes y ADR-0028 actualizados. Sin commit.
- Conteos y huellas de nueve tablas personales conservados; cero cuentas
  ficticias y bases temporales restantes. API y web responden HTTP 200.
- [Uso y límites](docs/ACCOUNT_PROFILE.md).

## Sin publicar — cambio autenticado de contraseña, 2026-10-23

- **Mi cuenta → Cambiar contraseña**: actual, nueva diferente, repetición y
  confirmación de cierre. Errores sin devolver secretos, estados de espera y
  enlace de recuperación. Tras 204 borra cookies y vuelve al login con aviso.
- POST Bearer por propietario y sesión activa revalidada, 2 KiB, cinco intentos
  por minuto/IP además del global. Contraseña actual incorrecta comparte contador
  y bloqueo de login/eliminación. Clave nueva mantiene 8–128 sin normalización;
  no implementa todavía política de contraseñas comprometidas.
- Bloqueo de usuario y versiones con reintentos acotados: hash, cierre de todas
  las sesiones web, consumo de recuperación pendiente y `password.changed` se
  confirman juntos. Conserva correo verificado, contenido y acceso de agentes.
  El aviso posterior por correo no lleva contraseña, token o enlace. Es de mejor
  esfuerzo, sin cola durable; un fallo no revierte el cambio.
- Migración `20261024003036_AccountPasswordActivity`: CHECK ampliado a trece
  eventos, sin nuevas columnas o cambios de contraseñas existentes. Parser,
  mapeo y exportación admiten el código nuevo. El downgrade no elimina eventos
  para adaptar el catálogo anterior; exige planificar compatibilidad.
- **217 pruebas habituales del backend aprobadas** en `PasswordQa`, ocho nuevas;
  trece ensayos PostgreSQL omitidos explícitamente en esa corrida. **Siete
  ensayos de actividad/cuentas aprobados aparte**, incluidos rollback de hash,
  sesiones y recuperación si falla el evento, y tres rondas de cambio concurrente
  con login/refresh que no dejan acceso con credenciales anteriores.
- **Tres ensayos de backup repetidos y aprobados**, con un evento del tipo nuevo
  en la base ficticia, recorrido CLI y contraseña incorrecta. **66 unitarias web**,
  tipos y build de producción aprobados. **Dos E2E nuevos aprobados juntos**:
  error de contraseña actual, cambio/cookies/tokens/aviso SMTP sin secretos y
  actividad; móvil de 360 px con coincidencia y cuenta preservada ante error.
  No se repitieron agente, retención PostgreSQL o toda la suite E2E histórica.
- Copia técnica previa cifrada DPAPI CurrentUser, fuera de OneDrive/repositorio,
  ACL usuario/SYSTEM y descifrado/SHA-256 verificados; 43958 bytes cifrados en
  `LocalAppData/SecurityCenterAI/Backups/migration_<UUID>`. Depende del perfil
  Windows original y no sustituye el backup portátil con contraseña.
- Migración aplicada y API habitual recompilada con **0 errores y 0 advertencias**.
  Sin cambios de modelo pendientes; conteos/huellas de nueve tablas coinciden
  con los anteriores. No se cambió la contraseña personal como parte de las pruebas.
- Documentación, pendientes y ADR-0027 actualizados. Sin commit.
  [Uso, contrato y límites](docs/ACCOUNT_PASSWORD.md).

## Sin publicar — inventario y cierre individual de sesiones, 2026-10-23

- **Mi cuenta → Ver sesiones activas**, `/account/sessions`: fechas de creación
  y vencimiento, sesión actual primero, otras por fecha/UUID, 50 por página,
  anterior/siguiente y estados vacío/error. Aclara que una sesión habilitada no
  implica presencia en línea. Sin nuevos campos de IP, navegador o ubicación.
- GET privado por propietario y POST confirmado para cerrar una sesión propia.
  Bearer, límite `DeviceAccount`, no-store/nosniff, páginas acotadas y cuerpo de
  hasta 1 KiB. Sesión ajena/inexistente produce el mismo 404. DTO sin tokens,
  hashes o versiones; cliente reconstruye solo campos permitidos.
- Reutiliza revocación con versión de concurrencia y evento `session.revoked`
  guardado junto al cambio. Repetir el cierre no duplica el evento. Invalida JWT
  y refresh rotados; conserva otras sesiones y la autorización del agente.
  No cancela solicitudes que ya estaban autorizadas.
- Confirmación por entrada en la web y borrado de las dos cookies al cerrar la
  actual, según bandera calculada por la API. Renovación admite la ruta exacta
  de sesiones y vuelve a página 1. Mantiene el cierre global existente.
- **209 pruebas habituales del backend aprobadas** en `SessionsQa`, seis nuevas.
  Se conservan sin cambios los diez casos anteriores de `AccountSessionTests`;
  los nuevos están en `AccountSessionManagementTests`. **Once ensayos PostgreSQL
  omitidos explícitamente** en esta corrida habitual.
- **Cinco ensayos PostgreSQL aprobados aparte**, tres anteriores y dos nuevos:
  aislamiento/rollback si falla el evento y tres rondas de refresh concurrente
  con cierre duplicado sin reactivar acceso. Base ficticia retirada al terminar.
- **61 unitarias web aprobadas**, cinco nuevas; tipos y build de producción
  correctos con salida QA separada. **Dos E2E nuevos aprobados en corridas
  separadas** con API/web/PostgreSQL reales: navegación, renovación, confirmación,
  cierre individual/actual y cookies; móvil de 360 px con paginación y página vacía.
  Se revisa ausencia de credenciales en el documento y contenido mínimo del listado.
  No se repitió toda la suite E2E, agente o ensayos de backup/retención.
- API habitual actualizada con compilación de **0 errores y 0 advertencias**,
  sin nuevas migraciones. Documentación de uso, privacidad, arquitectura,
  pendientes y ADR-0026 actualizada. Conteos/huellas de nueve tablas coinciden
  con la lectura anterior; sin cuentas E2E o bases temporales de auditoría al
  terminar. API/PostgreSQL disponibles y login web 200; enlaces y espacios correctos.
  Sin commit.
- [Uso, contrato y límites](docs/ACCOUNT_SESSIONS.md).

## Sin publicar — estado de PostgreSQL y logs mínimos, 2026-10-23

- `GET /health` conserva liveness HTTP independiente. `GET /health/ready` abre
  conexión y ejecuta SELECT 1, sin filas personales o escrituras, con tiempos y
  cancelación de tres segundos. Respuesta mínima 200/503 sin conexión/excepción;
  no-store, nosniff y límite adicional de 30 consultas/minuto/IP. No verifica
  esquema, todos los permisos o entrega SMTP.
- Middleware con id de petición nuevo en X-Request-Id y en problemas genéricos
  por excepción. Logs JSON con método cerrado, plantilla de ruta, estado, duración,
  id y código de fallo. Sin URL libre, query, cuerpo, cabeceras, IP, identidad,
  SQL, scopes o excepción. Respuestas iniciadas se abortan si fallan después.
- Lista cerrada de categorías, independiente de niveles verbosos de configuración.
  Avisos mínimos diferenciados de fallo de cola/SMTP y conteos de mantenimiento.
  Sin archivos, colector, métricas, conservación o alertas; scripts y otros
  servicios tienen alcance independiente.
- `scripts/status.ps1` consulta ambas rutas en localhost; códigos 0/1 sin imprimir
  errores internos. `dev.ps1`, al encontrar API activa, informa también PostgreSQL.
  Se comprobaron el recorrido disponible y un puerto local sin servicio.
- **203 pruebas habituales del backend aprobadas**, seis nuevas de salud/logs,
  en `MonitoringQa`; **nueve PostgreSQL omitidas explícitamente** en esta corrida.
  Casos de fallo/cancelación/límite, conexión rechazada fuera de la base personal,
  correlación y marcadores sensibles excluidos aun con logging Trace configurado.
  No se repitieron los ensayos PostgreSQL históricos, agente, web unitaria o E2E.
- API habitual recompilada con **0 errores y 0 advertencias**, actualizada y
  conectada al PostgreSQL real: readiness 200 con id válido y no-store; login web
  200. Sin nuevas migraciones; conteos y huellas de nueve tablas coincidentes con
  la lectura previa. Se revisaron parser PowerShell, enlaces locales y espacios
  en cambios/archivos nuevos. La API y la web permanecen activas.
- Documentación de uso, privacidad, arquitectura, pendientes y ADR-0025 actualizada.
  Sin commit. [Uso y límites](docs/OPERATIONAL_MONITORING.md).

## Sin publicar — actividad privada de seguridad, 2026-10-23

- **Mi cuenta → Ver actividad de seguridad**, en `/account/activity`: últimos
  50 eventos, estados vacío/error, fecha local, alcance visible y enlace a la
  exportación completa. Registro desde esta actualización, sin inventar pasado.
- Doce tipos cerrados: creación/login, cierres de sesión, reutilización de refresh
  bloqueada, verificación/reset, registro/código/vinculación/revocación de equipo
  y borrado individual de análisis. No registra fallos de login, exportaciones,
  todas las peticiones o actividad administrativa de backup/purga.
- Migración aditiva `20261023175852_PrivateSecurityActivity`: cuatro columnas
  en español, catálogo CHECK, índice por dueño/fecha/UUID y FK con cascada. No
  conserva IP, texto libre, secretos o identificadores de sesión/dispositivo/informe.
- La entrada se guarda con la operación en el mismo SaveChanges/transacción.
  EF rechaza cambios/borrados de entradas salvo al eliminar la cuenta. No es una defensa
  ante SQL directo/administradores o una base comprometida. Conservación, auditoría
  externa y monitoreo siguen pendientes; purga técnica no elimina esta actividad.
- GET privado con Bearer y propietario del claim; comparte `DeviceAccount`.
  Exportación de cuenta agrega `securityActivity`, completo y sin límite de 50,
  como campo aditivo del formato 1. Parser web compatible con respuestas anteriores.
- **197 pruebas habituales del backend aprobadas** en `AuditQa`, seis casos nuevos.
  Nueve PostgreSQL se omiten explícitamente en esa corrida. **Tres ensayos nuevos
  aprobados aparte** con base ficticia: rollback si falla el evento, cascadas y
  separación de usuarios, y tres rondas concurrentes de login/revocación/borrado.
  Manejo acotado de conflictos actualizado para el nuevo vínculo al propietario.
- **Tres ensayos de backup repetidos y aprobados**, incluyendo una entrada de la
  tabla nueva en el recorrido cifrado/restaurado. **145/145 del agente** en
  `AgentAuditQa`; **56/56 unitarias web**, tipos y build de producción correctos.
- **Dos E2E nuevos aprobados en corridas separadas** con API/web/PostgreSQL/Mailpit
  reales: navegación desde Mi cuenta, privacidad y aislamiento; móvil de 360 px con
  vacío, límite de 50, cabeceras privadas y descarga de 65 entradas. La navegación
  de escritorio falló en la primera corrida de desarrollo y pasó al comprobarla
  de nuevo por separado; no se cambió su implementación ni se atribuyó una causa
  no demostrada. No se repitió toda la suite E2E histórica.
- Antes de aplicar la migración se guardó una copia técnica cifrada DPAPI CurrentUser,
  con carpeta restringida fuera de OneDrive y comprobación de descifrado/SHA-256.
  Permanece en `LocalAppData/SecurityCenterAI/Backups/migration_<UUID>`; depende del
  perfil Windows original y no sustituye el backup portátil con contraseña.
- Migración aplicada y API habitual actualizada; sin cambios de modelo pendientes.
  Huellas/conteos de ocho tablas anteriores coincidentes, cero cuentas E2E y cero
  bases temporales al terminar. Nueva tabla vacía para el usuario existente hasta
  su siguiente acción registrada; no se fabricaron eventos. API/web respondieron
  `200`. Enlaces, parser PowerShell y `git diff --check` correctos. Sin commit.
- [Uso y privacidad](docs/SECURITY_ACTIVITY.md), ADR-0024 e inventario de pendientes
  actualizados. Siguiente operación por priorizar: monitoreo de dependencias y logs seguros.

## Sin publicar — backup cifrado y ensayo de recuperación, 2026-10-23

- Inventario consolidado de [funciones y controles pendientes](docs/PENDING_FEATURES.md),
  distinguiendo etapas de producto, preparación de producción y la IA externa
  pospuesta por elección del usuario.
- `scripts/backup.ps1` crea manualmente un archivo cifrado con contraseña e
  inventario, en la carpeta dedicada de LocalAppData fuera del repositorio y
  OneDrive. Contraseña por stdin, conexión solo en entorno del hijo y ACL de
  usuario/SYSTEM. Sin secretos en argumentos, nuevos paquetes o migraciones.
- Snapshot PostgreSQL compartido entre inventario y `pg_dump` custom. Archivo
  AES-256-GCM con PBKDF2-SHA256, sal/nonce aleatorios y cabecera autenticada;
  límite local de 64 MiB. No se escribe un dump descifrado a disco.
- Antes de publicar el nombre final, el archivo se autentica y restaura en una
  base nueva `scai_restore_test_<UUID>`. Se comparan esquema, conteos y SHA-256 de
  todas las tablas públicas, incluidas autenticación y migraciones. Se retira la
  base en `finally`; no hay parámetro para restaurar sobre la base activa.
- Los ensayos detectaron representaciones textuales distintas de casts de arreglo
  en dos CHECK tras `pg_restore`. Se compara ahora la expresión normalizada por el
  planificador PostgreSQL, sin ANALYZE ni eliminación de controles. Un ensayo
  adicional comprobó que un CHECK de significado diferente cambia la huella.
- **191 pruebas habituales del backend aprobadas** en `BackupQa`, con diez casos
  nuevos de formato/cifrado/rutas/argumentos. Seis PostgreSQL se omitieron
  explícitamente en esa corrida: tres de retención aprobadas en el bloque anterior
  y tres nuevas de backup, **aprobadas aparte** con `scripts/test-backup.ps1`.
- Los tres ensayos nuevos usaron una base vacía migrada y datos ficticios:
  contenido completo, escrituras concurrentes, contraseña/archivo incorrectos,
  inventario incoherente, origen inexistente, cambio de CHECK y verificación de
  una copia anterior a un borrado sin restaurar datos en el origen. CLI y
  contraseña Unicode por stdin también aprobados. Se retiraron bases y archivos.
- **145/145 pruebas del agente aprobadas** en `BackupQa`. Compilación de API sin
  errores ni advertencias. Sin cambios de frontend; no se repitieron sus suites
  o los E2E de bloques previos.
- No se creó una copia personal con una contraseña elegida por el agente. El
  usuario crea su primer backup ingresando su contraseña en la terminal.
  Siguen pendientes calendario, conservación/copia externa, auditoría y recuperación
  real con tratamiento de borrados posteriores y accesos restaurados.
  [Comandos y límites](docs/DATABASE_BACKUPS.md), ADR-0023.
- Al cerrar, ocho tablas habituales conservaron conteos/huellas, no quedaron
  bases de fixture/restauración de este bloque y API/web respondieron `200`.
  Scripts parseados, enlaces locales válidos y `git diff --check` sin errores.
  No se reinició la API habitual para este cambio de herramienta ni se hizo commit.

## Sin publicar — mantenimiento técnico opcional, 2026-10-22

- Vista previa y purga de sesiones cerradas, tokens de verificación/recuperación,
  códigos de vinculación y credenciales de dispositivos antiguos. Plazos iniciales
  de 30/7/7/30 días desde expiración, consumo o revocación según categoría;
  opciones validadas y lotes de hasta 200 filas padre por categoría por defecto.
- Los refresh no se purgan individualmente: se conservan todos los hashes durante
  la sesión activa para detectar reutilización. Solo se eliminan por cascada al
  quitar una sesión cerrada antigua. Perfiles, dispositivos, informes, resultados,
  feedback, catálogo y marcas mínimas permanecen fuera del mantenimiento.
- CLI antes del arranque web, sin HTTP, JWT, SMTP ni migraciones.
  `scripts/retention.ps1` ejecuta una vista previa por defecto y exige
  `-Action apply -ConfirmPurge` para borrar. Conexión por entorno y salida con
  conteos/fechas, sin identificadores, secretos o hashes. El helper no habilita
  persistentemente la limpieza de la API.
- Worker **desactivado por defecto**; cuando se habilita espera un intervalo
  completo antes de la primera ejecución. Aplicación en una transacción PostgreSQL,
  bloqueo asesor entre purgas, predicado revalidado al borrar y reintentos acotados
  ante deadlocks/serialización. Vista previa con instantánea `RepeatableRead`.
- **181 pruebas del backend aprobadas** en `RetentionQa`, incluyendo catorce casos
  nuevos. Tres pruebas PostgreSQL se omiten explícitamente en la corrida habitual;
  **las tres se aprobaron aparte** con `scripts/test-retention.ps1`, en una base
  temporal vacía con datos ficticios. Verificaron lotes/cascadas, hashes activos,
  contenido conservado, bloqueo entre purgas y cambio de elegibilidad antes del
  `DELETE`. También pasaron CLI preview/apply y rechazo sin confirmación; la base
  temporal se eliminó al concluir.
- **145/145 pruebas del agente aprobadas** en Release. Compilación de la API en
  `RetentionTools` sin errores ni advertencias. No hubo cambios de frontend y no
  se repitieron sus suites ni E2E de bloques previos.
- La vista previa en la base habitual informó cero elegibles y cero borrados.
  No se aplicó purga a esa base ni se activó el trabajo periódico. Los plazos de
  producción, conservación de contenido, backups/recuperación y auditoría durable
  siguen pendientes. [Operación](docs/RETENTION_MAINTENANCE.md) y ADR-0022.
- Huellas y conteos de las ocho tablas de perfil/dispositivo/contenido/catálogo
  coincidentes antes y después. API habitual actualizada con el worker desactivado,
  sin migraciones pendientes; `/health` y login web respondieron `200`.
  Scripts PowerShell parseados, enlaces locales comprobados y `git diff --check`
  sin errores. Sin commit ni publicación.

## Sin publicar — eliminación de cuenta, 2026-10-22

- **Eliminar mi cuenta** en Mi cuenta: contraseña actual y confirmación exacta
  `ELIMINAR`, con alcance visible y enlace a la exportación previa. La Server
  Action limpia cookies y vuelve al login con aviso solamente tras un `204`.
- `DELETE /api/account` exige Bearer de una sesión activa, rechaza campos extra
  y cuerpos mayores de 2 KiB. El propietario se obtiene del token. Fallos de
  contraseña comparten el contador y bloqueo de login; límite propio
  `AccountDeletion` de tres solicitudes/minuto/IP por defecto.
- Transacción PostgreSQL y bloqueo parametrizado del propietario, con sesión y
  contraseña revalidadas. Las cascadas existentes eliminan perfil, sesiones,
  tokens, dispositivos/credenciales, informes/hallazgos, feedback y marcas mínimas;
  conservan otras cuentas y el catálogo. Registrar el correo nuevamente crea
  otra identidad. Sin migraciones, correos adicionales ni cambios del agente.
- La prueba de concurrencia reprodujo errores 500 por referencias a usuarios
  borrados y deadlocks en solicitudes de login/refresh/ingesta. Se añadió manejo
  acotado de conflictos y de las claves foráneas correspondientes: los accesos
  anteriores se rechazan y las operaciones concurrentes no restauran la cuenta.
- **167/167 pruebas del backend**, **145/145 del agente** y **52/52 unitarias web**
  aprobadas, con tipos y compilación de producción correctos. La suite del agente
  se repitió por los cambios en la ingesta y autenticación.
- **Tres E2E nuevos aprobados en corridas separadas**, con PostgreSQL y Mailpit
  reales: borrado completo y aislamiento de cuentas; confirmación/contraseña en
  móvil de 360 px, cookies vacías y aviso en login; tres rondas concurrentes de
  borrado con ingesta/login/refresh sin restaurar la cuenta ni sus accesos.
  Verificados tokens anteriores inválidos y nuevo registro del correo con UUID
  nuevo e historial vacío. La UI se comprobó en desarrollo y el build de producción
  por separado; no se repitió toda la suite E2E histórica.
- Respaldo local antes de los E2E y huellas/conteos de ocho tablas coincidentes
  al concluir: un usuario, un dispositivo, una credencial, cero análisis,
  hallazgos o feedback, tres marcas mínimas y diez reglas. Ese era el estado
  previo a estas pruebas; se conservaron sus filas sin cambios y se limpiaron
  las cuentas/mensajes ficticios. API de QA detenida; API y web habituales activas.
- [Uso y límites](docs/ACCOUNT_DELETION.md), ADR-0021 e inventario actualizados.
  Descargas, archivos locales, correos/colas y backups quedan fuera de la operación.
  Sin papelera ni reaplicación automática de borrados al restaurar un backup.

## Sin publicar — exportación de datos de cuenta, 2026-10-22

- Panel **Descargar datos de mi cuenta** en **Mi cuenta**, con descarga adjunta
  desde **Exportar mi cuenta (JSON)**. Conserva la sesión y el contenido guardado.
- `GET /api/account/export` obtiene el propietario del Bearer con sesión activa.
  DTO explícitos con `exportVersion=1`, fecha UTC, perfil con correo/nombre,
  todos los dispositivos e informes/resultados propios, feedback y marcas
  mínimas de informes eliminados. Incluye equipos revocados y análisis antiguos
  sin dispositivo; no aplica los límites visuales de veinte informes/cien hallazgos.
- Excluye secretos, hashes y tablas internas de autenticación. No recalcula
  resultados ni reconstruye evidencia eliminada. En PostgreSQL, consultas sin
  tracking y una transacción `RepeatableRead` producen una instantánea coherente.
- Límite `AccountExport` predeterminado de tres solicitudes/minuto/IP, además
  del global. La ruta web intermedia desde el servidor, valida el contrato y
  entrega el adjunto sin exponer Bearer en JavaScript. `/account` y subrutas con
  `no-store` y `no-referrer`; JSON con `nosniff`.
- Sin migraciones, dependencias nuevas ni cambios en el agente. El JSON se
  prepara en memoria; no hay archivo persistido de exportación, tarea de fondo
  ni importación/restauración. El usuario conserva y protege su descarga.
- [Uso y contenido](docs/ACCOUNT_EXPORT.md), ADR-0020 e inventario de privacidad
  actualizados. Borrado completo de cuenta, retención y operación de backups
  siguen pendientes.
- **158/158 pruebas del backend** en `AccountExportQa` y **49/49 unitarias web**
  aprobadas, con tipos y compilación de producción en `.next-qa` correctos.
  Los casos nuevos verifican listas explícitas de campos, aislamiento entre
  cuentas, ausencia de secretos, relaciones, registros antiguos, contenido por
  encima de los límites visuales y el límite específico de solicitudes.
- **Dos E2E nuevos aprobados** en Chromium con PostgreSQL y Mailpit reales:
  cuenta con equipos activo/revocado, feedback y marca mínima; 23 análisis,
  incluidos 21 antiguos sin dispositivo y uno con 101 hallazgos; descarga móvil
  de 360 px, texto inerte y conservación de sesión e informes. La UI se probó
  con el servidor de desarrollo; el build de producción se comprobó por separado.
  No se repitió toda la suite E2E histórica ni la suite del agente en este bloque.
- Tras limpiar las cuentas y mensajes de prueba, las huellas y conteos de siete
  tablas de contenido/credenciales coinciden con la lectura anterior a los E2E:
  un usuario, un dispositivo, una credencial, tres análisis y treinta hallazgos;
  cero feedback y marcas mínimas. Se detuvo únicamente la API de QA; la API
  habitual y la web quedaron activas. `git diff --check` sin errores.

## Sin publicar — exportación y borrado individual de análisis, 2026-10-22

- Panel **Control de este análisis** al pie de **Entender este análisis**:
  **Exportar análisis (JSON)** y eliminación con checkbox de confirmación.
  La web vuelve al historial con un aviso tras borrar y conserva los otros
  análisis y el acceso del equipo.
- `GET /api/devices/{deviceId}/analyses/{analysisId}/export` requiere Bearer del
  propietario y entrega adjunto JSON `exportVersion=1`, fecha de generación,
  dispositivo id/nombre, análisis completo con resultados y feedback propio.
  DTO explícitos sin datos de cuenta, hashes o credenciales; texto libre intacto.
- `DELETE /api/devices/{deviceId}/analyses/{analysisId}` exige
  `{ "confirmed": true }`, rechaza campos extra y limita el cuerpo a 1 KiB.
  Borrado transaccional de análisis, todos sus resultados y todo su feedback.
  Acceso conjunto de usuario/dispositivo/análisis, incluido historial revocado;
  una segunda eliminación devuelve `404`.
- Marca mínima dispositivo/reporte/fecha en `deleted_analysis_reports`, sin
  hash, puntaje, evidencia o comentario. La ingesta devuelve `410` al mismo
  reporte eliminado, coordinando carreras con los marcadores de concurrencia
  de dispositivo y usuario. Legacy sin ReportId no crea marca. Sin purga
  automática mientras exista el dispositivo; FK con borrado físico en cascada.
- Generada `20261022194730_PrivateAnalysisHistory`, que crea la tabla de tres
  columnas en español. Aplicada a PostgreSQL tras un respaldo temporal; se
  revisó que `Up` solo agregue esa tabla, su clave compuesta y FK con cascada.
- Agente **0.4.1-preview.1**: interpreta `410`, conserva credencial e informe
  local y pide realizar un nuevo `scan`. No hay reintento automático ni cambios
  en Windows, descargas, informes locales o backups por borrar en la web.
- Corregida la publicación con PowerShell 5.1 al omitir `-OutputDirectory`:
  el helper resuelve el destino predeterminado `artifacts/agent` después de
  obtener la raíz del repositorio. Sin dependencias nuevas.
- Guía [de control del historial](docs/ANALYSIS_HISTORY.md), ADR-0019 e inventario
  de privacidad actualizados. Siguen pendientes la prueba en otra PC, retención
  automática, eliminación completa de cuenta y controles de operación.

- Validación automatizada aprobada: **147/147 pruebas del backend** en `HistoryQa`,
  **145/145 del agente** en Release y **40/40 unitarias web**, tipos y build de
  producción en `.next-qa`. Comprobado el parseo de exportación web de acuerdo
  con el DTO de análisis de la API.
- Aprobados **tres casos E2E nuevos** con PostgreSQL y Mailpit reales, comprobados
  en corridas separadas: control de acceso/contrato, descarga y eliminación en
  móvil de 360 px, y cinco rondas de carrera entre reenvío y eliminación.
  La UI descargó el JSON esperado, exigió confirmación y volvió al historial
  con aviso, conservando otro análisis. Los casos iniciales con timeout de
  conexión API/PostgreSQL durante el arranque se repitieron con resultado correcto.
  No se atribuye a esta entrega una repetición de toda la suite E2E histórica.
- Paquete final local:
  `artifacts/agent/SecurityCenterAI.Agent-0.4.1-preview.1-win-x64.zip`,
  **33.366.772 bytes**, runtime incluido **8.0.32**, 192 archivos declarados
  y manifiesto. SHA-256:
  `c16431213a578dcbdde5c465449ca3bb7aa7ca5d670976317ea8870649d92258`.
  El helper público aprobó **1.599 comprobaciones**, nueve procesos de
  diagnóstico y cuatro alteraciones rechazadas. La ejecución en el anfitrión
  no equivale a probar una PC limpia sin SDK o un segundo equipo.
- La limpieza dejó un usuario, un dispositivo, una credencial, tres análisis,
  treinta resultados, cero feedback y cero marcas de prueba. Se restauró el
  respaldo previo en una base temporal aislada y coincidieron los conteos y
  huellas de las seis tablas `users`, `devices`, `device_credentials`,
  `security_analyses`, `security_findings` y `analysis_feedback` con la base final.
  Los tres análisis y sus treinta resultados permanecieron intactos. Se retiraron
  la base temporal y el dump intermedio; el respaldo original sigue en TEMP,
  fuera del repositorio.
- Detenida la API de QA en 5179 tras comprobar su propiedad. Se conservaron
  la API habitual en 5169 y la web en 3000, que respondieron correctamente.
  `git diff --check` terminó sin errores y los artefactos locales siguen ignorados.

Las aprobaciones de los bloques anteriores que siguen son antecedentes
separados. La aplicación sigue local, sin publicación, release o commit nuevo.

## Sin publicar — comparación manual de análisis, 2026-10-22

- Adelantada esta parte de 1.1 por elección del usuario de continuar con software
  mientras dispone solo de su PC. La prueba del agente en otro equipo conserva
  su pendiente y no se habilita una beta externa.
- Vista **Comparar análisis** desde el historial del dispositivo: selector GET,
  originales y evidencias de ambos informes, seis categorías de cambios y
  estados vacíos cuando falta un segundo informe. Diseño comprobado a 360 y
  1440 px, con espacio interior de tarjetas y sin desbordamiento horizontal.
- API de lectura con Bearer exclusivo, propiedad conjunta de usuario, dispositivo
  y ambos informes, fechas ordenadas de recepción y captura. Las consultas
  conservan registros originales y funcionan sobre historial de dispositivos revocados.
- Diferencias de puntos/cobertura solo con catálogo completo y criterios
  compatibles. Desconocidos, nueva información, pérdida de información,
  cambios de aplicabilidad, versiones y reglas antiguas ambiguas se distinguen
  sin inventar reparaciones o causas. La evidencia se muestra como texto.
- **127/127 pruebas del backend**, **35/35 unitarias web**, tipos y compilación
  de producción correctos. **3 E2E nuevas aprobadas** con PostgreSQL/Mailpit
  reales: autorización, datos intactos, selección móvil, evidencia inerte,
  errores de selección y estado con un único informe. El recorrido E2E utilizó
  la web de desarrollo; la compilación de producción se comprobó por separado.
- Salida `.next-qa` opcional para compilar sin compartir `.next` con el servidor
  habitual. Sin dependencias nuevas ni migraciones. API y web habituales activas;
  eliminadas las cuentas E2E y conservadas las huellas originales de un usuario,
  un dispositivo, tres análisis y treinta comprobaciones.
- [Uso y límites](docs/ANALYSIS_COMPARISON.md) y ADR-0018. Exportación/borrado del
  historial y tratamiento de reintentos quedan como siguiente bloque de software,
  todavía sin implementar.

## Sin publicar — distribución portátil del agente, 2026-10-22

- Agente `0.4.0-preview.1`: versión leída de los metadatos del ensamblado,
  `version`/`--version` y `doctor [--json]` antes de resolver el almacenamiento.
  El diagnóstico selecciona metadatos de sistema/arquitectura/runtime/elevación
  y requisitos básicos; no usa la API, vault, informes o sondas de protección,
  ni guarda archivos. Un requisito ausente devuelve código 2; no es un puntaje.
- Validación de la ubicación local de Windows: si está vacía o es relativa,
  se rechaza antes de crear estado y no se usa la carpeta del ejecutable como
  alternativa. Se conservan ruta, esquema y DPAPI de las credenciales existentes.
- `scripts/publish-agent.ps1` genera ZIP `win-x64` con runtime .NET 8 incluido,
  sin trimming ni archivo único; binarios, ayuda offline, avisos del runtime,
  acceso `.cmd` y wrapper PowerShell opcional. El `.cmd` sin argumentos abre
  ayuda; ninguna acción de análisis/envío se inicia automáticamente.
- Manifiesto con hashes SHA-256, versiones de agente/runtime y tamaño de cada
  archivo; checksum externo del ZIP. Lista explícita de archivos admitidos,
  carpetas temporales únicas con limpieza verificada y rechazo de sobrescritura.
  No se incluyen `.env`, credenciales o informes. Paquete aún sin firma de código.
- `scripts/test-agent-package.ps1`: estructura, hashes y ejecución de comandos
  inocuos desde ruta con espacios/ñ y sin SDK en `PATH`; rechazo de alteraciones
  de hash interno, archivos extra, rutas fuera del paquete y checksum externo.
  La prueba no vincula, analiza ni envía datos del perfil real.
- **143/143 pruebas del agente aprobadas**, incluidos requisitos del diagnóstico,
  metadatos seleccionados, elevación, versión única y almacenamiento sin fallback
  relativo. Los cambios no requieren migraciones de base ni reinicio de API/web.
- Paquete final local: `artifacts/agent/SecurityCenterAI.Agent-0.4.0-preview.1-win-x64.zip`,
  33.366.409 bytes, runtime incluido **8.0.32**, 192 archivos declarados y manifiesto.
  SHA-256: `fba1908af11d49b40de3065b51f70aa7987bf48a5c1c49106393e9f91e930dc1`.
  El helper público aprobó **1.599 comprobaciones**, nueve procesos de diagnóstico
  y cuatro alteraciones rechazadas. Los artefactos están excluidos de Git; la
  entrega preliminar se conservó en `artifacts/agent/previous/<hash>`.
- Prueba manual posterior realizada por el usuario en la misma PC: `doctor`
  mostró los requisitos básicos disponibles y PostgreSQL recibió un nuevo
  análisis de `0.4.0-preview.1` el 2026-10-22. Resultado: 75/100 puntos,
  cobertura 70 %, siete correctas, cero para revisar, tres desconocidas y
  cero no aplicables. Confirma el recorrido portátil/análisis/envío/almacenamiento
  en este entorno; no valida otra PC ni convierte los desconocidos en fallos.
- [Distribución y pruebas supervisadas](docs/AGENT_PRIVATE_BETA.md),
  [comandos del agente](docs/WINDOWS_AGENT.md) y ADR-0017. La prueba en otra PC
  Windows 11 x64, una VM limpia sin SDK, otras ediciones/arquitecturas y la beta
  externa continúan pendientes; no se enviaron invitaciones ni se publicaron servicios.

## Sin publicar — guías locales y feedback privado, 2026-10-22

- Ajustado el espacio interior de las tarjetas de introducción, comprobaciones
  y feedback: 24 px en escritorio y 20 px en móvil. Comprobado en Chromium a
  360 y 1440 px, sin desbordamiento horizontal, con contenido representativo.
- Elección explícita de guías locales sin proveedor de IA externo. Plantillas
  versión 1 para las diez reglas y cuatro estados, con referencias oficiales de
  Microsoft, explicación de alcance y revisión manual. No se recalculan los
  puntajes ni se interpretan evidencias como instrucciones. Fuentes o versiones
  incompatibles reciben una explicación limitada, sin inventar diagnósticos.
- Nueva vista **Entender este análisis** desde el historial del dispositivo;
  conserva evidencia, puntos, cobertura y estados originales. Referencias HTTPS
  restringidas a documentación de Microsoft y texto escapado por React.
- Feedback privado editable: útil, poco claro o incorrecto, comentario opcional
  de 1000 caracteres, versión de guía y fechas UTC. Autorización Bearer exclusiva
  y propiedad conjunta cuenta/dispositivo/análisis; se conserva el historial de
  dispositivos revocados. Sin publicación ni listado entre cuentas.
- Migración `20261022123515_AnalysisExplanationFeedback`: tabla nueva con
  columnas en español, claves foráneas e índice único usuario/análisis. Reintento
  del primer envío concurrente, sin historial de ediciones. Copia de seguridad
  local antes de aplicar la migración; no modifica las tablas de datos anteriores.
- El middleware acota en memoria los cuerpos según los metadatos de cada ruta:
  64 KiB para informes y 8 KiB para feedback, también sin longitud declarada.
  Se corrigió el 500 detectado en Kestrel para devolver 413 al exceder el límite.
- Validación: **96/96 pruebas del backend**, **127/127 del agente** y **29/29
  unitarias web**. Tipos y compilación de producción de Next.js correctos.
  **33/33 E2E aprobados** con PostgreSQL y Mailpit reales: recorrido móvil,
  evidencia sin ejecutar HTML, feedback persistente/editable, aislamiento entre
  cuentas, rechazo de Device, concurrencia y límites de entrada.
- Después de la limpieza de las cuentas E2E, las huellas de las filas anteriores
  coincidieron con el respaldo de verificación: un usuario, un dispositivo,
  dos análisis y veinte comprobaciones conservados. Sin feedback de prueba residual.
- [Uso, privacidad, referencias y prueba local supervisada](docs/LOCAL_GUIDES_AND_FEEDBACK.md).
  La distribución del agente y los controles para una beta externa siguen pendientes.

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
