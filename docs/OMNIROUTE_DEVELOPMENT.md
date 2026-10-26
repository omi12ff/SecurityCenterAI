# OmniRoute como apoyo de desarrollo

Preparado el 2026-10-25; estado revisado el 2026-10-26 para esta PC. OmniRoute 3.8.52 está instalado por separado;
SecurityCenterAI mantiene sus reglas y guías locales. El usuario pidió configurar
el apoyo de desarrollo y explicar, sin implementar todavía, una integración de IA
dentro de la app.

Esa fue la petición inicial. Posteriormente, el 2026-10-25, el usuario autorizó
incorporar una explicación opcional por análisis. Su configuración y clave de
producto están separadas de este MCP de desarrollo. El usuario completó la
conexión OpenRouter SecurityAI y la clave de producto. El adaptador del producto
ya produjo una explicación válida con estados ficticios; el apoyo de desarrollo
continúa sin inferencia habilitada y usa una clave diferente.
[Guía del producto](AI_EXPLANATIONS.md).

## Estado comprobado

- Panel y base de OmniRoute disponibles en <http://127.0.0.1:20128/dashboard>.
- Escucha solo en loopback; datos en `%LOCALAPPDATA%\OmniRoute`, fuera de OneDrive.
- Acceso directo **OmniRoute** en Inicio. Arranque, parada y reinicio comprobados.
- 45 skills instaladas en Codex. Conexión MCP local `omniroute-development`
  registrada con tres herramientas: `omniroute_status`, `omniroute_models` y
  `omniroute_ask`. La primera consulta salud; las otras utilizan la API de inferencia.
- Clave local independiente `securitycenterai-development`, protegida con DPAPI
  CurrentUser. Sin scopes administrativos, con registro local de prompts desactivado
  (`noLog`) y con política de modelos restringida inicialmente vacía. No se
  modificaron sus permisos de modelo ni se habilitaron consultas de desarrollo
  al configurar la clave del producto.
- **Contraseña y configuración inicial completadas en esta PC:** la consulta
  pública `require-login` devuelve `requireLogin`, `hasPassword` y `setupComplete`
  en `true` el 2026-10-26. La gestión privada exige iniciar sesión y su estado no
  se deduce de este chequeo público.
- El usuario completó la conexión OpenRouter **SecurityAI** y la clave del
  producto: gestión desactivada, solo esa conexión, `noLog` activado y compresión
  de prompts desactivada. La allowlist y la app usan ahora la ruta explícita
  `openrouter/openrouter/free`.
- La consulta inicial a `/v1/models` anunció solo `openrouter/free`; una consulta
  real detectó que ese nombre es ambiguo en esta instalación. Con la ruta
  calificada, el adaptador devolvió un resumen válido y diez comprobaciones con
  estados ficticios. No se usó un informe personal. La configuración del
  producto fue habilitada por el usuario y la API se reinició con la corrección;
  readiness y web respondieron 200. El valor de fábrica sigue desactivado.

El MCP local expone únicamente esas tres operaciones; no importa el servidor MCP
administrativo de OmniRoute ni abre otro servidor. No lee archivos del proyecto.
Una consulta recibe solo el texto que el asistente le envía expresamente. El
modelo y URL no se pueden seleccionar mediante argumentos de la herramienta.
El límite inicial es 12.000 caracteres, 1.200 tokens de salida y 60 segundos, sin
reintentos. Las respuestas son opiniones del modelo: requieren revisión y pruebas.

El cliente resuelve SDK MCP y zod desde la instalación existente de OmniRoute.
Si se desinstala esa aplicación, la conexión de desarrollo deja de funcionar.
La configuración anterior de Codex tiene copia DPAPI fuera del repositorio;
se comprobó que solo se añadió la nueva entrada MCP, conservando las demás opciones.
La sesión actual debe volver a descubrir esa conexión para mostrar sus herramientas;
registrarla no cambia el modelo de la conversación.

## Completar la conexión gratuita

La conexión del producto ya está preparada como se describe arriba. Los pasos
siguientes corresponden a habilitar por separado el apoyo de desarrollo, si se
decide hacerlo; no se han aplicado sus permisos ni se ha habilitado su inferencia.

