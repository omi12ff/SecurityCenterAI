# Alcance y cierre de la primera versión

Revisado: 2026-10-24. Objetivo acordado: completar el software de la **1.0 gratuita**
de la [guía maestra](Guia_Maestra_Security_Center_AI_Actualizada.pdf), página 7.
La versión 1.0 del PDF identifica el documento y no una release publicada.

El usuario pospuso explícitamente las pruebas en otra PC/VM: por ahora solo tiene
el equipo actual y no dispone de una VM. Esto no bloquea implementar el software;
la compatibilidad pendiente debe seguir visible antes de anunciar un lanzamiento.

## Recorrido y requisitos

| Requisito de la guía | Estado local | Falta para cerrar |
| --- | --- | --- |
| Landing y explicación transparente | Implementada para visitantes, con registro, recorrido, datos, FAQ y enlaces a privacidad/términos/ayuda. | Finalizar documentos/contacto y preparación de publicación. |
| Registro, login, verificación y recuperación | Implementado y probado. | Configurar/validar correo real y operación externa. |
| Dashboard con puntos y problemas principales | Puntos/cobertura, prioridades separadas por estado, enlaces a cada guía y actualización manual implementados. Recorrido integrado comprobado. | Revisión para publicación y compatibilidad externa. |
| Un dispositivo por cuenta | Implementado y recorrido integrado comprobado. | Conservar el límite en cambios futuros. |
| Agente Windows 10 y 11 | Consola portátil manual para Windows 11 x64 en vista previa. | Windows 10 y otras PC sin validar; no anunciar compatibilidad que no fue comprobada. |
| Entre 8 y 12 verificaciones | Diez reglas con resultados parciales y evidencia limitada. | Ampliar validación real cuando haya otro entorno disponible. |
| Análisis manual e historial básico | Implementado; consulta, explicación, comparación, exportación y borrado. Primeros pasos e historial compacto comprobados en el recorrido integrado con informes ficticios. | Compatibilidad real ampliada y revisión para publicación. |
| Detector de enlaces y mensajes, con límite diario | Implementado y probado localmente, con reglas y contador persistente por cuenta. | Revisión final antes de publicar; no garantiza reputación o ausencia de fraude. |
| Explicaciones y pasos en español | Guías locales implementadas. | Se mantiene la elección del usuario de no integrar IA externa. |
| Privacidad, términos y canal de errores | Páginas públicas en borrador y preparación local de reportes implementadas. | Responsable, país, contacto, revisión de políticas y canal real de recepción antes de publicar. |

## Recordatorio: datos para las páginas públicas

El 2026-10-24 el usuario confirmó que estos datos todavía no están definidos y
pidió recordárselos:

- [ ] Nombre del responsable del proyecto.
- [ ] País de operación.
- [ ] Correo público de contacto para consultas y reporte de errores.

Retomar este recordatorio al trabajar en privacidad, términos y reporte de errores,
y antes de publicar la aplicación. Hasta definirlos, esas páginas quedan como
borradores locales; no completar los datos con valores inventados. Mientras tanto
se puede continuar con los demás bloques de software de la 1.0.

## Orden de trabajo actual

1. Detector básico de enlaces/mensajes con confirmación, límites diarios del backend,
   manejo de errores y datos mínimos. [Contrato y alcance](LINK_MESSAGE_CHECKS.md).
2. Landing pública y recorrido de entrada implementados; conservar explicación de límites y estado de desarrollo.
3. Primeros pasos y acceso privado al ZIP incorporados. [Recorrido](GETTING_STARTED.md).
   Prioridades del dashboard e historial compacto incorporados. [Recorrido](DASHBOARD_PRIORITIES.md).
4. Borradores de privacidad/términos y preparación local de errores incorporados.
   [Datos y estado](PUBLIC_INFORMATION.md). Los tres datos públicos del recordatorio
   siguen pendientes; no inventar identidad jurídica, jurisdicción o correo.
5. Recorrido integrado local incorporado y comprobado con cuenta/informes ficticios.
   [Alcance de la validación](RELEASE_VALIDATION.md). Revisión final de cambios y
   preparación de publicación pendientes; no se crea una release automáticamente.

Las pruebas en otra PC quedan pospuestas, no aprobadas. Para publicación también
siguen pendientes hosting/dominio/TLS, secretos y permisos, correo externo,
conservación, recuperación y monitoreo según el [inventario](PENDING_FEATURES.md).
No se despliega, publica un paquete o crea una release con este cambio de foco.

## Fuera de esta primera versión

Automatización/alertas de 1.1, drivers/salud de 1.2, escritorio/bandeja de 1.3,
familia de 1.5 y monetización de 2.0 conservan sus etapas. La comparación manual
ya adelantada se conserva. Tampoco se reemplaza el antivirus o se ejecutan
correcciones arbitrarias en el equipo.

Evidencia real por bloque en el [changelog](../CHANGELOG.md); una fila implementada
localmente no equivale a validación pública, revisión de PR o soporte para todas
las versiones de Windows previstas.
