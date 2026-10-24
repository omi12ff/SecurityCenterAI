# Agente portátil: preparación de pruebas privadas

Revisión: **22 de octubre de 2026**.

La versión actual del paquete es **0.4.1-preview.1**, para pruebas manuales y supervisadas.
No publica una beta externa ni incorpora alojamiento, instalador, servicio,
inicio automático o actualización automática. La API y la web siguen siendo
servicios independientes del agente.

La web incorpora **Primeros pasos** y una descarga privada del ZIP seleccionado
por el servidor, sin crear una release externa. [Recorrido y configuración](GETTING_STARTED.md).

## Preparar el paquete

Quien genera el paquete necesita Windows, PowerShell 5.1 o superior y el SDK
.NET 8 fijado por `global.json`. Desde la raíz del repositorio:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-agent.ps1
```

En el anfitrión de desarrollo la política de scripts es `Restricted`. Este
comando permite ejecutar el helper del repositorio en ese proceso de PowerShell;
no modifica la política persistente. La misma forma se usa para el helper de
validación del paquete. Es una instrucción de compilación y comprobación del
repositorio: en el destino portátil los lanzadores `.exe` y `.cmd` funcionan
directamente sin un script de lanzamiento ni cambios de política. El wrapper `.ps1` del
paquete es opcional y se utiliza solo donde la política ya permita scripts.

El destino predeterminado es `artifacts\agent`; se puede elegir otro con
`-OutputDirectory`. La salida incluye:

- `SecurityCenterAI.Agent-<version>-win-x64.zip`, con una carpeta interna del
  mismo nombre.
- `SecurityCenterAI.Agent-<version>-win-x64.zip.sha256`, con el hash del ZIP.

El script rechaza sobrescribir una entrega existente. Para volver a compilar
la misma versión, elige otra carpeta de salida y conserva el hash de cada prueba.

Para comprobar la entrega desde el repositorio, indica el ZIP generado:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-agent-package.ps1 -ArchivePath '.\artifacts\agent\SecurityCenterAI.Agent-0.4.1-preview.1-win-x64.zip'
```

El helper comprueba estructura e integridad, ejercita comandos de diagnóstico
y prueba el rechazo de paquetes alterados. No ejecuta un `scan`, una vinculación
ni un envío. Usa la política limitada al proceso indicada arriba; no cambia la
configuración de los equipos donde se ejecutará el paquete.

Dentro están el ejecutable, sus dependencias y runtime .NET 8, los lanzadores
`Iniciar-agente.cmd` e `Invoke-Agent.ps1`, la guía offline `LEEME.txt`,
`DOTNET-LICENSE.txt`, `DOTNET-THIRD-PARTY-NOTICES.txt` y
`package-manifest.json`. Los dos archivos `DOTNET-*` conservan la licencia y
los avisos de terceros del runtime distribuido. El manifiesto registra versión, destino `win-x64`,
publicación autocontenida y hashes SHA-256 de los archivos que enumera; no
incluye un hash de sí mismo. Distribuye la carpeta completa, no solo el `.exe`.
No se incluyen `.env`, credenciales ni informes de la PC que compila.

En el destino no hace falta instalar el SDK o un runtime .NET por separado:
la publicación autocontenida lleva el runtime. Esto no sustituye los
componentes de Windows ni demuestra compatibilidad con cualquier edición.
La versión del runtime distribuido se actualiza generando un paquete nuevo.
[Publicación autocontenida de .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/).

Antes de entregar un paquete, registra su versión, revisión de Git, fecha y
SHA-256. Revisa los cambios locales: la revisión de Git por sí sola no identifica
el contenido de un árbol con modificaciones sin commit.

## Recibir y ejecutar en la misma PC

