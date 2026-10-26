# Funciones y controles pendientes

Revisado: 2026-10-26. Inventario consolidado del [roadmap](../ROADMAP.md),
[seguridad](../SECURITY.md), [privacidad](../PRIVACY_NOTES.md) y arquitectura.
No son fechas de entrega ni compromisos de implementar todo antes de la beta.

## Foco actual: primera versión 1.0 gratuita

El usuario pospuso otra PC/VM y pidió cerrar la primera versión de la guía.
Landing, detector básico, Primeros pasos, prioridades del dashboard e historial
compacto están incorporados localmente, junto con borradores de privacidad/términos
y preparación local de reportes de errores. El recorrido integrado utiliza cuenta
y agente ficticios. Siguen responsable/país/contacto, documentos finales, canal
real de recepción y preparación de publicación. [Checklist 1.0](RELEASE_1_0.md).
Las pruebas de compatibilidad siguen pendientes y no se anuncian como completadas.

**Recordatorio solicitado por el usuario:** responsable, país de operación y
correo público aún sin definir. Retomarlo al preparar las páginas de políticas
y reporte de errores, y antes de publicar; conservar borradores locales hasta
completar los datos. [Detalle y casillas pendientes](RELEASE_1_0.md).

## Preparación operativa para publicación

**Diseño pendiente de revisión con el usuario:** se investigaron tres alternativas
en [Propuestas de diseño](DESIGN_PROPOSALS.md). No aplicar el rediseño, instalar
bibliotecas visuales ni extender componentes antes de esa revisión.

