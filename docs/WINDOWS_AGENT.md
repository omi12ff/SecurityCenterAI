# Agente Windows: consola y paquete portátil

Revisión: **2026-10-22**. Versión del agente: **0.4.1-preview.1**.
Los resultados de compilación y pruebas de cada bloque
se registran en [CHANGELOG.md](../CHANGELOG.md).

Esta consola .NET 8 permite vincular la PC con el dispositivo creado en la web,
enviar un contacto manual y ejecutar diez comprobaciones locales de solo lectura.
`scan` conserva el informe local; `scan --send` o `send` permiten enviarlo
explícitamente para consultar puntos confirmados, cobertura y resultados en
la web. Hay un ZIP portátil autocontenido para Windows 11 x64; no hay
instalador, servicio, inicio automático, reparación ni IA.

## Ejecutar el paquete sin SDK

Para generar y usar el ZIP, consulta el
[recorrido de pruebas privadas](AGENT_PRIVATE_BETA.md). El destino no necesita
SDK ni runtime .NET instalado por separado. El runtime incluido conserva sus
archivos `DOTNET-LICENSE.txt` y `DOTNET-THIRD-PARTY-NOTICES.txt` en el paquete.
Extrae toda la carpeta y abre
una terminal normal en ella:

```powershell
.\SecurityCenterAI.Agent.exe version
.\SecurityCenterAI.Agent.exe doctor
.\SecurityCenterAI.Agent.exe scan
```

El informe permanece local. Para vincular el equipo y enviar después, con la
API ya activa en esta misma PC:

```powershell
.\SecurityCenterAI.Agent.exe pair --api http://localhost:5169
.\SecurityCenterAI.Agent.exe send
```

El código se solicita de forma oculta. `Iniciar-agente.cmd` muestra la ayuda y
también acepta estos comandos; invoca el ejecutable sin PowerShell ni SDK.
Si la política del equipo permite scripts, `Invoke-Agent.ps1 -Action menu`
ofrece un menú manual. No solicita elevación ni modifica políticas.

`version` y `--version` muestran la versión sin consultar el almacenamiento.
`doctor` y `doctor --json` comprueban requisitos mínimos locales: Windows con
build 22000 o posterior, sistema y proceso x64, Windows PowerShell del sistema y disponibilidad de
una ruta absoluta de LocalAppData. No hacen solicitudes, no escriben ni leen
credenciales o informes y no ejecutan las diez sondas. No verifican escritura,
módulos, permisos ni protección efectiva. El diagnóstico devuelve `0` si están
presentes los requisitos, `2` si falta alguno y `130` al cancelar; no es una
certificación de compatibilidad o seguridad.

La URL de loopback solo funciona si la API está en la PC que ejecuta el agente.
Otra PC requiere HTTPS con certificado válido y una infraestructura todavía
pendiente; el ZIP no publica el backend. La matriz de compatibilidad y los
pasos de actualización, retiro y pruebas consentidas están en
[AGENT_PRIVATE_BETA.md](AGENT_PRIVATE_BETA.md).

## Desarrollo desde el repositorio

Usa Windows y el SDK .NET 8 seleccionado por `global.json`, desde una terminal
interactiva en la raíz del repositorio. No hace falta instalar PowerShell 7.
Para vincular, enviar un contacto o subir resultados, la API debe estar activa; para comprobar
el sistema localmente no hace falta API ni cuenta vinculada.

1. Inicia el backend con el helper habitual en otra terminal:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\dev.ps1
   ```

2. Inicia sesión en `http://localhost:3000`, confirma el correo y abre
   **Dispositivos**. En el equipo que ya registraste, pulsa **Generar nuevo código**.
   El código es de un uso y vence en diez minutos; no lo incluyas en comandos,
   capturas o mensajes compartidos.

3. Vincula la consola desde la raíz del repositorio:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\agent.ps1 -Action pair
   ```

   La consola solicitará el código con entrada oculta. Pégalo y presiona Enter.
   El canje exitoso guarda la credencial y envía un primer contacto. No recibe
   la contraseña de tu cuenta. La URL predeterminada de la API es
   `http://localhost:5169`; si usas otra instancia, agrega `-ApiUrl`:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\agent.ps1 -Action pair -ApiUrl http://localhost:5179
   ```

4. Para actualizar el último contacto en otra ocasión, envía un heartbeat manual:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\agent.ps1 -Action heartbeat
   ```

   Actualiza **Mis dispositivos** en la web. Deberías ver el estado vinculado,
   la versión declarada del agente y el último contacto. El campo de plataforma
   puede seguir sin información hasta enviar un análisis; la ingesta lo marca
   como Windows por el protocolo recibido, sin atestación remota. Un contacto
   exitoso no indica que Windows sea seguro o que el agente permanezca online.