1. Obtén el ZIP y su hash esperado de quien preparó la prueba. Calcula el hash
   antes de extraerlo:

   ```powershell
   Get-FileHash -LiteralPath '.\SecurityCenterAI.Agent-<version>-win-x64.zip' -Algorithm SHA256
   ```

   Sustituye `<version>` por el nombre recibido y compara el valor con el
   SHA-256 confirmado por el responsable. Un hash comprueba integridad respecto
   de ese valor; no acredita al editor si el ZIP y el hash proceden de una
   fuente comprometida. [Get-FileHash](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/get-filehash).

2. Extrae toda la carpeta a una ubicación del usuario, fuera de la carpeta
   comprimida. No requiere permisos de administrador.
3. Abre `Iniciar-agente.cmd` para ver la ayuda, o abre una terminal normal en la
   carpeta extraída y ejecuta:

   ```powershell
   .\SecurityCenterAI.Agent.exe version
   .\SecurityCenterAI.Agent.exe doctor
   .\SecurityCenterAI.Agent.exe scan
   ```

   `version` también acepta `--version`. `doctor` revisa requisitos mínimos del
   entorno local; `scan` guarda un informe local y no lo envía.

El `.cmd` abre el ejecutable directamente: sin argumentos muestra ayuda y espera
una tecla; con argumentos acepta comandos como `Iniciar-agente.cmd scan` o
`Iniciar-agente.cmd scan --send`. Si tu política permite scripts PowerShell,
`Invoke-Agent.ps1 -Action menu` ofrece un menú que solo ejecuta la acción elegida.
Sin `-Action`, ese script muestra ayuda; también acepta `-Action doctor` y
`-Action scan-send`. No cambies políticas para ejecutar estos lanzadores: si los
scripts están restringidos, usa el `.exe` o `.cmd` según las reglas del equipo.

`doctor` comprueba Windows con build 22000 o posterior, sistema y proceso x64,
presencia de Windows PowerShell del sistema y disponibilidad de una ruta
absoluta de LocalAppData. El objetivo de pruebas sigue siendo Windows 11 x64:
el número de build no valida una edición concreta. No se conecta,
no escribe archivos, no lee ni descifra credenciales o informes y no ejecuta las
diez comprobaciones. Tampoco comprueba acceso de escritura, módulos, permisos,
API ni protección efectiva. `doctor --json` produce la misma información en JSON,
sin mostrar la ruta del usuario. Devuelve código `0` si los requisitos mínimos
están presentes, `2` si alguno falta y `130` si se cancela. `version` no accede al
almacenamiento y devuelve `0`. Estos códigos no son un puntaje de seguridad.