**Soporte de plataforma:** la PC usa SDK 8.0.426/runtime 8.0.32; Microsoft publica
8.0.33 como parche vigente y fija el fin de soporte de .NET 8 en **2026-11-10**.
Preparar actualización del runtime de API y del runtime incluido en el ZIP, y
migración a .NET 10 LTS antes de un lanzamiento posterior a esa fecha. Actualizar
el runtime del sistema no actualiza por sí solo un agente autocontenido.
[Política oficial de .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

| Área | Pendiente |
| --- | --- |
| Copias | Programación, conservación, rotación, copia fuera del equipo y gestión de contraseñas/claves. Backup manual cifrado y ensayos aislados implementados; no hay restauración sobre la base activa. |
| Recuperación operativa | Invalidación transaccional de accesos y conservación de contenido incorporadas solo al ensayo temporal. Quedan RPO/RTO, responsables/custodia, destino y recuperación real, secretos y conciliación de borrados posteriores antes de reabrir. [Ensayo](RECOVERY_REHEARSAL.md), [preparación](RECOVERY_RUNBOOK.md). |
| Auditoría | Ya existe actividad privada de catorce acciones completadas, con filtros de acción/días UTC, navegación hacia eventos anteriores y exportación completa. Pendientes intentos fallidos y otras operaciones, revisión operativa global, conservación, rol escritor separado y protección ante SQL/administradores o base comprometida. [Alcance](SECURITY_ACTIVITY.md). |
| Monitoreo | Logs mínimos, correlación y readiness PostgreSQL incorporados. Diagnóstico local de cola con contadores/edad, avisos y JSON de solo lectura. Pendientes métricas durables, alertas externas, capacidad y respuesta a incidentes. `/health` conserva liveness independiente. [Alcance](OPERATIONAL_MONITORING.md), [cola](EMAIL_QUEUE_DIAGNOSTICS.md). |
| Conservación | Acordar plazos de contenido, logs, correo y copias; controles y responsables de borrado. Los plazos iniciales de autenticación no son una política de producción aprobada. |

## Cuenta, sesiones y privacidad

| Área | Pendiente |
| --- | --- |
| Perfil | Nombre y cambio de correo incorporados. Correo requiere dirección actual verificada, contraseña al solicitar/finalizar y confirmación de ambas direcciones. Pendientes MFA y validación externa. [Alcance](ACCOUNT_EMAIL_CHANGE.md). |
| Contraseña | Cambio autenticado, mínimo de 15, lista local inicial y aviso tras recuperación incorporados. Cambio/reset cancelan solicitudes de correo pendientes. Avisos en cola cifrada persistente. Pendientes corpus amplio/actualizado, revisión integral y entrega externa. [Política](ACCOUNT_SECURITY_POLICY.md), [correo](ACCOUNT_EMAIL_CHANGE.md). |
| MFA | Segundo factor opcional, recuperación y revocación del factor. |
| Sesiones | Inventario, cierre individual y límite de diez sesiones habilitadas por cuenta incorporados, con ensayo de concurrencia en PostgreSQL. Pendientes monitoreo/alertas de reutilización y revisión integral para varias instancias/proxies. [Uso](ACCOUNT_SESSIONS.md). |
| Abuso | Revisión de enumeración/tiempos, proxies confiables y límites que funcionen con varias instancias; la IP de Next.js se comparte actualmente. |
| Correo | Cola cifrada persistente, reintentos/reservas tras reinicio y revalidación de enlaces incorporados. Pendientes proveedor/dominio/remitente, claves para el alojamiento elegido, entrega externa, capacidad, alertas y conservación. Protección actual Windows/DPAPI. [Límites](MAIL_DELIVERY.md). |
| Recopilación | Preferencias y avisos por módulo: finalidad, datos mínimos, envío y borrado; revisar cada nueva categoría antes de incorporarla. |
| Escala | Capacidad de exportaciones grandes, tiempos, memoria y acceso a posibles descargas temporales antes de una operación externa. |

## Agente y funciones del producto

| Etapa | Pendiente |
| --- | --- |
| Beta 0.5 | Prueba en otra PC/VM pospuesta explícitamente por el usuario. Faltan compatibilidad sin SDK/runtime y ampliar permisos, ediciones, hardware y entornos; se retomará cuando disponga de otro entorno. |
| Agente | Firma de binarios, instalador/desinstalación, distribución/actualización seguras, custodia de informes locales y respuesta ante credenciales comprometidas. |
| Lanzamiento 1.0 | Bloques locales, borradores y recorrido integrado incorporados. Faltan datos públicos, políticas finales, canal real de recepción y preparación de publicación. [Validación](RELEASE_VALIDATION.md), [checklist](RELEASE_1_0.md). |
| Automatización 1.1 | Programación de análisis, alertas y verificación de soluciones. La comparación manual de informes ya existe. Las acciones ejecutables requieren alcance y autorización propios. |
| Drivers y salud 1.2 | Errores PnP, drivers faltantes, firmas, versiones y recomendaciones basadas en fuentes oficiales. No hay inventario actual de hardware, drivers o aplicaciones. |
| Escritorio 1.3 | Icono de bandeja, interfaz local, notificaciones y actualizador seguro. La visión incluye vigilancia y autonomía con alcance y aprobaciones por plan/operación; todavía no están implementadas. El cliente actual es una consola manual portátil. |
| Familia 1.5 | Miembros, permisos, equipos compartidos y límites del panel familiar. |
| Monetización 2.0 | Planes, límites por plan, facturación y soporte prioritario después de validar estabilidad, uso y costos. |
| Exploración | Aplicación móvil acompañante, pequeños negocios y otras plataformas, sin compromiso actual de implementación. |
| IA explicativa opcional | El usuario autorizó el 2026-10-25 añadir explicación por análisis, sin chatbot. Valor de fábrica desactivado; configuración local habilitada por el usuario con clave de producto y `openrouter/openrouter/free`, restringida a SecurityAI y a esa ruta explícita. API reiniciada y habilitada para una prueba explícita desde la interfaz. Contexto reducido, botón explícito sin casilla y alternativa local incorporados. Prueba real con diez estados ficticios aprobada; quedan revisión de retención/costos y más casos de funcionamiento. Límites en memoria de una sola API; no se envió un informe personal al proveedor. [Alcance](AI_EXPLANATIONS.md). |

## Publicación y calidad

| Área | Pendiente |
| --- | --- |
| Web pública | Landing y páginas informativas en borrador incorporadas. Faltan versiones finales de privacidad/términos, receptor de errores y proceso privado de vulnerabilidades con responsables/contacto definidos. El reporte local no envía tickets. |
| Despliegue | Hosting, dominio, TLS, hosts/CORS, secretos y rotación, permisos mínimos de PostgreSQL y cifrado del almacenamiento. |
| Operación de varias instancias | Reservas de correo concurrentes probadas en PostgreSQL. Pendientes claves accesibles/protegidas para las identidades del alojamiento, límites, alertas y revisión integral; una cola compartida no resuelve por sí sola esa operación. |
| Validación continua | Helper local consolidado y workflow Windows de CI preparados; PostgreSQL/E2E opcionales en local. Workflow validado con actionlint, sin envío o ejecución remota todavía. Pendientes primera corrida alojada, dependencias, migraciones/capacidad y compatibilidad en otra PC. [Uso](VERIFICATION.md). |
| Métricas de producto | Definir finalidad y datos mínimos antes de medir activación, retorno o resolución; actualmente no hay analítica de uso. |

Ya están implementados registro/login, sesiones revocables y renovación,
verificación/recuperación, dispositivos, diez comprobaciones y envío manual,
puntos/cobertura, guías locales, feedback, comparación, exportaciones, borrado
individual/de cuenta, mantenimiento técnico opcional, backup cifrado con ensayo
aislado, actividad privada de seguridad, sesiones activas y monitoreo local de API/PostgreSQL.
La beta sigue incompleta.
