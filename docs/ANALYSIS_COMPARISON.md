# Comparar análisis de un dispositivo

Implementado el 22 de octubre de 2026. Se adelanta la comparación prevista en
la etapa 1.1 mientras queda pendiente probar el agente en otra PC. No requiere
instalar herramientas, actualizar el paquete del agente ni cambiar la base.

## Cómo usarlo

1. Entrá a **Dispositivos → Ver equipo e historial**.
2. Cuando haya dos análisis o más, elegí **Comparar análisis**.
3. Seleccioná el análisis anterior y el reciente y pulsá **Comparar**. Al abrir
   la vista se seleccionan por defecto los dos últimos informes recibidos.
4. Revisá los resultados originales, la diferencia de puntos/cobertura cuando
   corresponda y las evidencias de cada regla en ambos informes.

El selector ofrece los últimos veinte informes del equipo. Un enlace con dos
identificadores válidos también permite consultar informes anteriores que sigan
perteneciendo al mismo dispositivo y usuario. La selección queda en la URL;
no se guardan comparaciones ni se modifican informes.

Las fechas principales indican cuándo el servidor recibió cada informe. La
fecha de comprobación procede del agente y es autodeclarada. No se admite
seleccionar el mismo informe dos veces ni invertir la recepción o, cuando ambos
informan su fecha, el orden de finalización de las comprobaciones. Un envío
tardío puede hacer que esos órdenes difieran; en ese caso se pide otra selección.

## Qué significa cada cambio

| Estado anterior → reciente | Categoría | Interpretación |
| --- | --- | --- |
| Para revisar → Correcto | Mejoras observadas | La nueva observación cumple el criterio; no demuestra quién o qué produjo el cambio. |
| Correcto → Para revisar | Para revisar | Aparece una condición para revisar; no demuestra una infección. |
| Desconocido → Correcto o Para revisar | Nueva información | Ahora se pudo observar algo que antes era desconocido. No equivale a una reparación. |
| Correcto o Para revisar → Desconocido | Información perdida | La consulta más reciente no determinó un estado antes conocido; no prueba un fallo nuevo. |
| Estados iguales | Sin cambio de estado | Coincide el estado, aunque evidencia y momento pueden diferir. Desconocido sigue siendo desconocido. |
| Cambia la aplicabilidad, falta una regla, está duplicada o cambia una versión no compatible | No comparables | Se conservan los registros originales sin inventar una equivalencia. |

Las comparaciones de estados requieren informes `windows-agent`, esquema 2,
modelo de puntos 1 y reglas conocidas versión 1. Los registros anteriores o
incompatibles se muestran sin deducir cambios. Se unen las reglas de ambos
informes: una regla ausente no se interpreta como un resultado correcto.

Los puntos se toman de los análisis guardados, sin recalcularlos. Solo se muestra
una diferencia numérica cuando ambos informes contienen el catálogo completo de
diez reglas, sin duplicados, con versiones compatibles, puntajes y cobertura
disponibles y el mismo conjunto de reglas aplicables. La diferencia es reciente
menos anterior; la cobertura se expresa en puntos porcentuales. Si cambia la
aplicabilidad o falta un puntaje no se presenta una diferencia de universos distintos.

Una diferencia positiva puede incluir nueva información. No es una probabilidad
de seguridad ni prueba una reparación. La comparación describe observaciones de
dos momentos; no consulta el estado actual de Windows ni ejecuta cambios.

## API y privacidad

`GET /api/devices/{deviceId}/analyses/compare?baselineId={id}&targetId={id}`
requiere una sesión Bearer de cuenta y verifica ambos propietarios y dispositivo.
Una credencial Device no permite leer comparaciones. Un dispositivo revocado
conserva su historial para el propietario. Las selecciones ajenas reciben 404 y
las selecciones repetidas o invertidas reciben 400.

La respuesta incluye ambos análisis originales, reglas/estados/evidencias,
categorías, resumen y diferencias cuando corresponden. El frontend valida el
contrato y muestra evidencia como texto. Comparte los límites existentes de
`DeviceAccount` y globales. Las consultas no crean registros, envían correo ni
transfieren información a un servicio de IA.

Las restricciones de retención, exportación y borrado del historial siguen
siendo las documentadas en [PRIVACY_NOTES.md](../PRIVACY_NOTES.md).
