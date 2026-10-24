# Funciones y controles pendientes

Revisado: 2026-10-24. Inventario consolidado del [roadmap](../ROADMAP.md),
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

| Área | Pendiente |
| --- | --- |
| Copias | Programación, conservación, rotación, copia fuera del equipo y gestión de contraseñas/claves. El backup manual cifrado y el ensayo aislado se incorporan en este bloque; no hay restauración sobre la base activa. |
| Recuperación operativa | Procedimiento inicial documentado. Quedan por acordar RPO/RTO, responsables y custodia; implementar recuperación real, invalidar accesos restaurados y reconciliar borrados posteriores a la copia antes de reabrir. No se ejecutó recuperación sobre la base activa. [Preparación](RECOVERY_RUNBOOK.md). |
| Auditoría | Ya existe actividad privada de catorce acciones completadas, con filtros de acción/días UTC, navegación hacia eventos anteriores y exportación completa. Pendientes intentos fallidos y otras operaciones, revisión operativa global, conservación, rol escritor separado y protección ante SQL/administradores o base comprometida. [Alcance](SECURITY_ACTIVITY.md). |
| Monitoreo | Ya existen logs mínimos de API, correlación por solicitud y `/health/ready` con consulta real de PostgreSQL. Pendientes métricas durables, otros componentes, alertas y respuesta a incidentes. `/health` conserva liveness independiente. [Alcance](OPERATIONAL_MONITORING.md). |
| Conservación | Acordar plazos de contenido, logs, correo y copias; controles y responsables de borrado. Los plazos iniciales de autenticación no son una política de producción aprobada. |

## Cuenta, sesiones y privacidad

| Área | Pendiente |
| --- | --- |
| Perfil | Nombre y cambio de correo incorporados. Correo requiere dirección actual verificada, contraseña al solicitar/finalizar y confirmación de ambas direcciones. Pendientes MFA y validación externa. [Alcance](ACCOUNT_EMAIL_CHANGE.md). |
| Contraseña | Cambio autenticado, mínimo de 15, lista local inicial y aviso tras recuperación incorporados. Cambio/reset cancelan solicitudes de correo pendientes. Pendientes corpus amplio/actualizado, revisión integral y entrega durable. [Política](ACCOUNT_SECURITY_POLICY.md), [correo](ACCOUNT_EMAIL_CHANGE.md). |
| MFA | Segundo factor opcional, recuperación y revocación del factor. |
| Sesiones | Inventario, cierre individual y límite de diez sesiones habilitadas por cuenta incorporados, con ensayo de concurrencia en PostgreSQL. Pendientes monitoreo/alertas de reutilización y revisión integral para varias instancias/proxies. [Uso](ACCOUNT_SESSIONS.md). |
| Abuso | Revisión de enumeración/tiempos, proxies confiables y límites que funcionen con varias instancias; la IP de Next.js se comparte actualmente. |
| Correo | Reintentos SMTP acotados y caducidad de cola incorporados; continúa en memoria. Pendientes proveedor/dominio/remitente, cola durable, entrega, control de volumen, conservación y recuperación posterior a reinicio. [Límites](MAIL_DELIVERY.md). |
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
| Escritorio 1.3 | Icono de bandeja, interfaz local, notificaciones y actualizador seguro. El cliente actual es una consola manual portátil. |
| Familia 1.5 | Miembros, permisos, equipos compartidos y límites del panel familiar. |
| Monetización 2.0 | Planes, límites por plan, facturación y soporte prioritario después de validar estabilidad, uso y costos. |
| Exploración | Aplicación móvil acompañante, pequeños negocios y otras plataformas, sin compromiso actual de implementación. |
| IA externa | Pospuesta por elección del usuario. Se mantienen las guías locales. Reabrir proveedor, autorización, contexto, costos y retención solo si cambia esa decisión. |

## Publicación y calidad

| Área | Pendiente |
| --- | --- |
| Web pública | Landing y páginas informativas en borrador incorporadas. Faltan versiones finales de privacidad/términos, receptor de errores y proceso privado de vulnerabilidades con responsables/contacto definidos. El reporte local no envía tickets. |
| Despliegue | Hosting, dominio, TLS, hosts/CORS, secretos y rotación, permisos mínimos de PostgreSQL y cifrado del almacenamiento. |
| Operación de varias instancias | Estado compartido donde corresponda, correo durable, límites y concurrencia; el bloqueo entre purgas no resuelve por sí solo esa operación. |
| Validación continua | Helper local consolidado y workflow Windows de CI preparados; PostgreSQL/E2E opcionales en local. Workflow validado con actionlint, sin envío o ejecución remota todavía. Pendientes primera corrida alojada, dependencias, migraciones/capacidad y compatibilidad en otra PC. [Uso](VERIFICATION.md). |
| Métricas de producto | Definir finalidad y datos mínimos antes de medir activación, retorno o resolución; actualmente no hay analítica de uso. |

Ya están implementados registro/login, sesiones revocables y renovación,
verificación/recuperación, dispositivos, diez comprobaciones y envío manual,
puntos/cobertura, guías locales, feedback, comparación, exportaciones, borrado
individual/de cuenta, mantenimiento técnico opcional, backup cifrado con ensayo
aislado, actividad privada de seguridad, sesiones activas y monitoreo local de API/PostgreSQL.
La beta sigue incompleta.
