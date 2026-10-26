# Explicación opcional de IA por análisis

Revisado: 2026-10-26. El usuario autorizó añadir una explicación de los resultados
guardados, con solicitud explícita por consulta. La decisión amplía las guías locales
iniciales y queda registrada en [ADR-0041](../DECISIONS.md#adr-0041--explicación-opcional-por-análisis-con-contexto-de-catálogo-reducido).
La función se entrega **desactivada por defecto**. El 2026-10-26 el usuario
habilitó su configuración local y preparó una clave de producto para la conexión
OpenRouter **SecurityAI**. La prueba real detectó que `openrouter/free`, aunque
aparece en el catálogo, produce HTTP 400 por ambigüedad de proveedor. La clave
y la app usan ahora **`openrouter/openrouter/free`**: el primer segmento fija
OpenRouter y el resto es el identificador nativo de su router gratuito.
Una consulta sintética con el adaptador .NET devolvió un resumen válido y las
diez comprobaciones; no se usó el informe personal. Esta prueba no evalúa la
precisión de todos los modelos que el router gratuito puede elegir.

El usuario informó que la clave de producto tiene gestión desactivada, acceso
solo a SecurityAI y a `openrouter/openrouter/free`, `noLog` activado y compresión de prompts
desactivada. La credencial y el apoyo de desarrollo siguen separados; el MCP
de desarrollo permanece sin inferencia habilitada.

## Uso

1. Abrí **Dispositivos → Ver equipo e historial → Entender este análisis**.
2. Conservá la guía local como referencia: explica evidencia, revisión manual,
   límites y documentación de cada comprobación.
3. Si la instalación habilitó la función, revisá el aviso de datos y pulsá
   **Ampliar explicación con IA**. El usuario pidió retirar la casilla: el clic
   solicita el envío de esa consulta; abrir la página no llama al modelo.
4. Leé la respuesta como ayuda que puede equivocarse. Para revisar una
   comprobación, usá sus acciones manuales y referencias de la guía local.

Se necesita sesión activa de la cuenta dueña y correo verificado. Una credencial
del agente no permite consultar esta función. No hay entrada de conversación,
preguntas libres o un chatbot. No cambia el informe, puntos, cobertura o estado
de Windows; tampoco ejecuta comandos o correcciones. Un estado desconocido sigue
sin determinar y una observación favorable no demuestra seguridad completa.

Desactivación, fallo de red/proveedor, cuota o respuesta incompatible mantienen
disponible la guía local. La IA no es un requisito para consultar el análisis,
su feedback, comparación, exportación o borrado.

El bloque inicial admite informes `windows-agent` de esquema 2, catálogo y cálculo
versión 1, con las diez reglas reconocidas. Un informe antiguo o incompatible
conserva su guía local sin convertirse en un diagnóstico nuevo.

## Datos de una consulta

La API construye el contexto; el navegador no aporta evidencia, prompt, destino
o modelo. Solo incluye:

| Dato | Fuente |
| --- | --- |
| Puntos confirmados y cobertura | Cálculo determinista guardado del análisis. |
| Identificador, versión y estado de las diez reglas | Resultados reconocidos por la API. |
| Títulos, descripciones y límites | Catálogo/guías locales del servidor. |

No incluye evidencia libre del agente, nombres, correo, identificadores de
usuario/dispositivo/informe, fechas, feedback, archivos o credenciales. Los
identificadores de reglas son claves del catálogo, no identificadores personales.
Las rutas privadas de la API sí identifican el dispositivo y análisis para
autorizar el acceso; esos UUID no se copian al contexto enviado a OmniRoute.

El recorrido eventual es navegador → servidor Next.js → API .NET → OmniRoute →
proveedor configurado. Aunque OmniRoute escuche en loopback, el proveedor puede
recibir el contexto fuera del equipo. Antes de habilitarlo deben revisarse
destinatario, ubicación, conservación, permisos, cuotas y condiciones. `noLog`
en una clave local no garantiza ausencia de conservación externa.

## Respuesta y conservación

La API exige un JSON de campos cerrados y tamaños limitados, con resumen y las
diez reglas conocidas sin duplicados. Una respuesta inválida se rechaza y se
usa la alternativa local. Cumplir el esquema no garantiza que el texto sea
correcto. La web presenta texto escapado; las acciones manuales y referencias
provienen de la guía local, no de enlaces o comandos generados por el modelo.

El resumen admite hasta 2000 caracteres y cada explicación hasta 1600; el JSON
del modelo se limita a 24.000 caracteres y la respuesta HTTP a 64 KiB. No se
aceptan llamadas a herramientas. Los filtros de texto y la comprobación de
incertidumbre de estados desconocidos son controles limitados, no una evaluación
completa de corrección o de todas las instrucciones que podría inventar un modelo.

La aplicación no persiste prompts, respuestas o historial de conversaciones de
este módulo. La respuesta queda solo en memoria de la página, sin almacenarse
en cookies, `localStorage`, PostgreSQL, feedback, exportaciones o backups. La
valoración existente sigue evaluando la guía local. No se crea un consentimiento
persistente o permiso general para consultas posteriores.

La cuota utiliza temporalmente el identificador del propietario en memoria de la
API; no lo envía al proveedor. Reiniciar la API restablece sus contadores. Los
logs del producto no incluyen prompts, respuestas, claves o cuerpos del proveedor.
Terminal, gateway y proveedor tienen controles y conservación separados.

## Contrato y límites

Ambas rutas verifican la sesión, el correo y la pertenencia conjunta del
dispositivo/análisis. Un recurso ajeno o inexistente recibe la misma respuesta.
Los dispositivos revocados conservan su historial propio. Las respuestas privadas
no deben almacenarse en caché.

| Ruta | Comportamiento |
| --- | --- |
| `GET /api/devices/{deviceId}/analyses/{analysisId}/ai-explanation` | Informa disponibilidad/configuración y aviso de datos; no llama al proveedor ni devuelve un historial. |
| `POST /api/devices/{deviceId}/analyses/{analysisId}/ai-explanation` | Recibe solo `{ "confirmed": true }`, comprueba límites y solicita una explicación o devuelve la alternativa local. |

Por defecto hay **cinco intentos por propietario y día UTC**. Los intentos admitidos
pueden consumir cuota aunque el proveedor falle o su respuesta no sirva. No hay
reintentos automáticos del proveedor. La concurrencia es de **cuatro solicitudes
globales y una por propietario**, sin cola de trabajos. Estos controles viven en
memoria de una sola API, se restablecen al reiniciar y no coordinan varias
instancias: no constituyen el control de capacidad/abuso de un despliegue público.
Las cuotas y disponibilidad del proveedor son adicionales e independientes.

## Configuración del servidor

El bloque `AiExplanation` se entrega desactivado:

```json
{
  "AiExplanation": {
    "Enabled": false,
    "GatewayBaseUrl": "http://127.0.0.1:20128/v1/",
    "Model": "",
    "ApiKey": "",
    "ApiKeyFile": "",
    "TimeoutSeconds": 30,
    "DailyLimit": 5
  }
}
```

El modelo de esta instalación es **`openrouter/openrouter/free`**, con proveedor
explícito. La variante corta del catálogo `openrouter/free` es ambigua en esta
instalación y se rechaza al configurar la app. El bloque también admite modelos
gratuitos explícitos con formato `openrouter/proveedor/modelo:free`, sujetos a
los permisos del gateway. La clave actual solo permite el router gratuito con
proveedor explícito; aceptar un formato en la API no concede acceso a otro
modelo. La etiqueta gratuita no garantiza cuota, disponibilidad o
precio futuro. El navegador no puede cambiar modelo/destino. No habilitar combos
o fallback hacia otras conexiones: acotar también modelo y conexión en OmniRoute.

**Corrección de alias del 2026-10-26:** los ejemplos preliminares usaban el prefijo
`or/`. El registro de esta instalación utiliza `openrouter`; `or/openrouter/free`
no coincide con su catálogo y el bloque corregido lo rechaza, al igual que otros
alias `or/`, modelos pagos y rutas `auto`. Los ADR conservan la decisión y el
contexto histórico. La lectura del catálogo resultó insuficiente: la posterior
prueba real comprobó la ruta calificada y su respuesta estructurada.

El gateway debe usar HTTPS, salvo HTTP en `localhost`, `127.0.0.1` o `::1`.
Su ruta base es `/v1/`, sin credenciales en URL, parámetros o fragmentos. No se
siguen redirecciones. El tiempo predeterminado es 30 segundos, configurable entre
5 y 60; el límite diario admite 1–50 intentos. La solicitud de modelo limita
la salida a 4500 tokens y no habilita streaming o herramientas. Se solicita
`json_schema` estricto con los campos esperados, diez entradas e identificadores
del catálogo, y `require_parameters: true` para exigir compatibilidad. La API
valida además tamaño, reglas únicas, texto e incertidumbre antes de mostrarlo.
El soporte de esquema varía por endpoint; puede no haber un modelo gratuito
compatible disponible en ese momento. [Salidas estructuradas de OpenRouter](https://openrouter.ai/docs/guides/features/structured-outputs).

En esta PC el límite local se amplió a **60 segundos** durante la corrección del
estado de espera; el valor de fábrica sigue siendo 30. La prueba real con siete
estados correctos y tres desconocidos ficticios produjo diez explicaciones
válidas y un resumen de 426 caracteres. También hubo errores transitorios y un
timeout; una respuesta correcta no garantiza disponibilidad continua del router.

Usá una **clave de producto independiente** de `securitycenterai-development`,
sin permisos administrativos y con `noLog`. La clave del proveedor pertenece a
la configuración privada de OmniRoute; la API usa la clave local del gateway.
No pegues secretos en el chat, código, argumentos de comandos o archivos del
repositorio. La web no recibe ninguna de estas claves.

La configuración local del producto y su clave protegida se mantienen bajo
`%LOCALAPPDATA%\SecurityCenterAI\Api\AiExplanation`, fuera del repositorio y
OneDrive, en `settings.json` y `gateway-key.dpapi`. La API carga ese JSON al
arrancar, fuera de `Testing`; las variables de entorno posteriores pueden
sobrescribirlo. No se recarga automáticamente: reiniciá la API después de cambiar
la configuración. La clave se protege con DPAPI CurrentUser para la identidad Windows
que ejecuta la API; otro usuario o alojamiento necesita revisar la custodia.
Un proceso con acceso a esa misma sesión Windows puede descifrarla. La exclusión
de Git o DPAPI no sustituyen los permisos y revocación de OmniRoute.

El [MCP de apoyo al desarrollo](OMNIROUTE_DEVELOPMENT.md) tiene configuración y
credencial separadas. Configurarlo no habilita por sí solo consultas del producto.
La activación del producto tampoco cambia el modelo de la conversación de Codex.

### Asistente local de configuración

Desde la raíz del repositorio, consultá el estado sin mostrar secretos:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\configure-ai.ps1 -Action status
```

`status` es la acción predeterminada. En esta PC ya completaste la contraseña,
la configuración inicial de OmniRoute, la conexión SecurityAI y la clave de
producto. Conservá su acceso solo al modelo `openrouter/openrouter/free` y a esa conexión,
sin gestión o fallback, con `noLog` activado y compresión de prompts desactivada.
Si ya hay una clave de
producto protegida preparada para esta instalación, el helper la reutiliza.
Si falta, solicita la clave local de OmniRoute con entrada oculta; nunca se pide
la clave del proveedor en esta consola.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\configure-ai.ps1 -Action configure -Model 'openrouter/openrouter/free' -DailyLimit 5 -TimeoutSeconds 30
```

`configure` guarda `Enabled: true` en el JSON local; tendrá efecto al reiniciar
la API. No comprueba que el modelo o su conexión estén disponibles.

También admite un identificador explícito `openrouter/proveedor/modelo:free` disponible
y permitido en tu gateway. `-ReplaceKey` reemplaza la clave protegida y vuelve
a pedirla con entrada oculta. El parámetro de scripting `ApiKey` recibe un
`SecureString`; no pasar claves en texto claro por argumentos. El helper fija
el gateway local `http://127.0.0.1:20128/v1/`; cambiar el destino requiere revisar
la configuración del servidor.

Para desactivar futuras consultas del producto:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\configure-ai.ps1 -Action disable
```

`disable` guarda `Enabled: false` y conserva la clave protegida y los demás
ajustes. Revocar esa clave en OmniRoute es una operación separada.

Después de configurar o desactivar, reiniciá la API de desarrollo para cargar
el JSON actualizado:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev.ps1 -Restart -Configuration Debug
```

La configuración local no ejecuta una consulta al modelo ni acredita conexión,
disponibilidad o condiciones del proveedor. Reabrí la guía del análisis para ver
el estado cargado. No cambies a un modelo pago o a otra conexión para resolver
un error sin revisar nuevamente alcance y condiciones.

El helper se probó el 2026-10-26 con una clave ficticia y directorio temporal,
incluyendo configuración, reemplazo/reutilización DPAPI, desactivación repetida
y permisos de usuario/SYSTEM. La aplicación de permisos conserva los metadatos
del directorio y permite repetir la operación. No se usó una clave de producto
real ni se consultó al proveedor en esa prueba.

## Alcance pendiente

La ruta explícita ya produjo un resumen y diez explicaciones válidas mediante
el adaptador .NET con estados ficticios. La pantalla se comprobó con pruebas de
navegador aisladas. La revisión del informe propio se solicita con el botón;
no se utilizó ese informe en la prueba de conectividad. Deben revisarse las
condiciones de datos, conservación y cuotas del proveedor antes de enviarlo.
No se ejecuta una llamada real como parte de la configuración local. Para publicar
faltan además controles de producción, límites compartidos y revisión de las
[políticas todavía en borrador](PUBLIC_INFORMATION.md). Responsable, país y correo
público siguen sin definir; se mantiene el [recordatorio](RELEASE_1_0.md).

La prueba en otra PC/VM y la beta externa siguen pospuestas. La visión posterior
de escritorio incluye bandeja y vigilancia/autonomía acotadas por plan, alcance
y aprobaciones explícitas. No están implementadas con este módulo, ni se obtiene
permiso para ejecutar acciones por haber solicitado una explicación.

Resultados concretos de validación en [CHANGELOG.md](../CHANGELOG.md).

## Qué muestra la pantalla

- Al pulsar el botón: solo la palabra **«Pensando»** con una pulsación suave en
  lugar del botón, sin iconos o puntos. La guía local sigue disponible. La
  animación se desactiva con movimiento reducido o colores forzados del sistema.
- Al terminar: «Explicación ampliada con IA», resumen y diez explicaciones. Cada
  entrada conserva título/estado original y enlaza a su comprobación local.
- Si falla: «No se generó una explicación con IA», motivo seguro (conexión,
  permisos/modelo, cuota, presupuesto, timeout o formato) y opción de reintento.
  Nunca se presenta la guía local como si fuera el resultado del modelo.
  Durante un reintento desaparece el error anterior hasta recibir la respuesta.

La API devuelve `failureCode` de una lista fija cuando puede clasificar el fallo;
no propaga cuerpos, claves o detalles internos del gateway. El contrato POST
conserva `confirmed: true` como solicitud explícita; la web lo emite solo desde
su botón, sin casilla y sin consultas automáticas al abrir la página.

Si se rechaza una salida, el servidor registra únicamente una categoría fija de
validación (estructura, cantidad/identidad de reglas, contenido o incertidumbre).
No registra respuestas del modelo, prompts, claves o identificadores del informe,
y esa categoría interna no se añade al resultado mostrado en el navegador.

Cuando falla el transporte o el gateway devuelve un HTTP no satisfactorio, se
registra también `AI explanation unavailable`, con una categoría fija y el
código HTTP si existe. No se lee ni registra el cuerpo de error del proveedor,
su mensaje de excepción, claves, prompts o datos del informe. Un timeout o una
conexión fallida carecen de código HTTP. Estos registros distinguen fallos de
conexión, permisos, cuota e indisponibilidad sin mostrar detalles internos al
cliente.

Una respuesta HTTP 200 de la API puede contener `method: local-guide` si la IA
falló: confirma que se entregó la alternativa local, no que el modelo generó
una explicación. Para diagnosticar, revisá `status`/`failureCode` y el evento
anterior. La disponibilidad del gateway tampoco garantiza que un proveedor
pueda atender una consulta en ese momento. OpenRouter documenta los errores y
el filtrado de parámetros en su [guía de errores](https://github.com/OpenRouterTeam/docs/blob/main/api_reference/errors-and-debugging.mdx)
y [selección de proveedores](https://github.com/OpenRouterTeam/docs/blob/main/guides/routing/provider-selection.mdx).

Revisión del 2026-10-26: web, API/readiness y gateway respondieron 200; una
consulta explícita con diez observaciones ficticias devolvió `generated`, diez
reglas válidas y un resumen de 218 caracteres. No se utilizó un informe personal.
El fallo mostrado en la captura del usuario no se reprodujo en esa consulta;
no se puede determinar retrospectivamente su código HTTP porque no se
registraba. Se conservan modelo gratuito, tiempo máximo, cuotas y validaciones.
