# Verificación integrada local de la primera versión

Fecha: **2026-10-24**. Es una comprobación de la implementación local, no una
release pública ni una validación en otro equipo Windows.

## Recorrido automatizado

`e2e/release-journey.spec.ts` crea una cuenta ficticia `e2e-<UUID>@example.invalid`
y recorre la aplicación mediante Chromium, PostgreSQL y Mailpit locales:

1. Landing, registro web, Primeros pasos y verificación del correo de prueba.
2. Descarga del ZIP existente y comparación del SHA-256 mostrado por la web.
3. Alta de equipo y vinculación mediante el protocolo del agente.
4. Dos informes sintéticos: uno parcial con revisión/desconocido y otro con
   criterios informados correctos. Consulta de progreso, guías y prioridades.
5. Valoración privada y exportación individual del análisis con su feedback.
6. Historial por equipo y comparación de ambos resultados.
7. Borrado de un análisis y rechazo 410 al reenviar el mismo reporte.
8. Detector de mensajes con consentimiento y cuota persistente.
9. Exportación de cuenta: informe restante, marca mínima del borrado y contador,
   sin contenido del mensaje revisado.
10. Eliminación confirmada de la cuenta ficticia, retiro de cookies y rechazo
    del acceso del agente. Fixtures retiran cuenta/correo de prueba al terminar.

La vinculación y los informes son una simulación del protocolo: este E2E no
ejecuta el binario, no escanea la PC ni lee el estado/credencial/informes del
agente personal. La descarga no se extrae o ejecuta. Las pruebas .NET del agente
se ejecutan por separado. Otra PC/VM sigue pospuesta por decisión del usuario.

El caso respeta Retry-After acotado si el límite HTTP frena una ingesta. Usa
esperas explícitas de navegación y actualización tras cambios externos del
agente simulado; no modifica límites de la API o requisitos de autorización.
La confirmación de borrado de cuenta verifica la navegación y cualquier aviso
visible antes de comprobar la revocación.

La tarjeta del equipo abre el historial mediante una navegación completa. El
caso comprueba ambos accesos: título del equipo y «Ver equipo e historial».
Así consulta datos actuales después de informes recibidos fuera del navegador.

## Repetir la comprobación

Con API, web, PostgreSQL y Mailpit ya activos en loopback, desde la carpeta web:

```powershell
npx.cmd playwright test e2e/information.spec.ts e2e/landing.spec.ts e2e/release-journey.spec.ts
```

Requiere un ZIP válido configurado para Primeros pasos. Usa cuentas ficticias en
la base de desarrollo y conserva los datos personales; no ejecutar contra una
instalación pública. Los helpers rechazan limpieza fuera del espacio E2E.
No ejecutar dos suites E2E a la vez contra el mismo entorno de desarrollo.
Separar build/typegen y reinicios de las corridas de navegador; no modificar
el entorno mientras Playwright está recorriendo la aplicación.

La verificación de backend/agente, las tres suites PostgreSQL aisladas y las
unidades/tipos/build web se describen en [VERIFICATION.md](VERIFICATION.md).
Los resultados de esta corrida se registran en [CHANGELOG.md](../CHANGELOG.md).
No confundir este recorrido con repetir toda la suite E2E histórica o CI remoto.

En el cierre del 2026-10-24 aprobaron juntos los seis casos del comando anterior,
96 unidades web, tipos/build, 251 pruebas habituales del backend, 145 del agente
y las 23 PostgreSQL separadas. La revisión automática rechazó iniciar una web
temporal de producción en 3011 (`blocked by policy`); su API temporal se detuvo.
Los E2E finales se realizaron contra desarrollo. El build de producción aprobado
no se presenta como una comprobación de ejecución de producción de esta corrida.

## Estado para continuar

Los bloques funcionales locales de la 1.0 están incorporados, incluidas páginas
informativas en borrador y preparación local de errores. La recepción pública
de reportes, las políticas finales y la publicación siguen pendientes.

Antes de publicar deben definirse responsable, país y correo, revisar el texto y
acordar hosting/dominio/TLS, correo externo, conservación, copias/recuperación,
secretos/permisos y monitoreo. No se dan por completados mediante estas pruebas.
El [checklist 1.0](RELEASE_1_0.md) y el [inventario](PENDING_FEATURES.md) conservan
las condiciones abiertas. El usuario solicitó guardar este corte en un commit
local; no se crea un tag, release o despliegue, ni se envía al remoto.
