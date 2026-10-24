# Verificación local consolidada

Desde la raíz, en otra terminal, con dependencias ya instaladas:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
```

Comprueba `git diff --check`, compila los proyectos de pruebas y sus dependencias
en `VerificationQa`, ejecuta backend/agente y después unitarias, tipos y build
de producción de la web. Usa `.next-qa` para el build; restaura el archivo generado
`next-env.d.ts` y la variable QA incluso si falla. No instala, migra o inicia
servidores, cambia `.env`, crea commits o publica archivos. El build se guarda
en las carpetas ignoradas habituales. No debe ejecutarse simultáneamente con
otro build que use la misma configuración o `.next-qa`.

Las pruebas PostgreSQL de la corrida habitual aparecen omitidas de forma explícita.
Para incluir las tres suites existentes, con Docker/PostgreSQL disponibles:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -IncludePostgres
```

Reutiliza los helpers de actividad/cuentas, retención y backup: cada uno crea
su base ficticia con nombre acotado y la retira en `finally`. La suite de backups
también ensaya la CLI cifrada con secretos de prueba. No purga o restaura sobre
la base personal. Necesita `.env` local para conectarse sin mostrar sus valores.

Para incluir **toda** la suite E2E contra servicios locales ya iniciados:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1 -IncludePostgres -IncludeE2E
```

E2E usa PostgreSQL/Mailpit y cuentas `e2e-<UUID>@example.invalid` con limpieza
acotada. Hay que tener API y web locales funcionando y Playwright instalado.
Escribe datos ficticios en la base de desarrollo; no es una prueba de solo
lectura. No debe apuntarse a una instalación pública o con usuarios externos.
El helper rechaza direcciones E2E que no sean loopback, tengan credenciales,
query, fragmento o ruta distinta de raíz; no imprime los valores rechazados.
Respeta los límites reales de la API, que pueden afectar una corrida extensa.
Los recorridos seleccionados pueden ejecutarse con `npm.cmd run test:e2e -- ...`
como en los bloques previos, sin pedir al script una corrida histórica completa.

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
