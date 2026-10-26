# Herramientas de diseño

Se instalaron para el entorno de desarrollo de esta PC, con autorización del
usuario. No son dependencias necesarias para ejecutar Security Center AI.

| Herramienta | Instalación | Uso en este proyecto |
| --- | --- | --- |
| Impeccable 4.7.1 | Skill global `impeccable` | Refinamiento de interfaces, jerarquía, estados y revisión mecánica. |
| UI/UX Pro Max | Skill global `ui-ux-pro-max` | Consulta de patrones y criterios de lectura/accesibilidad. |
| Product Design 0.1.56 | Plugin nativo de Codex | Flujo de auditoría con capturas, criterio de producto y revisión visual. |
| Figma 16.0.0 | Plugin nativo de Codex | Preparado para trabajo editable; requiere que Codex exponga sus herramientas y, para funciones remotas, la conexión de cuenta. |
| ECC | Plugin existente | `frontend-design-direction`, `design-system`, `make-interfaces-feel-better`, `accessibility`. |

Python 3.14 se instaló para el usuario porque UI/UX Pro Max utiliza scripts de
búsqueda locales. La app web y la API no requieren Python.

Fuentes: [Impeccable](https://github.com/pbakaus/impeccable) (revisión
`ade37830fee364c1457f9ada479470296f80dbbb`) y
[UI/UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill) (revisión
`28b3b8dee8c976df4fa3e94aed4e092ad43fcb03`). Los plugins se instalaron desde
el catálogo de Codex. No se enviaron capturas de cuentas personales a servicios
de diseño ni se creó un archivo remoto en Figma.

## Trabajo diario

Podés pedir «usá Impeccable para pulir esta pantalla», «auditá accesibilidad» o
«aplicá nuestro sistema visual». Codex también puede elegir estas herramientas
cuando sean pertinentes. No necesitás nombrarlas en cada cambio.

Las nuevas instalaciones se descubren automáticamente en turnos posteriores de
Codex. En el turno de instalación se leyeron sus instrucciones locales para
aplicarlas. Si Figma pide conectar la cuenta, hacelo desde el panel de plugins;
no compartas claves por el chat. La conexión remota de Figma no se verificó en
esta sesión y no condiciona la ejecución de la app.

La dirección y los límites del producto están en [DESIGN.md](../DESIGN.md) y
[PRODUCT.md](../PRODUCT.md). Los valores están en
[design-tokens.json](../design-tokens.json), con implementación compartida en
`src/SecurityCenterAI.Web/src/app/design-system.css`.

## Revisión reproducible

La prueba visual utiliza una cuenta, equipo e informe ficticios en la base
aislada creada por el runner. No utiliza resultados personales ni consulta IA
externa. Guarda PNG locales en `artifacts/design-review/after/`.

```powershell
$env:SC_AI_VISUAL_REVIEW = '1'
$env:DESIGN_REVIEW_PHASE = 'after'
node scripts/test-web-e2e.mjs --configuration DesignQa -- e2e/design-review.spec.ts
Remove-Item Env:SC_AI_VISUAL_REVIEW, Env:DESIGN_REVIEW_PHASE
```

Para una revisión completa, omití la selección del archivo de prueba. Los
artefactos y dependencias locales permanecen fuera de Git. No se publican
pantallas ni se exporta el proyecto a una herramienta remota automáticamente.