Los binarios de esta etapa no tienen firma de editor ni reputación de descarga
establecida. Windows o la organización pueden advertir o bloquear su ejecución.
Detén la prueba y registra el mensaje para revisión con el responsable; no
desactives Defender, SmartScreen ni las políticas del equipo para continuar.
SmartScreen utiliza, entre otros factores, reputación del archivo y su firma.
[Microsoft Defender SmartScreen](https://learn.microsoft.com/en-us/windows/security/operating-system-security/virus-and-threat-protection/microsoft-defender-smartscreen/).

## Vincular y enviar de forma explícita

Para usar la API local, deja el backend y la web en sus terminales habituales.
Abre **Dispositivos**, confirma el correo si corresponde y genera un código
para el equipo. En otra terminal, desde la carpeta extraída:

```powershell
.\SecurityCenterAI.Agent.exe pair --api http://localhost:5169
.\SecurityCenterAI.Agent.exe heartbeat
.\SecurityCenterAI.Agent.exe scan
```

Pega el código únicamente en el prompt oculto de `pair`; no se pasa como argumento.
Revisa el informe local. Si decides guardarlo en la web, ejecuta:

```powershell
.\SecurityCenterAI.Agent.exe send
```

La alternativa `scan --send` comprueba y envía en una misma acción explícita.
`send` usa el último informe compatible y conserva su identificador: reintentar
el mismo contenido no duplica el análisis. No modifica un análisis anterior.
La API rechaza informes fuera de su ventana temporal; en ese caso realiza un
`scan` nuevo, sin editar las fechas del JSON.

Si el propietario eliminó ese resultado en la web, el mismo reporte recibe
`410`. El agente `0.4.1-preview.1` pide un nuevo `scan`, conservando su credencial
y archivo local, sin reintento automático. `send` del informe eliminado no lo
restaura. El control de exportación y borrado individual, su marca mínima y los
límites para copias y backups están en [ANALYSIS_HISTORY.md](ANALYSIS_HISTORY.md).

Consulta **Ver equipo e historial → Entender este análisis** para revisar
evidencia, guías locales y feedback privado. Un estado desconocido conserva su
incertidumbre; no significa protección desactivada ni aprobación.

`localhost`, `127.0.0.1` y `[::1]` se refieren a la PC que ejecuta el agente.
En otra PC no alcanzan el backend de tu equipo de desarrollo. El agente solo
admite HTTP en esos destinos de loopback; para otra máquina requiere una URL
raíz HTTPS y un certificado válido. La infraestructura externa, correo real,
operación y demás controles de producción siguen pendientes. No abras puertos
del entorno local como sustituto de ese trabajo.

El paquete permite comprobar Windows localmente sin cuenta ni API. La
vinculación, el contacto y el envío sí necesitan la API indicada. No hay
recolección ni envío en segundo plano.

## Datos, actualización y retiro

La carpeta del programa contiene binarios y documentación. El estado se guarda
por separado en `%LOCALAPPDATA%\SecurityCenterAI\Agent`:

| Estado | Tratamiento |
| --- | --- |
| `credential.enc` | Credencial y destino protegidos con DPAPI `CurrentUser`. No se distribuye ni se copia a otra cuenta Windows. |
| `reports\scan-*.json` | Evidencia seleccionada y resultados en texto claro. No se incluyen en el ZIP ni se envían salvo solicitud explícita. |

La protección DPAPI corresponde al usuario Windows; procesos bajo ese mismo
contexto pueden descifrar la credencial. No protege una sesión ya comprometida
ni convierte ese archivo en una credencial transportable.
[Alcance CurrentUser](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.dataprotectionscope).

Para actualizar, termina las operaciones del agente, verifica el paquete nuevo
y extráelo en una carpeta nueva. Ejecuta allí `version` y `doctor`. En la misma
PC y cuenta Windows, el programa consulta el estado existente en LocalAppData:
no copies `credential.enc` ni informes dentro del paquete. Conserva la carpeta
anterior hasta comprobar la nueva; no mezcles archivos de distintas versiones.
Un usuario o equipo distinto debe realizar su propia vinculación, respetando
el límite de un dispositivo activo por cuenta.

Para retirar el acceso remoto, pulsa **Revocar acceso** en la web. Después,
`SecurityCenterAI.Agent.exe forget` borra solamente la credencial local.
`forget` por sí solo no revoca otras copias ni borra informes. Quitar la carpeta
del programa tampoco elimina el estado en LocalAppData. No existe borrado o
retención automática: revisa y elimina manualmente los informes cuando ya no
los necesites. El historial y feedback de la web no se borran por estas acciones.

## Compatibilidad y evidencia

El objetivo inicial del paquete es Windows 11 x64 con Windows PowerShell 5.1. Las diez
comprobaciones dependen de los módulos, edición, hardware y permisos de la PC;
`doctor` no garantiza que todas puedan responder. Ejecuta primero sin elevar
permisos y conserva los estados desconocidos. No cambia BIOS, TPM, cifrado,
firewall, antivirus ni Windows Update.

| Entorno | Evidencia de esta etapa |
| --- | --- |
| Antecedente del paquete 0.4.0-preview.1 en el anfitrión: Windows 11 Home Single Language, 26H2, build 26300.9711, x64 | Ayuda, versión, doctor/JSON y launchers comprobados el 22 de octubre de 2026 desde ruta con espacios/ñ y sin SDK en PATH, con runtime incluido 8.0.32. No se ejecutaron scan/pair/forget sobre el perfil real. Es un host con SDK y no equivale a probar una máquina limpia ni todas las instalaciones de Windows 11. |
| Paquete actual 0.4.1-preview.1 en el anfitrión de desarrollo | Helper público aprobado: 1.599 comprobaciones, nueve procesos de diagnóstico y cuatro alteraciones rechazadas; runtime incluido 8.0.32 y 192 archivos declarados más manifiesto. La prueba de paquete no ejecuta scan/pair/forget sobre el perfil real. No valida una segunda PC o una instalación limpia sin SDK. El hash y tamaño de esta entrega están en el changelog. |
| Windows 11 x64 limpio sin SDK/runtime preinstalados | Pendiente de comprobación en un equipo o VM separado. El paquete se publica como autocontenido; no se afirma haber validado este entorno. |
| Recorrido manual del usuario en la misma PC, agente 0.4.0-preview.1 | Doctor con requisitos básicos disponibles y nuevo análisis recibido en PostgreSQL el 22 de octubre de 2026: 75/100, cobertura 70 %, siete correctas y tres desconocidas. Sin resultados para revisar. Confirma el recorrido manual en este entorno; la prueba en otro equipo sigue pendiente. |
| Otras ediciones o builds de Windows 11 | Sin validación específica registrada. No se generalizan los resultados del anfitrión. |
| Windows 10 o Windows Server | Fuera del objetivo inicial y sin validación específica registrada. |
| Windows ARM64 o x86, Linux y macOS | Fuera del destino `win-x64` de este paquete; no validados ni ofrecidos por esta etapa. |

Los resultados de compilación, pruebas automatizadas y comprobación manual
realmente realizados pertenecen a [CHANGELOG.md](../CHANGELOG.md). Los controles
pendientes de esta tabla no deben presentarse como pruebas aprobadas.

## Recorrido de una prueba supervisada

Antes de invitar personas o enviar archivos, acuerda con cada participante el
alcance, los datos y si autoriza el envío. Este documento no da de alta usuarios
ni contacta personas. Para cada prueba consentida registra:

1. Identificador de caso sin nombre personal, fecha, paquete y SHA-256;
   Windows, arquitectura, nivel de permisos y restricciones relevantes.
2. Resultado de `version`, `doctor`, apertura del menú y un `scan` local.
   Comprueba que los archivos se guarden bajo el usuario y que el análisis
   local no aparezca en la web antes del envío.
3. Solo si hay una API preparada y consentimiento de envío: vinculación con
   código oculto, contacto, envío, reintento del mismo informe y correspondencia
   del dispositivo, puntos, cobertura e historial en la web.
4. Claridad de las guías y feedback, estados desconocidos, errores observados y
   recuperación. Revisa códigos, tokens, correo y rutas antes de compartir
   capturas; no adjuntes la credencial ni informes completos por defecto.
5. Revocación y retirada acordadas, distinguiendo credencial local, informes e
   historial del servidor. Conserva únicamente la evidencia autorizada.

Anota el resultado de cada paso como aprobado, fallido o no realizado, con su
causa. Un `doctor` correcto o diez resultados favorables no certifican la PC.
Antes de ampliar la beta quedan pendientes compatibilidad en equipos separados,
distribución firmada y confiable, infraestructura HTTPS y controles de operación,
cuentas, privacidad y retención descritos en [ROADMAP.md](../ROADMAP.md).

La referencia completa de comandos y reglas permanece en
[WINDOWS_AGENT.md](WINDOWS_AGENT.md); el protocolo, puntaje e idempotencia están
en [ANALYSIS_INGESTION.md](ANALYSIS_INGESTION.md).