5. Ejecuta las comprobaciones locales:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\agent.ps1 -Action scan
   ```

   Se muestra un resumen y se guarda un informe JSON local con las diez reglas.
   No cambia la configuración del equipo ni envía los resultados al servidor.
   Si quieres guardarlo también en la web, ejecuta `-Action scan -Send` en vez
   de ese comando, o envía después el último informe con `-Action send`.
   Actualiza **Ver equipo e historial**. Consulta el [recorrido de envío y
   su interpretación](ANALYSIS_INGESTION.md).

`ExecutionPolicy Bypass` se limita a cada proceso iniciado por esos comandos;
el helper no cambia la política global ni solicita permisos de administrador.
Esta forma corresponde a los scripts de desarrollo del repositorio en un host
con política `Restricted`. El paquete portátil se ejecuta mediante `.exe` o
`.cmd`, sin cambiar esa política; su wrapper PowerShell es opcional y requiere
un entorno que ya permita ejecutarlo.

## Comandos y recuperación

| Acción del helper | Qué hace |
| --- | --- |
| `-Action pair` | Solicita un código oculto, lo canjea una vez y guarda la credencial protegida. |
| `-Action pair -Replace` | Permite sustituir una credencial local existente mediante un código nuevo. El canje exitoso revoca la credencial remota anterior de ese dispositivo. |
| `-Action heartbeat` | Envía un contacto a la API guardada junto con la credencial. No ejecuta `scan`. |
| `-Action status` | Lee metadatos locales sin mostrar el token. No consulta su vigencia remota. |
| `-Action scan` | Ejecuta las diez comprobaciones y guarda su informe local, incluso sin vinculación. |
| `-Action scan -Send` | Guarda el informe y solicita su envío explícito a la API vinculada. Requiere credencial vigente. |
| `-Action send` | Envía o reintenta el último informe del esquema 2 sin volver a comprobar Windows. |
| `-Action forget` | Borra la copia local de la credencial. No revoca acceso remoto ni elimina informes. |
| `-Action doctor` | Comprueba requisitos mínimos locales, sin red ni acceso a credenciales o informes. |
| `-Action version` | Muestra la versión del agente sin consultar el almacenamiento. |
| `-Action help` | Muestra ayuda de la consola. |

Si tu política permite ejecutar scripts, también puedes usar
`.\scripts\agent.ps1 -Action status`. La alternativa directa con el SDK es:

```powershell
dotnet run --project src/SecurityCenterAI.Agent -- pair --api http://localhost:5169
dotnet run --project src/SecurityCenterAI.Agent -- heartbeat
dotnet run --project src/SecurityCenterAI.Agent -- scan
dotnet run --project src/SecurityCenterAI.Agent -- scan --send
dotnet run --project src/SecurityCenterAI.Agent -- send
```

En la alternativa directa, sustituir una credencial existente utiliza
`pair --api http://localhost:5169 --replace`. El código siempre se escribe en
el prompt oculto; no existe un argumento para pasarlo en la línea de comandos.
`Replace` autoriza sustituir la copia local. El código determina qué dispositivo
se vincula: si pertenece a otro registro o API, no revoca por sí mismo la
credencial remota que estaba guardada antes; revoca ese acceso desde su web.

Para **revocar realmente el acceso**, utiliza **Revocar acceso** en la web.
`forget` solo elimina la copia de esta consola; otra copia podría seguir
autorizada. Si se revoca el registro completo, hay que crear otro dispositivo
antes de vincularlo. Si solo se restableció la contraseña o venció la credencial,
puedes generar un código nuevo para el mismo dispositivo y volver a vincularlo.
«Cerrar todas las sesiones» actúa únicamente sobre sesiones web.

Si el canje falla o se interrumpe después de llegar al servidor, genera otro
código y vuelve a intentarlo: puede haberse consumido aunque no se guardara
la respuesta local. No hay reintento automático de una operación de un uso.

Si eliminaste un resultado desde la web y `send` recibe `410`, el agente indica
que ese informe fue eliminado y pide un nuevo `scan`. Conserva la credencial
y el archivo local, sin reintentar automáticamente.
`scan --send` genera otro reporte y solicita explícitamente su envío; repetir
`send` del mismo reporte eliminado no lo restaura. [Control del historial](ANALYSIS_HISTORY.md).

## Diez comprobaciones y sus límites

