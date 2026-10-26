# Revisión local del 26 de octubre de 2026

**Preparación para publicación: 60/100; desarrollo y demostración local
funcionales, publicación pendiente.** Es una rúbrica de ingeniería con nueve de
quince controles comprobados y peso uniforme, no una certificación ni una
medición de la seguridad de una PC. Cualquier bloqueo operativo sigue impidiendo
publicar aunque las pruebas de software pasen.

## Controles con evidencia local

| Control | Evidencia y alcance |
| --- | --- |
| Compilación | API/agente compilados; tipos y build de producción web comprobados con salida QA separada. |
| Pruebas sin PostgreSQL | 389 API, 145 agente y 103 unitarias web aprobadas. Las 117 pruebas de IA están incluidas en las 389. |
| Integración PostgreSQL | 40 aprobadas en cuatro suites temporales: cuentas/actividad 16, retención 4, backups/recuperación 8 y cola de correo 12. Se omiten explícitamente en la ejecución habitual. |
| Recorridos de navegador | 89 escenarios distintos cubiertos entre la corrida general y las repeticiones focalizadas. El caso de ZIP ausente necesita su propia configuración; las 14 pruebas de IA utilizan transporte ficticio. |
| Autenticación y propiedad | Sesiones, renovación, recuperación, cambios de cuenta, exportación y borrado probados con cuentas ficticias; incluyen aislamiento entre propietarios y carreras relevantes. No equivale a una auditoría de penetración. |
| Tratamiento de datos de IA | Botón explícito sin casilla, contexto reducido, validación de salida y alternativa local. La prueba real previa del adaptador usó diez estados ficticios; no se envió un informe personal. No es una revisión de las políticas del proveedor. |
| Dependencias NuGet/npm | Se eliminaron dos alertas de transitivas antiguas en ambos proyectos de pruebas. La repetición de auditoría NuGet revisó los seis proyectos sin alertas conocidas; npm tampoco reportó vulnerabilidades. No cubre el runtime instalado ni garantiza ausencia de fallos. |
| Copias y recuperación ensayadas | Creación/verificación cifrada e invalidación de accesos comprobadas en bases temporales. No se restauró sobre la base personal. |
| Diagnóstico local | API readiness, web y gateway respondieron 200. Pruebas de cola y mantenimiento aprobadas; no demuestra entrega de SMTP externo o monitoreo continuo. |

Los resultados del navegador se consolidan entre ejecuciones: la corrida extensa
alcanzó 87 aprobados, una omisión por ZIP presente y un fallo en una espera de
respuesta añadida al test de descarga. Se retiró esa instrumentación; ambos
recorridos de exportación aprobaron después. El recorrido de ZIP ausente también
aprobó por separado. No se presenta esa secuencia como una única corrida verde.

## Correcciones de esta revisión

- `test-web-e2e.mjs` crea su base, puertos, API/web y clave JWT ficticia. Aplica
  migraciones solo allí y retira sus procesos/base al terminar. `verify.ps1
  -IncludeE2E` ahora lo utiliza. Los SQL de preparación y limpieza reciben el
  destino temporal y validan su nombre. No cambia límites o servicios habituales.
- Los límites por IP del servidor de pruebas admiten la carga de una suite
  completa. Los límites de negocio siguen activos; las pruebas backend de
  políticas conservan su configuración propia. La prueba con servicios
  compartidos había encontrado respuestas 429 por la carga acumulada.
- Se acotaron selectores al formulario/región correctos: tres controles llamados
  «Contraseña actual» y el anunciador de navegación de Next.js hacían ambiguas
  algunas pruebas. Se actualizaron las expectativas de los campos públicos
  adicionales de exportación, conservando la revisión de ausencia de secretos.
- Se fijaron `System.Net.Http` 4.3.4 y `System.Text.RegularExpressions` 4.3.1
  únicamente en proyectos de pruebas con `PrivateAssets="all"`. Eran transitivas
  del framework de pruebas. Fuentes: [aviso HTTP](https://github.com/advisories/GHSA-7jgj-8wvc-jh57)
  y [aviso regex](https://github.com/advisories/GHSA-cmhx-cq75-c4mj).
- Las huellas agregadas de usuarios, equipos, análisis y hallazgos coincidieron
  antes/después; no quedaron usuarios E2E en la base personal. No hubo commit,
  publicación, migración personal ni cambio de `.env` por esta revisión.

## Seis controles pendientes para publicar

1. **Runtime y soporte:** instalado SDK 8.0.426/runtime 8.0.32. Microsoft publica
   8.0.33 como parche vigente y fija el fin de soporte de .NET 8 en **2026-11-10**;
   .NET 10 LTS tiene soporte hasta 2028-11-14. Actualizar el runtime utilizado por
   la API, volver a empaquetar el runtime incluido en el ZIP y preparar la
   migración a LTS vigente antes de una publicación posterior. Un agente
   autocontenido no recibe la actualización solo porque cambie el runtime del
   sistema. [Política oficial](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
2. **Responsable e información pública:** el usuario todavía debe definir
   responsable, país y correo público. Conservado el recordatorio; privacidad,
   términos y canales reales siguen en borrador.
3. **Alojamiento:** destino, dominio/TLS, orígenes, secretos, permisos y operación
   externa no configurados ni comprobados.
4. **Correo externo:** proveedor, remitente/dominio y entrega real pendientes.
   Mailpit es la bandeja local de desarrollo.
5. **Recuperación operativa:** custodia de claves, programación/rotación y copia
   fuera de la PC, responsables y recuperación real pendientes. El ensayo
   aislado no demuestra recuperación en un alojamiento real.
6. **Distribución y compatibilidad:** otra PC/VM Windows continúa pospuesta por
   el usuario. Firma, comportamiento de descarga/SmartScreen y actualización
   segura necesitan su validación correspondiente antes de ampliar las promesas.

El workflow CI está preparado y conserva su ensayo local previo; no se ejecutó
en GitHub en esta revisión. La inferencia de apoyo al desarrollo continúa
desactivada, separada de la clave de producto. Las cuotas de IA en memoria y la
operación con varias instancias mantienen sus límites documentados.

## Diseño para revisar después

Hay [tres propuestas documentadas](DESIGN_PROPOSALS.md), con referencias oficiales
y compromisos de cada opción. Recomiendo **Centro Windows claro con el lenguaje
de Informe guiado**. No se aplicó un rediseño ni se instalaron bibliotecas
visuales. La primera pantalla de muestra se preparará después de la revisión
con el usuario.

Para repetir pruebas, consultar [Verificación local](VERIFICATION.md). Los
registros y huellas de esta revisión están en `artifacts/overnight-review/` y
los logs de servidores ficticios en `artifacts/web-e2e/`, excluidos de Git.
