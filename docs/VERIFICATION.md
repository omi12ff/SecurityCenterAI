# Verificación local consolidada

Desde la raíz, en otra terminal, con dependencias ya instaladas:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
```

Comprueba `git diff --check`, compila los proyectos de pruebas y sus dependencias
en `VerificationQa`, ejecuta backend/agente y después unitarias, tipos y build
de producción de la web. Usa `.next-qa` para el build; restaura el archivo generado
`next-env.d.ts` y la variable QA incluso si falla. Sin opciones no instala, migra
o inicia servidores, cambia `.env`, crea commits o publica archivos. El build se guarda
en las carpetas ignoradas habituales. No debe ejecutarse simultáneamente con
otro build que use la misma configuración o `.next-qa`.

Las pruebas PostgreSQL de la corrida habitual aparecen omitidas de forma explícita.
Para incluir las cuatro suites existentes, con Docker/PostgreSQL disponibles:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -IncludePostgres
```

Reutiliza los helpers de actividad/cuentas, retención, backup y cola de correo: cada uno crea
su base ficticia con nombre acotado y la retira en `finally`. La suite de backups
también ensaya la CLI cifrada y la invalidación de accesos/conservación de contenido
con secretos de prueba (`create`, `verify`, `rehearse`). No purga o restaura sobre
la base personal. Necesita `.env` local para conectarse sin mostrar sus valores.

