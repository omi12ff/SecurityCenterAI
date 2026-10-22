# Roadmap

Actualizado: 2026-10-21.

La planificación sigue la [Guía Maestra de Security Center AI](docs/Guia_Maestra_Security_Center_AI_Actualizada.pdf), documento 1.0
del 2026-07-27. Su número de versión identifica el documento; el producto
todavía no tiene un lanzamiento público 1.0. Las versiones de esta página son
etapas previstas y no implican releases ni fechas de entrega confirmadas.

## Estado actual: base 0.1, web y cuentas hacia 0.5

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
| Pruebas del backend | 82/82 aprobadas el 2026-10-21 con EF InMemory, reloj controlable y correo de prueba; incluye ingesta, límites y puntaje. Las carreras e índices se comprobaron además con PostgreSQL real. |
| Frontend Next.js | Registro, login, dashboard, cuenta y formularios de verificación y recuperación en español. |
| Sesión web | Cookies `HttpOnly` de access y refresh, renovación rotatoria, logout y cierre global con revocación en la API. |
| Correo local | SMTP de pruebas con Mailpit; configuración autenticada y TLS exigidos fuera de desarrollo. |
| Pruebas de la web | 26/26 unitarias, tipos, build y 31/31 E2E aprobados con PostgreSQL y SMTP/Mailpit reales. |
| Dispositivos y base de resultados | Vinculación, credencial propia, revocación e historial de resultados por dispositivo, con autorización separada de cuenta y cliente. |
| Agente Windows y verificaciones locales | Prueba de concepto de consola con diez comprobaciones, credencial DPAPI, heartbeat y envío explícito con `scan -Send` o `send`. Sin servicio ni instalador. |
| Pruebas del agente | 127/127 aprobadas en Windows: DPAPI, consultas controladas, envío, lectura de informes e integración con API/EF InMemory. Las diez sondas se comprobaron manualmente en el bloque previo. |
| Ingesta y puntos confirmados | Esquema 2, identificador de reporte idempotente por dispositivo, validación de diez reglas, catálogo del servidor, cobertura y resultados parciales; evidencia de pruebas en el changelog. |
| IA | Pendiente. Sin proveedor, prompts, remediación o decisiones generadas por un modelo. |

El dashboard consulta análisis almacenados. El agente puede comprobar diez
aspectos de Windows y producir un informe local. Al solicitar el envío, la API
guarda el análisis y diez resultados, calcula puntos confirmados y cobertura y
la web permite consultarlos. El resultado es limitado y autodeclarado por el
cliente: no constituye una certificación de la PC. Las
pruebas automatizadas del backend usan EF Core InMemory; la suite E2E de la web
utiliza PostgreSQL real por separado.

## Etapas del producto

| Versión | Objetivo | Alcance previsto | Estado |
| --- | --- | --- | --- |
| 0.1 | Base interna | Backend, PostgreSQL, autenticación, Swagger y dashboard básico. | Base local verificada y documentación organizada; continúa la evolución interna. |
| 0.5 | Beta privada | Frontend, dispositivos, hallazgos, puntaje y primeras verificaciones. | Recorrido manual agente/API/web con diez reglas, puntos confirmados y cobertura. Pendientes distribución/operación del agente, explicaciones y preparación de la beta con usuarios. |
| 1.0 | Lanzamiento público gratuito | Web pública, agente Windows, historial y detector básico de enlaces y mensajes. | Pendiente. |
| 1.1 | Automatización | Análisis programados, alertas, comparación de resultados y verificación de soluciones. | Pendiente. |
| 1.2 | Drivers y salud | Errores PnP, drivers faltantes, firmas, versiones y recomendaciones desde fuentes oficiales. | Pendiente. |
| 1.3 | Escritorio | Icono de bandeja, interfaz local, notificaciones y actualizador seguro. | Pendiente. |
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
3. **Siguiente bloque:** incorporar explicaciones sobre hallazgos con evidencia
   y preparar una beta
   privada con feedback. El proveedor de IA y sus límites requieren una
   decisión antes de integrarlo.
4. Antes de un despliegue externo, resolver los controles de cuentas pendientes:
   política de contraseñas, MFA, correo durable, retención, auditoría y límites
   con proxies o varias instancias. El recorrido básico local ya está comprobado.

## Criterios para la beta y el lanzamiento

Para considerar logrado el primer análisis real:

- El agente se vincula y puede revocarse sin almacenar la contraseña del usuario.
- Una verificación fallida permite conservar los resultados parciales útiles.
- Cada hallazgo contiene evidencia, severidad, recomendación y regla aplicada.
- El dashboard muestra el puntaje y los resultados del dispositivo correcto.
- El cálculo y la validación de resultados tienen pruebas automatizadas.
- Se documentan los datos recopilados y no se envían archivos personales fuera
  del alcance aprobado.

Para el lanzamiento público gratuito 1.0 también faltan landing, operación de
cuentas y correo preparada para producción, un dispositivo por cuenta, análisis
manual, historial básico, detector de enlaces y mensajes con límites diarios,
explicaciones en español, privacidad, términos y canal de reporte de errores.
La puesta en marcha local actual no equivale a una validación para producción.

Las etapas posteriores deben mantener evidencia verificable, permisos mínimos
y privacidad. Los drivers se recomiendan desde fuentes oficiales, y los planes
pagos se incorporan después de validar uso recurrente, estabilidad y costos.
Cada avance debe reflejarse en [CHANGELOG.md](CHANGELOG.md), con las pruebas
realizadas y las limitaciones pendientes.
