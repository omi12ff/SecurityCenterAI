# Security Center AI

Contexto para las herramientas de diseño. Fuente: README.md, PROJECT_CONTEXT.md,
ROADMAP.md y la guía del proyecto; no reemplaza esos documentos.

## Personas y tarea

Una persona que usa Windows quiere comprender las observaciones de su equipo,
distinguir qué revisar de qué no se pudo comprobar, y encontrar un siguiente
paso manual claro. La interfaz está en español y debe resultar accesible sin
conocimientos de seguridad informática.

## Producto disponible

Web Next.js/React con cuenta, dispositivos, resultados de diez comprobaciones,
puntos confirmados y cobertura separados, historial, comparación, guías locales,
feedback privado, exportación y borrado. Agente Windows portátil manual. IA
opcional por análisis mediante un botón; no es un chatbot y puede fallar.

## Límites que el diseño conserva

- Los puntos no son un porcentaje de protección. Desconocido no confirma un fallo.
- Mostrar fecha, cobertura e incertidumbre; no inventar telemetría o actividad.
- No existe vigilancia continua, reparación automática o protección garantizada.
- No iniciar consultas de IA o acciones en Windows al abrir una pantalla.
- Conservar aviso de datos junto al botón de IA, confirmaciones destructivas,
  autenticación, autorización y resultados originales.
- Responsable, país y contacto públicos siguen pendientes. No inventarlos.

## Encargo actual

El usuario autorizó instalar y aplicar las herramientas de UX/UI. Refinar la
identidad existente: azul profundo, verde agua, superficies claras y una lectura
guiada. Prioridad: Resumen y Entender el análisis; extender los estilos compartidos
a dispositivos, cuenta, Primeros pasos y acceso. No cambiar el backend por estética.

## Entorno

Next.js 16 / React 19 / TypeScript / CSS propio. Preferir componentes, iconos y
patrones existentes; leer las guías del Next instalado antes de modificar APIs.
Verificación local con Chromium/Playwright y servicios/base temporales. Capturas
de cuentas ficticias. No publicar, crear commits o subir información personal.
