# Sistema visual de Security Center AI

Estado: refinamiento aplicado y validado localmente, 2026-10-26.

## Dirección

Una herramienta clara y serena para Windows. Conservar la identidad azul/verde
agua del producto y hacer más fácil leer y decidir: primero lo observado,
después lo que necesita atención y finalmente el detalle. Resumen es una
superficie de operación; la guía combina lectura con acciones explícitas.

Referencias y alternativas: docs/DESIGN_PROPOSALS.md. Se adopta A con la lectura
guiada de B; no se crea una marca nueva ni se añaden funciones de protección.

## Contrato verificable

| Superficie | Trabajo principal | Jerarquía |
| --- | --- | --- |
| Resumen | Entender el último informe y decidir qué revisar. | Puntos/cobertura, prioridades, historial. |
| Entender el análisis | Comprender estados y revisar evidencia. | Resumen, explicación opcional, comprobaciones con detalle progresivo. |
| Dispositivos | Vincular y administrar acceso. | Equipo/estado, siguiente acción, historial. |
| Cuenta y acceso | Gestionar identidad y sesiones. | Acción concreta, campos legibles, feedback y confirmaciones. |

Tamaños de revisión: 360x800, 768x1024 y 1440x900. Contenido real largo y estados
parciales deben caber sin desbordar la página. Las tablas pueden desplazarse en
su propio contenedor. Navegación y formularios funcionan con teclado.

## Reglas visuales

- Tipografía de sistema de Windows, seleccionada por familiaridad: Segoe UI
  Variable / Segoe UI. Texto principal legible y una escala clara de títulos.
- Texto de lectura acotado; agrupar información y abrir detalles cuando se
  necesitan. Conservar datos, evidencia y referencias completas.
- Escala de espacios 4/8/12/16/24/32/40/48. Controles de al menos 44px cuando
  corresponde; foco visible y separación entre acciones.
- Superficies blancas con borde sobrio; no sumar sombras fuertes ni anidar
  tarjetas para cada párrafo. Verde para correcto, ámbar para revisar, azul
  para desconocido y rojo para errores/acciones destructivas, siempre con texto.
- Números tabulares. Ningún anillo o dibujo simula un porcentaje de protección.
- Animación solo para comunicar interacción/espera. Conservar «Pensando» y
  respetar movimiento reducido y colores forzados.

## Estados

Movimiento de botones: una presión breve reconoce la acción (120ms, escala
0.985 y desplazamiento de 1px); la superficie cambia suavemente al pasar el
cursor (180ms). El foco es inmediato y las solicitudes no esperan a que termine
la transición. Sin entradas de página, rebotes o dependencias nuevas. Los
botones deshabilitados no se animan y el movimiento reducido elimina la
transformación, conservando el feedback de color. Presupuesto: CSS sobre el
control activo, sin listeners de puntero ni cálculos de layout en cada frame.

| Estado | Feedback y salida |
| --- | --- |
| Carga | «Pensando» en IA; acción en curso sin doble envío; guía disponible. |
| Vacío | Explicar que no hay equipo o informe y señalar Primeros pasos. |
| Error | Causa segura y reintento; no presentar la guía como salida de IA. |
| Parcial | Cobertura y desconocidos visibles, sin convertirlos en fallos. |
| Éxito | Resultado vinculado al informe original y sus referencias. |
| Permiso | Renovación/verificación o regreso seguro sin revelar recursos ajenos. |

## Herramientas y evidencia

Impeccable y ECC para pulido, consistencia y accesibilidad; UI/UX Pro Max como
referencia de patrones. Sus búsquedas generales devolvieron plantillas de
marketing que no encajan con esta app: no se adoptaron badges de confianza,
testimonios, ventas o datos inventados. Las reglas de lectura y accesibilidad
se contrastan con el código y el navegador.

Product Design aporta la revisión de flujo y capturas; Figma está instalado para
trabajo editable cuando sus herramientas/conexión estén disponibles en Codex.
La implementación actual y las capturas permanecen locales. Evidencia y
comparación en artifacts/design-review/, fuera de Git.

## Validación realizada

- Compilación web y comprobación TypeScript correctas; 103 pruebas unitarias web.
- 90 casos E2E del perfil normal quedaron aprobados. La ejecución completa
  encontró dos localizadores de feedback ambiguos por el estado accesible de IA;
  se acotaron a la región de valoración y ambos casos pasaron en la repetición.
  El caso adicional de paquete ausente pasó en su perfil separado: 91 casos
  distintos verificados entre ambos perfiles.
- Capturas reales de seis superficies en 360x800, 768x1024 y 1440x900, con cuenta
  e informe ficticios. Sin desbordamiento horizontal del documento ni errores
  JavaScript no controlados en ese recorrido. La sección actual queda visible
  en la navegación móvil. Se revisó también un viewport equivalente a zoom 200%
  (720x450 CSS, escala 2), esperando a que termine la renovación de sesión.
- Nueve pares de la paleta cumplen 4.5:1 para texto y 3:1 para el borde de campo.
  Se comprobaron teclado y apertura por enlace de las evidencias, foco completo
  de 3px, movimiento reducido, colores forzados y estados de IA sin solicitudes
  a un proveedor real. No es una certificación de accesibilidad ni una prueba
  con lector de pantalla o hardware externo.
- El detector mecánico de Impeccable sobre los cambios principales no encontró
  hallazgos. Las capturas posteriores se usaron para corregir foco recortado,
  composición de cabeceras móviles y estilos huérfanos.

`artifacts/design-review/index.html` compara antes/después;
`evidence.json` registra las observaciones y artefactos locales. El runner y las
instrucciones para reproducirlos están en [DESIGN_TOOLING.md](docs/DESIGN_TOOLING.md).