Para incluir **toda** la suite E2E con PostgreSQL y Mailpit disponibles y Chromium
instalado (`npx.cmd playwright install chromium` desde la carpeta web):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -IncludePostgres -IncludeE2E
```

`-IncludeE2E` utiliza `scripts/test-web-e2e.mjs`: crea una base
`scai_e2e_test_<UUID>`, aplica las migraciones allí e inicia API y web propias en
puertos loopback temporales. Genera una clave JWT de prueba y deshabilita la IA
en esa API. No reinicia los servicios habituales, modifica sus límites ni envía
informes al proveedor. Los límites por IP de la instancia de prueba admiten la
carga de la suite; las cuotas diarias y las validaciones de negocio se conservan.
Los tests de políticas de la API comprueban por separado los límites de seguridad.

Las operaciones SQL de preparación y limpieza reciben `E2E_DATABASE_NAME` y
rechazan nombres fuera de ese espacio. Al finalizar, incluso ante un fallo de
prueba, se detienen los procesos propios y se elimina esa base temporal. Los
mensajes de Mailpit se limpian solo por el destinatario ficticio de cada prueba.
No vacía la bandeja ni escribe en la base personal. Los registros locales quedan
en `artifacts/web-e2e/`, excluidos de Git. Una terminación forzada del sistema
puede impedir `finally`; no eliminar bases o procesos ajenos para recuperarlo.

Para repetir únicamente la suite completa de navegador, desde la raíz:

```powershell
npm.cmd --prefix src/SecurityCenterAI.Web run test:e2e:isolated
```

Este comando compila API/web antes de probar. `-- --configuration OtraQa`
selecciona otra carpeta de API; `-- --configuration OtraQa --no-build` requiere
ese DLL y `.next-qa` ya compilados. El helper restaura `next-env.d.ts` al terminar.
Para seleccionar archivos, usa el separador `--`, por ejemplo
`node scripts/test-web-e2e.mjs --configuration OtraQa --no-build -- e2e/account-export.spec.ts`.
La variante `--agent-package-unavailable` inicia su web con ZIP y destino del
agente ausentes y selecciona ese recorrido de error, sin modificar la
configuración habitual. Se prueba aparte: en la corrida normal se omite porque
el ZIP está configurado.
Requiere Node 24, SDK/.NET EF local restaurado, Docker/PostgreSQL, Mailpit y las
dependencias web instaladas; no instala herramientas ni cambia `.env`.

`npm.cmd run test:e2e -- ...` sigue siendo la entrada directa de Playwright para
los recorridos seleccionados y las fixtures HTTP de IA. Sin las variables de
aislamiento apunta a los servicios de desarrollo y sus límites reales; no es el
comando recomendado para repetir toda la suite. No apuntarlo a producción.

El [recorrido integrado 1.0](RELEASE_VALIDATION.md) concentra registro, ZIP,
agente simulado, análisis, guías, comparación, detector, exportaciones y borrado.
Se repite con las páginas de información y landing mediante:

```powershell
Set-Location .\src\SecurityCenterAI.Web
npx.cmd playwright test e2e/information.spec.ts e2e/landing.spec.ts e2e/release-journey.spec.ts
```

No ejecutar dos suites E2E simultáneamente ni mezclar una corrida de navegador
con build/typegen o reinicios de sus servicios. El recorrido no ejecuta el agente
real ni lee credenciales personales y no reemplaza su matriz de compatibilidad.

El script falla en el primer comando fallido. `-Configuration OtraQa` permite
separar compilaciones; se rechaza una configuración usada por una API activa.
La solución solo declara Debug/Release, por eso se compilan los proyectos de
pruebas directamente, incluidos todos sus proyectos referenciados actuales.
Si se agregan nuevos proyectos de pruebas, hay que incorporarlos al helper.

## Prueba aislada de explicación de IA

### Resultado más reciente de la revisión completa

El 2026-10-26 se repitieron **389 pruebas API, 145 del agente y 103 unitarias
web**, con tipos/build comprobados. Las **40 PostgreSQL** de la suite habitual
se omitieron allí de forma explícita y aprobaron por separado: 16 cuentas y
actividad, 4 retención, 8 backup/recuperación y 12 cola de correo.

Se revisaron los **89 escenarios distintos de navegador**: corrida general con
87 aprobados y una omisión para ZIP ausente; las dos pruebas de exportación
aprobaron en la repetición focalizada después de retirar una espera de respuesta
que no corresponde al evento de descarga. El escenario de ZIP ausente aprobó
con `--agent-package-unavailable`. No se presenta esa combinación como una única
corrida sin fallos. También se ensayó el runner con compilación propia y dos
recorridos de exportación, y su limpieza ante una selección sin pruebas.

Se parchearon dos transitivas de los proyectos de pruebas; la auditoría NuGet
revisó los seis proyectos sin vulnerabilidades conocidas reportadas, y npm
reportó cero. Esto no cubre el runtime instalado o el incluido en el ZIP;
su actualización quedó priorizada en el [inventario](PENDING_FEATURES.md).
Las huellas de los cuatro conjuntos principales de datos personales se
conservaron. [Informe y pendientes](LOCAL_REVIEW_2026_10_26.md).

### Evidencia de los bloques anteriores

La corrección posterior de la espera **«Pensando»** aprobó **390 pruebas API**
(40 PostgreSQL omitidas explícitamente, sin repetir sus suites) y **15 E2E de IA**,
con tipos/build web. Las 390 incluyen **118 de IA** y una comprobación de que el
diagnóstico no registra contenido del modelo o credenciales. La prueba real
del adaptador con esquema estricto y estados ficticios devolvió diez reglas
válidas; hubo también errores transitorios de proveedor y timeout. No se envió
el informe personal. [Formato y límites actuales](AI_EXPLANATIONS.md).

La validación del 2026-10-26 aprobó **371 pruebas de API**, con 40 PostgreSQL
omitidas explícitamente, y **145 del agente**. Las 371 incluyen **99 nuevas de
IA**: 33 del servicio y 66 del proveedor HTTP ficticio. La compilación de API
terminó sin errores ni advertencias. También aprobaron **101 unitarias web**,
tipos, build y **11 E2E de IA aisladas**. No se comprobó inferencia externa.

La comprobación posterior del alias, también el 2026-10-26, aprobó **110 pruebas
filtradas de IA**: 77 de proveedor HTTP ficticio y 33 de servicio. Además aprobaron
**16 fixtures del helper** y la compilación de API `Debug` terminó sin errores
ni advertencias. No reemplaza la corrida consolidada anterior ni su total de 371.
La API se reinició con la corrección y readiness/web respondieron 200; quedó
habilitada para una prueba explícita desde la interfaz, sin inferencia real
comprobada o análisis enviado al proveedor.

Para preparar el build de producción aislado en `.next-qa` y repetir solo esos
recorridos de navegador, desde la raíz y con Playwright instalado:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
npm.cmd --prefix src/SecurityCenterAI.Web run test:e2e -- e2e/ai-explanation.spec.ts
```

