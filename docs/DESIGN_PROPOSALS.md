# Propuestas de diseño para revisar

Investigación original: 2026-10-26. Luego el usuario autorizó instalar todas las
herramientas propuestas y aplicarlas al proyecto. Se adoptó **A con la lectura
guiada de B**; la implementación vigente se documenta en [DESIGN.md](../DESIGN.md).
Se conserva este archivo como registro de alternativas. No se subieron
capturas de cuentas personales, código o datos del proyecto a un servicio de diseño.

## Qué resolveríamos

SecurityCenterAI se dirige a una persona que quiere entender su PC. La primera
pantalla debería responder qué se observó, qué falta comprobar y dónde empezar.
Las capturas actuales muestran bloques de explicación muy largos y estados que
comparten tratamiento visual. La propuesta conserva la marca azul/verde agua,
los puntos confirmados y la cobertura como medidas distintas, y la IA como
ampliación opcional de un análisis. Es un criterio de diseño propio, no una
función nueva comprometida.

## Opciones

| Opción | Sensación | Organización propuesta | Ventaja | Compromiso |
| --- | --- | --- | --- | --- |
| **A. Centro Windows claro — recomendada** | Familiar, sobria, legible. | Resumen del equipo, puntos/cobertura separados, prioridades y diez comprobaciones con detalle desplegable. | Encaja con usuarios de Windows y permite evolucionar lo existente. | Hay que reducir texto visible sin ocultar incertidumbre o errores. |
| **B. Informe guiado** | Cercana y explicativa. | Qué se observó, por dónde empezar y detalle; explicación de IA en un bloque identificado dentro del informe. | Facilita una lectura sin vocabulario técnico. | Menos densidad; necesita mantener acceso rápido a evidencia y comparación. |
| **C. Panel de diagnóstico** | Precisa, compacta, con tema oscuro opcional. | Lista o tabla de comprobaciones, filtros por estado y panel de detalle. | Escaneo rápido y mejor crecimiento del historial. | Puede resultar técnica para principiantes; no la usaría como primera opción de la 1.0. |

## A. Centro Windows claro

Referencias para mirar:

- [Microsoft PC Manager](https://pcmanager.microsoft.com/en-US): referencia de
  organización por tareas de una herramienta para PC. Tomar la familiaridad y
  la presentación por categorías; sus acciones de limpieza/reparación no son
  funciones existentes de SecurityCenterAI.
- [Fluent 2: tarjetas](https://fluent2.microsoft.design/components/web/react/core/card/usage):
  referencia de jerarquía entre título, contenido breve y acciones. Usar una
  tarjeta por concepto, evitando tarjetas anidadas y párrafos completos en portada.
- [Fluent 2: acordeones](https://fluent2.microsoft.design/components/web/react/core/accordion/usage):
  referencia para exponer detalle progresivamente. La acción principal y el
  estado que requiere atención deben permanecer visibles.

Aplicación propuesta: cabecera compacta del equipo con fecha del informe;
resumen con dos métricas independientes; lista de prioridades; comprobaciones
con nombre, icono y texto de estado; evidencia y guía dentro del detalle. No se
presentaría un botón web de «Analizar ahora» que simule ejecutar el agente:
el cliente actual sigue siendo manual.

## B. Informe guiado

Referencias para mirar:

- [Malwarebytes: informe de salud](https://help.malwarebytes.com/hc/en-us/articles/31589681920923-Malwarebytes-Monthly-Health-Report):
  referencia de recorrido desde un resumen hacia el detalle y orientación final.
  Nuestro producto observa configuraciones; no puede copiar sus afirmaciones de
  detección o bloqueo de amenazas.
- [Fluent 2: mensajes](https://fluent2.microsoft.design/components/web/react/core/messagebar/usage):
  referencia para distinguir información, advertencia y error, con una acción
  de recuperación dentro del mismo contexto.

Aplicación propuesta: una introducción breve, resultados agrupados en
«Para revisar», «Sin determinar» y «Correctas», y un bloque identificado como
explicación generada con IA después de solicitarla. Las fuentes y acciones
manuales siguen en la guía local. La IA no se representa con una burbuja de chat.

## C. Panel de diagnóstico

Referencias para mirar:

- [Carbon: indicadores de estado](https://www.carbondesignsystem.com/building-blocks/core/patterns/status-indicators):
  referencia de símbolos y etiquetas además del color.
- [Carbon: tablas](https://www.carbondesignsystem.com/building-blocks/core/components/data-table/guidelines):
  referencia de filas legibles, detalle expandible y herramientas de consulta.

Aplicación propuesta: lista compacta en móvil; tabla o lista con panel lateral
en escritorio; filtro por los cuatro estados del agente y acceso directo a
comparar informes. El tema oscuro sería una decisión separada, no requisito
para adoptar la organización más compacta.

## Reglas compartidas que habría que aprobar

- Mantener «Puntos confirmados» y «Cobertura» separados. No convertir 75/100
  en un mensaje de «PC segura» o «75 % protegida».
- Diferenciar «Desconocido» de «Para revisar». El estado desconocido no confirma
  una protección desactivada. Cada estado necesita texto/icono, además del color.
- Una acción principal por sección. La ampliación de IA no desplaza la guía
  local ni inicia consultas al abrir una página.
- Conservar lenguaje español simple, fechas comprensibles, foco de teclado y
  etiquetas accesibles. Los mensajes de error explican la recuperación.
- Diseñar para 360 × 800 y 1440 × 900; comprobar también zoom al 200 %, etiquetas
  largas, resultados parciales y móvil sin desplazamiento horizontal.
- No prometer vigilancia de fondo, análisis remoto, reparación automática,
  protección antivirus, planes pagos o compatibilidad todavía no validada.

## Estados que se revisarían antes de implementar

| Estado | Qué tendría que comunicar | Acción o salida |
| --- | --- | --- |
| Sin equipo | Todavía no existe un equipo vinculado. | Ir a Primeros pasos o vinculación. |
| Sin análisis | El equipo existe pero no hay resultados. | Explicar el envío manual desde el agente. |
| Cargando IA | Hay una consulta solicitada, con espera visible. | Evitar doble envío; permitir seguir leyendo la guía. |
| Resultado parcial | Hay comprobaciones sin determinar. | Mostrar cobertura y acceso a esas comprobaciones. |
| Error | La operación no terminó; causa y alcance claros. | Reintentar cuando corresponde, conservar la guía. |
| Permiso o sesión | Falta sesión/correo o acceso al recurso. | Renovar sesión, verificar correo o volver al historial. |
| Éxito | Resultado recibido y vinculado a su informe original. | Revisar explicaciones y guía, sin recalcular puntos. |

Estas son propuestas de comportamiento y jerarquía para una revisión futura.
No se ejecutó el gate visual de un rediseño: no hay una implementación nueva
que evaluar, y no se afirmará lo contrario por haber investigado referencias.

## Decisiones aplicadas después de la investigación

Se conserva azul/verde agua con tema claro. Se refinan Resumen y Entender el
análisis, extendiendo la base compartida a dispositivos, Primeros pasos, acceso
y cuenta. La futura app de escritorio mantiene su alcance pendiente. Los
criterios, herramientas y evidencia local están en [DESIGN.md](../DESIGN.md).