Recomendación inicial para probar: **OpenRouter Free**, por su catálogo de modelos
gratuitos. Su plan anuncia 50 solicitudes diarias; disponibilidad, políticas de
datos y cuotas pertenecen al proveedor y pueden cambiar. [Precio oficial](https://openrouter.ai/pricing/).
**Groq Free** es otra opción con límites por modelo; el cliente preparado ahora
solo permite identificadores gratuitos OpenRouter y se adaptaría al elegir otra
modalidad. [Límites oficiales de Groq](https://console.groq.com/docs/rate-limits).

1. **Ya completaste la contraseña y la configuración inicial en esta PC.** Abrí
   el panel e iniciá sesión para revisar la configuración privada. La contraseña
   y las claves se introducen en tu navegador o terminal privada, nunca en el chat.
2. Revisá si ya configuraste una cuenta OpenRouter; si falta, creala y generá su
   clave de API desde su propia web. Conservá el plan gratuito y elegí modelos
   gratuitos. Esa clave es del proveedor;
   es diferente de la clave local de desarrollo ya preparada.
3. En **Proveedores** de OmniRoute (`/dashboard/providers`), revisá tu conexión
   OpenRouter o agregala e ingresá allí su clave si falta. Probá una conexión/modelo,
   evitando pruebas masivas.
   Copiá el identificador completo del modelo que realmente aparece disponible.
4. En **API Manager** (`/dashboard/api-manager`), editá permisos de la clave
   `securitycenterai-development`. En **Models**, permití únicamente el modelo
   gratuito elegido; en **Connections**, limitá a tu conexión OpenRouter. Conservá
   acceso de gestión desactivado y evitá combos o fallback a otras conexiones.
   No uses presupuesto cero como bloqueo: en OmniRoute significa sin límite.
5. Activá el apoyo con el modelo copiado. Este es un ejemplo para el router
   gratuito, que debe estar disponible y permitido en tu instalación:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\OmniRoute\development\Enable-Development.ps1" -Model 'openrouter/openrouter/free'
   ```

   También admite un modelo explícito con sufijo `:free`. El helper comprueba
   su presencia en el catálogo accesible y cambia la configuración local; no
   hace una completion. La prueba de respuesta real todavía debe hacerse.
6. Volvé a abrir o actualizar la sesión de Codex si la conexión aún no aparece.
   Empezá con «Usá OmniRoute para responder únicamente OK» y comprobá el resultado.
   Después puede pedirse una segunda opinión sobre un fragmento de código o un
   diseño. No enviar `.env`, contraseñas, claves, informes personales o dumps.

Los identificadores permitidos por los helpers utilizan el nombre calificado
`openrouter/openrouter/free` o modelos explícitos
`openrouter/proveedor/modelo:free`, siempre según catálogo y permisos. El
2026-10-26 se corrigió el prefijo preliminar `or/`, que no corresponde al registro
instalado de OmniRoute 3.8.52. Se rechazan alias antiguos, `auto`, fallback,
modelos pagos y el nombre corto ambiguo `openrouter/free`. La solicitud
también declara `allow_fallbacks:false`; las
restricciones de modelos/conexiones del servidor son necesarias para acotar
decisiones globales del gateway. Una etiqueta de modelo no concede cuota ni
convierte una cuenta externa en gratuita.

## Comprobar y detener

```powershell
node "$env:LOCALAPPDATA\OmniRoute\development\smoke.mjs"
powershell -NoProfile -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\OmniRoute\Stop-OmniRoute.ps1"
```

El smoke comprueba protocolo MCP, herramientas, salud y bloqueo de inferencia
desactivada. Impide lecturas de la clave y peticiones privadas durante esa prueba.
No certifica la conexión al proveedor. La clave protegida, configuración, helpers
y copia de configuración Codex permanecen fuera del repositorio.

Tras corregir el alias el 2026-10-26, aprobaron 15 comprobaciones de modelos en
Node y el smoke MCP, con las operaciones privadas bloqueadas durante el ensayo.
El apoyo de desarrollo sigue con inferencia desactivada y no se modificaron los
permisos de la clave de desarrollo. No se realizó una completion.

## Explicación opcional en la app

El bloque posterior añade **Ampliar explicación con IA** para un análisis guardado,
solicitado con botón junto al aviso de datos y desactivado por defecto. La API .NET prepara
solo puntos/cobertura y regla/versión/estado con texto del catálogo local; no
envía evidencia libre, nombres, correo, ids de cuenta/equipo/informe, fechas,
feedback o archivos. Valida el JSON de las diez reglas antes de mostrar texto
en la web; acciones manuales y referencias permanecen en las guías locales.

La clave de producto debe ser diferente de `securitycenterai-development`, con
permisos acotados al modelo/conexión OpenRouter gratuitos elegidos y `noLog`.
No copia la configuración del MCP o la clave de Codex, ni permite elegir destino
desde el navegador. La aplicación no guarda respuestas o conversaciones y la
guía local queda siempre disponible. El usuario habilitó la configuración del
producto con su clave separada; la ruta corregida es `openrouter/openrouter/free`, con
la API ya reiniciada y habilitada para una prueba explícita desde la interfaz.
El adaptador real se comprobó con diez reglas y estados ficticios: devolvió una
explicación válida. No se usó un informe personal. El nombre corto
`openrouter/free` figuraba en el catálogo, pero la consulta real fue rechazada
por ambigua; la clave de producto y la app usan la ruta con proveedor explícito.
La disponibilidad del catálogo por sí sola no prueba que una consulta funcione.
[Activación, cuota y límites](AI_EXPLANATIONS.md).

Referencias de la integración: [MCP en Codex](https://developers.openai.com/codex/mcp/)
y [configuración oficial](https://developers.openai.com/codex/config-reference/).