El primer comando repite también backend, agente, unitarias y tipos; no incluye
las suites PostgreSQL ni los E2E históricos. La fixture de IA inicia su propia
API HTTP ficticia y servidor Next.js sobre puertos loopback temporales, con
cuenta, token e informe sintéticos. No usa la base personal, Mailpit, credenciales
reales o un proveedor de IA, y detiene sus servidores al terminar. No requiere
iniciar la API/web habitual. Evitá otro build que use `.next-qa` durante la corrida.

Los recorridos comprueban clic explícito sin casilla, carga, texto escapado, estados
originales, salida temporal, alternativas ante fallos/cuota, respuestas inválidas,
disponibilidad 404 de una API anterior y errores 401/404 de generación. No equivalen
a toda la suite E2E histórica ni a una prueba del proveedor real.

La reparación posterior aprobó **117 pruebas IA**, **102 unitarias web** y
**14 E2E aisladas**, con tipos/build correctos. Cubren solicitud con botón sin
casilla y causas seguras de rechazo del gateway, permisos, cuota y timeout.
Un diagnóstico separado con el adaptador real y estados ficticios produjo
`generated`, un resumen de 524 caracteres y diez reglas válidas. No se usó un
informe personal. El nombre corto fue rechazado por ambiguo y la instalación
fija ahora `openrouter/openrouter/free` en la app y en su clave.

Para ejecutar el filtro actual de 117 pruebas backend de IA con transporte controlado:

```powershell
dotnet test .\tests\SecurityCenterAI.Tests\SecurityCenterAI.Tests.csproj --configuration AiExplanationQa --filter "FullyQualifiedName~AnalysisAiExplanationTests|FullyQualifiedName~AiExplanationProviderTests"
```

El filtro completo va entre comillas para que PowerShell no interprete `|` como
una tubería. Usá una configuración de compilación distinta si esa carpeta está
siendo usada por otra API o corrida.

## Workflow de GitHub Actions

El archivo [ci.yml](../.github/workflows/ci.yml) está preparado para push, pull
request y ejecución manual. Reutiliza el helper en Windows Server 2022 con Node
24 y SDK de `global.json`. Instala el frontend con `npm ci`, desde el lockfile.
Las acciones están fijadas a SHA completos, el token tiene permiso de lectura
y checkout no conserva credenciales. No usa secretos de la aplicación, Docker,
datos personales, PostgreSQL o E2E, ni publica binarios/artefactos.

Se comprobaron referencias e inputs con las fuentes primarias de
[checkout](https://github.com/actions/checkout),
[setup-dotnet](https://github.com/actions/setup-dotnet) y
[setup-node](https://github.com/actions/setup-node). La definición pasa
[actionlint 1.7.12](https://github.com/rhysd/actionlint/releases/tag/v1.7.12),
instalado fuera del repositorio tras verificar el SHA-256 del archivo de release.
Los cambios de versión/SHA requieren volver a revisar sus notas y compatibilidad.

Para repetir el lint con la herramienta instalada en esta máquina:

```powershell
& "$env:LOCALAPPDATA\SecurityCenterAI\Tools\actionlint\1.7.12\actionlint.exe" .github\workflows\ci.yml
```

El helper también se ensayó en una copia temporal con los archivos candidatos,
sin `.env`, dependencias o compilados previos: `npm ci`, backend, agente, unitarias,
tipos y build aprobaron. Es un ensayo limpio en la PC actual; no reemplaza el
runner alojado o la prueba del agente en otra PC. La revisión automática rechazó
su borrado recursivo con motivo `blocked by policy`; la copia quedó conservada en
`%LOCALAPPDATA%\SecurityCenterAI\CiRehearsal\ci_106b7d93a79f468b94517e8ccd03ec2b`.
Contiene fuentes de prueba y artefactos generados; se excluyeron `.env` y
configuración privada al crearla. No se publicaron sus archivos.

El workflow todavía **no se envió o ejecutó en GitHub**. Los resultados locales
no prueban el runner alojado; la primera corrida remota sigue pendiente. No se
cambiaron reglas de protección, secretos o permisos del repositorio remoto.

La verificación no sustituye ensayo en otra PC, revisión de dependencias,
seguridad/capacidad u operación de producción. Ejecutar el helper local no activa
GitHub Actions ni publica resultados fuera del equipo.
