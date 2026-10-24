# Primeros pasos y acceso al agente desde la web

Implementado localmente el 2026-10-24 para el cierre funcional de la 1.0.
No es una publicación externa ni una nueva versión del agente.

## Recorrido para el usuario

Entrá a **Resumen → Primeros pasos**, o a `/getting-started` con sesión iniciada.
El dashboard vacío también ofrece **Empezar con mi equipo**, y Dispositivos enlaza
la misma guía. Registro y login conservan su destino habitual en el dashboard.

La página consulta el perfil actual, los equipos propios y el detalle del equipo
no revocado. Indica correo verificado, registro, acceso vigente y recepción de un
análisis del agente de ese equipo. Descargar un archivo no cuenta como análisis;
`doctor`, `status` y `heartbeat` tampoco. El progreso no es un inventario de lo
instalado en la PC ni un indicador de conexión en tiempo real.

La guía permite descargar el ZIP, comparar SHA-256, extraerlo, ejecutar `doctor`,
registrar el equipo, pegar un código de un uso en la entrada oculta del agente,
ejecutar `scan` y decidir si usar `send`. Los botones copian comandos de texto;
no ejecutan programas desde el navegador. No incluyen códigos o credenciales.
Al terminar, **Actualizar progreso** lleva al análisis y sus guías. El historial
de un equipo revocado se conserva, pero no completa la preparación de otro equipo.

La vista previa se orienta a Windows 11 x64. El ZIP incluye .NET y no requiere
SDK, Docker o Node.js en el equipo que solo ejecuta el agente. Firma de editor,
compatibilidad externa y distribución pública siguen pendientes. Si Windows
bloquea el ejecutable, no se indica desactivar protecciones para continuar.

## Configuración del servidor Next.js

Copiá las variables necesarias de `src/SecurityCenterAI.Web/.env.example` a
`.env.local` dentro de esa carpeta. No deben usar el prefijo `NEXT_PUBLIC_`.

```dotenv
AGENT_PACKAGE_PATH=../../artifacts/agent/SecurityCenterAI.Agent-0.4.1-preview.1-win-x64.zip
AGENT_API_URL=http://localhost:5169
```

- `AGENT_PACKAGE_PATH` selecciona **un único ZIP** preparado con
  `scripts/publish-agent.ps1`. Puede ser absoluta o relativa al directorio de
  trabajo de Next.js. Requiere el archivo contiguo `.zip.sha256` generado por el
  empaquetador. No se buscan versiones automáticamente ni se publican carpetas.
- `AGENT_API_URL` es la dirección raíz accesible **desde el agente**. Solo admite
  HTTPS o HTTP en localhost, 127.0.0.1 y [::1], sin credenciales, rutas, query o
  fragmento. No se deduce de `API_BASE_URL`, que puede ser interna del servidor.
  En desarrollo, si falta la variable, se ofrece localhost:5169; en producción,
  una dirección ausente o inválida deja el paso pendiente con un mensaje.
- localhost identifica la PC del agente. La web y la API se mantienen en sus
  terminales y el agente se usa en otra; no se detienen para ejecutar los comandos.
- En desarrollo local se seleccionó el ZIP existente 0.4.1-preview.1 mediante
  `.env.local`, excluido de Git. No se cambió el `.env` de PostgreSQL ni se generó
  otro binario. Reiniciá Next.js si cambiás la configuración y no se recarga.

Si faltan configuración, ZIP, checksum o permisos de lectura, o si falla la
integridad, la guía informa que el paquete no está disponible. La cuenta y el
registro del equipo pueden prepararse mientras tanto. No se muestra una descarga
ficticia ni rutas internas del servidor.

## Descarga y límites

`GET /getting-started/agent/download` exige sesión y consulta `/api/auth/me` para
comprobar revocación y correo verificado. Las cookies decodificadas no autorizan
la descarga por sí solas. Una sesión vencida vuelve por el flujo de renovación a
Primeros pasos; el usuario debe solicitar de nuevo la descarga.

El navegador no elige archivos: no hay parámetro de ruta o versión, y las queries
se rechazan. El nombre debe seguir el formato cerrado del agente win-x64. Se
rechazan entradas de archivo que sean enlaces simbólicos, tamaños mayores de
128 MiB, checksum mayor de 512 bytes, nombre distinto en el checksum, cabecera
no ZIP, cambios durante la comprobación o SHA-256 diferente. La suma se calcula
por bloques y la respuesta usa el mismo descriptor, desde el byte cero, con
cierre al terminar o cancelar. No extrae el ZIP ni valida su manifiesto interno;
esa comprobación corresponde al helper de empaquetado existente.

La respuesta es un adjunto ZIP con nombre validado, tamaño y cabeceras
`private, no-store`, `no-referrer` y `nosniff`. No se agregan tablas, migraciones,
contadores de preparación, analítica o eventos de auditoría. La descarga queda
bajo custodia del usuario; informes y credencial mantienen el alcance de
[la guía del agente](WINDOWS_AGENT.md).

El ZIP y su checksum deben residir en una carpeta controlada por quien opera el
servidor y mantenerse sin modificaciones. El hash no autentica al editor ni
protege frente a un operador que reemplace ambos archivos. No se agrega firma,
actualizador, almacenamiento de paquetes en base de datos, CDN o rate limiting
específico de descargas. Antes de una distribución externa deben resolverse los
controles de publicación del [checklist 1.0](RELEASE_1_0.md).

## Verificación

Pruebas unitarias comprueban integridad, límites, nombres, archivos faltantes,
stream completo, cancelación y rechazo de direcciones/comandos no admitidos.
E2E con cuentas ficticias comprueba autorización, progreso real, aislamiento,
vinculación sin análisis, envío sintético, revocación, descarga y SHA-256, rechazo
de rutas y móvil. No lee o envía un informe personal ni ejecuta el agente Windows.
La evidencia y las suites ejecutadas están en [CHANGELOG.md](../CHANGELOG.md).

La prueba `getting-started-unavailable.spec.ts` se omite salvo que
`E2E_AGENT_PACKAGE_UNAVAILABLE=1`. Requiere un servidor QA separado con paquete
ausente y `AGENT_API_URL` inválida, apuntando `E2E_BASE_URL` a ese servidor. No
hay que modificar o retirar el paquete de la web habitual para ejecutar el ensayo.

Referencias de implementación: [Route Handlers de Next.js](https://nextjs.org/docs/app/getting-started/route-handlers)
y [streams de archivos de Node.js 24](https://nodejs.org/docs/latest-v24.x/api/fs.html#filehandlereadablewebstreamoptions).