Todas las reglas tienen versión **1**. Los estados son `Passed` (cumple el
criterio limitado de esa regla), `Attention` (requiere revisión), `Unknown`
(no se pudo determinar) y `NotApplicable` (plataforma explícitamente no soportada).
En JSON se serializan como `passed`, `attention`, `unknown` y `notApplicable`;
la consola los presenta como Correcto, Revisar, Desconocido y No aplica.
El informe local no calcula el puntaje. Al enviarlo, la API calcula puntos
confirmados y cobertura con su catálogo; un resultado favorable no certifica
seguridad. Los pesos y casos parciales están en [la guía de ingesta](ANALYSIS_INGESTION.md#cómo-leer-los-resultados).

La severidad de la tabla se utiliza cuando el estado es `Attention`. Los otros
estados llevan `informational`; un `Unknown` sigue siendo desconocido y requiere
considerar su causa. La falta de permisos, módulo o datos no se interpreta como
aprobación. Una comprobación que falla no descarta las demás.

| ID de regla | Consulta y criterio para `Passed` | Evidencia retenida y límite | Severidad con `Attention` |
| --- | --- | --- | --- |
| `WIN-DEFENDER-ANTIVIRUS` | `Get-MpComputerStatus`: antivirus y servicio antimalware habilitados. | Estado combinado; no evalúa antivirus de terceros. Defender puede estar desactivado por otro producto o una política. | `high` |
| `WIN-DEFENDER-REALTIME` | `Get-MpComputerStatus`: protección en tiempo real habilitada. | Estado de Defender; no demuestra el estado de otra solución antivirus. | `high` |
| `WIN-DEFENDER-SIGNATURES` | Fecha de actualización local de firmas con antigüedad de hasta siete días. | Antigüedad en días y umbral; no consulta cuál es la última firma disponible. Una fecha inválida o futura incoherente es desconocida. | `medium` |
| `WIN-FIREWALL-PROFILES` | `Get-NetFirewallProfile -PolicyStore ActiveStore`: Dominio, Privado y Público habilitados. | Estado de esos tres perfiles efectivos; no examina las reglas individuales ni puertos expuestos. | `high` |
| `WIN-SECURE-BOOT` | `Confirm-SecureBootUEFI` devuelve verdadero. | Estado de arranque seguro; un BIOS/no soporte explícito es no aplicable. Sin permisos es desconocido. | `medium` |
| `WIN-TPM-READY` | `Get-Tpm`: TPM presente y preparado. | Presencia y preparación; no recoge fabricante, serie, claves ni autorización del propietario. No comprueba una versión mínima de TPM. | `medium` |
| `WIN-BITLOCKER-SYSTEM` | `Get-BitLockerVolume` del volumen del sistema: protección activa, cifrado completo y 100 %. | Estado de protección y porcentaje; no guarda unidades, protectores, identificadores o claves de recuperación. Suspensión o transición requiere revisión. | `medium` |
| `WIN-UPDATE-SERVICE` | `Win32_Service` de `wuauserv`: modo de inicio distinto de Disabled. | Modo y estado del servicio; Manual o Stopped pueden ser normales. **No comprueba parches faltantes ni que Windows esté actualizado.** | `medium` |
| `WIN-UAC-ENABLED` | Lectura de registro `EnableLUA=1`. | Ese valor de configuración; no evalúa nivel de avisos ni si un cambio espera reinicio. | `medium` |
| `WIN-SMB1-SERVER` | `Get-SmbServerConfiguration`: `EnableSMB1Protocol=false`. | Estado del componente servidor; no evalúa el cliente SMB1, características opcionales o exposición en la red. | `high` |

Son consultas fijas de solo lectura. La consola no ejecuta instrucciones de la
API ni del usuario como comandos PowerShell, y no activa/desactiva protecciones.
No se conservan la salida bruta ni los mensajes de error de los proveedores:
se producen evidencias seleccionadas y mensajes de causa acotados.

## Permisos y compatibilidad

Ejecuta primero desde una terminal normal. Cada regla informa si no pudo obtener
datos; no es necesario elevar toda la consola para obtener los demás resultados.
La POC no eleva permisos automáticamente. Los módulos disponibles y las políticas
dependen de la edición de Windows y de la administración del equipo.

Microsoft especifica que `Confirm-SecureBootUEFI` requiere PowerShell como
administrador; en una terminal normal el resultado puede ser `Unknown`.
BitLocker y otros proveedores también pueden denegar lectura o estar ausentes.
Un error de consulta no permite deducir si la función está habilitada.
La consola no modifica BIOS, TPM, cifrado, antivirus, firewall ni el servicio
Windows Update para resolver esas lecturas.

## Credencial e informes locales

El almacenamiento fijo es `%LOCALAPPDATA%\SecurityCenterAI\Agent`, fuera de la
carpeta del proyecto y su OneDrive:

| Archivo o carpeta | Contenido y tratamiento |
| --- | --- |
| `credential.enc` | Credencial opaca, destino de API, identificador y vencimiento, protegidos mediante DPAPI `CurrentUser`. No se imprimen código o token. |
| `reports\scan-{UTC}-{aleatorio}.json` | Informe local en texto claro con esquema 2, UUID estable, versiones, fechas y diez resultados, evidencia y recomendaciones. Se genera al solicitar `scan`; el envío es una acción explícita. |

DPAPI asocia la protección al usuario Windows; procesos que ejecutan bajo ese
mismo contexto pueden descifrarla. No protege contra malware que ya controla la
sesión y no es cifrado transportable para cambiar de usuario. El secreto se
necesita brevemente en memoria para autenticar las solicitudes.

Los informes no incluyen correo, usuario, hostname, inventario de aplicaciones,
historial, archivos, seriales ni claves de recuperación. Aun así revelan estados
de seguridad: revísalos antes de compartirlos. No hay subida automática, purga
o borrado seguro de esos archivos. `forget` no los elimina; puedes borrarlos
desde el Explorador cuando ya no los necesites. La ruta mostrada por Windows
puede contener tu nombre de perfil y conviene ocultarla en capturas.

Actualizar el paquete significa extraer una versión nueva en una carpeta
separada, después de verificar su hash, y ejecutar allí `version` y `doctor`.
En la misma PC y cuenta Windows se utiliza el estado existente de LocalAppData;
no copies credenciales o informes a la carpeta del programa ni a otro usuario.
Eliminar la carpeta del programa no revoca acceso ni borra esos datos.

La API admite una URL raíz HTTPS sin usuario, ruta adicional, consulta o
fragmento. HTTP solo se permite con `localhost`, `127.0.0.1` o `[::1]` exactos.
El cliente no sigue redirecciones ni omite la validación de certificados;
así evita reenviar el secreto a un destino redirigido. Usa el destino guardado
en el canje para los contactos y envíos posteriores. El informe no puede elegir
otra API ni un dispositivo o usuario de destino. El servidor los obtiene de la
credencial. Los informes del esquema 1 siguen siendo locales; vuelve a ejecutar
`scan` para obtener un reporte compatible con la ingesta.

Las consultas del sistema tienen hasta 12 segundos cada una; una que vence
produce estado desconocido y permite continuar con las demás. La respuesta
de vinculación está limitada a 16 KiB. Los timeouts HTTP y de lectura de cuerpo
se aplican por etapa, no como un SLA de duración total de `pair`.

## Comprobar el agente

Desde Windows y la raíz del repositorio:

```powershell
dotnet test tests/SecurityCenterAI.Agent.Tests --configuration AgentQa
```

La configuración separada evita compartir binarios con una API Debug activa.
La suite usa respuestas y consultas controladas, DPAPI del usuario Windows y
la API de pruebas con EF InMemory; no necesita Docker o un canje sobre tu equipo
personal. Las pruebas DPAPI requieren Windows y no se presenta esa suite como
validada en Linux. Un `scan` manual es una comprobación adicional del sistema real y sus
permisos: no sustituye las pruebas ni hace que un dato desconocido sea favorable.
Los resultados concretos están en [CHANGELOG.md](../CHANGELOG.md).

## Fuentes técnicas

Los criterios locales son decisiones de esta POC, no un estándar de cumplimiento
ni una certificación Microsoft. Se contrastaron los campos y las restricciones
con estas fuentes oficiales:

- [DPAPI: alcance CurrentUser](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.dataprotectionscope).
- [Get-MpComputerStatus](https://learn.microsoft.com/en-us/powershell/module/defender/get-mpcomputerstatus).
- [Perfiles efectivos del firewall y ActiveStore](https://learn.microsoft.com/en-us/powershell/module/netsecurity/get-netfirewallprofile).
- [Confirm-SecureBootUEFI: soporte y permiso de administrador](https://learn.microsoft.com/en-us/powershell/module/secureboot/confirm-securebootuefi).
- [Get-Tpm y sus campos](https://learn.microsoft.com/en-us/powershell/module/trustedplatformmodule/get-tpm).
- [Get-BitLockerVolume y estados de protección/cifrado](https://learn.microsoft.com/en-us/powershell/module/bitlocker/get-bitlockervolume).
- [Win32_Service: StartMode y State](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-service).
- [Configuración de UAC](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/user-account-control/settings-and-configuration) y [valor EnableLUA](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-gpsb/958053ae-5397-4f96-977f-b7700ee461ec).
- [Get-SmbServerConfiguration](https://learn.microsoft.com/en-us/powershell/module/smbshare/get-smbserverconfiguration) y [alcance de SMB1](https://learn.microsoft.com/en-us/windows-server/storage/file-server/troubleshoot/detect-enable-and-disable-smbv1-v2-v3).

El recorrido manual de [ingesta y puntos confirmados](ANALYSIS_INGESTION.md)
conecta estas comprobaciones con la web. El paquete portátil prepara pruebas
privadas supervisadas; la distribución externa firmada, automatización,
compatibilidad ampliada e infraestructura externa siguen pendientes en
[ROADMAP.md](../ROADMAP.md). Se eligieron guías locales sin IA externa.
