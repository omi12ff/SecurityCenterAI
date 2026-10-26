# Información de privacidad, términos y ayuda

Implementado localmente el 2026-10-24 para la primera versión.

## Páginas y estado

- `/privacy`: descripción de cuenta, equipos, informes, feedback, cookies,
  revisión de enlaces/mensajes, explicación opcional de IA por análisis, archivos
  locales, exportación/borrado y conservación.
- `/terms`: alcance y límites de la prueba, diez comprobaciones, cuota del detector,
  uso autorizado del equipo, accesos y condiciones aún por definir.
- `/report-problem`: preparación local de un reporte; no recibe o envía tickets.

Se accede sin sesión desde la landing, registro/login, cuenta, dashboard,
dispositivos y páginas de error. Privacidad y términos muestran **Borrador para
pruebas locales**, revisión y datos pendientes. No inventan responsable, país,
contacto, legislación aplicable, plazos de copias, disponibilidad o soporte.
Registro ofrece enlaces informativos; no guarda aceptación de un borrador.

El usuario confirmó que responsable/país/correo no están definidos y pidió un
recordatorio. Se mantiene en [el checklist 1.0](RELEASE_1_0.md); estos borradores
no completan ese requisito para una publicación externa. También falta acordar
proveedores, conservación y condiciones del servicio, y revisar el texto final.

## Reporte de errores: datos y controles

El formulario pide sección, resumen, pasos, resultado esperado y observado.
La sección usa una lista cerrada. Los límites son 160/2000/1000/1000 caracteres
UTF-16; se rechazan controles ajenos a tabulación/saltos de línea. Requiere una
confirmación explícita de revisión de datos sensibles.

El JSON v1 contiene solo esos campos y la fecha UTC de preparación. No agrega
nombre, correo, cuenta, navegador, URL, logs, cookies, credenciales, capturas o
informes. El texto escrito sí puede contener información sensible: la confirmación
es una revisión del usuario, no una garantía de anonimización automática.

No hay endpoint, Server Action, envío de correo o persistencia. Editar invalida
la vista previa y la confirmación anterior; limpiar retira campos y vista previa.
Copiar usa el portapapeles y descargar crea un Blob local, con nombre fijo
`SecurityCenterAI-reporte-local.json`; su URL temporal se revoca después.
Portapapeles y descargas permanecen bajo control del usuario.

Los controles de texto no tienen atributos `name`; el formulario usa POST como
resguardo y el botón queda deshabilitado hasta hidratarse. Sin JavaScript se
muestra una alternativa y no se habilita un envío nativo de contenido. El texto
se muestra en controles React, sin interpretar HTML, shell o enlaces.

La página advierte que el canal público de recepción y el proceso privado de
vulnerabilidades aún no están definidos. No abre un issue, llama a una app externa
o dirige información a un correo ficticio. El usuario debe acordar el canal antes
de compartir la copia por su cuenta.

## Revisión y fuentes

El contenido deriva del [inventario técnico](../PRIVACY_NOTES.md), los flujos de
cuenta y las capacidades del agente. Se corrigieron referencias antiguas a doce
tablas, ausencia de edición de perfil y agente en preparación; el modelo actual
tiene dieciséis tablas tras incorporar la cola cifrada de correo. La página de
privacidad describe también ese material pendiente recuperable. La landing describe el servicio de actualizaciones y no
anuncia detección de reinicios pendientes, que el agente no implementa.

Se consultó el [checklist de transparencia del ICO](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/the-right-to-be-informed/what-privacy-information-should-we-provide/)
solo como referencia estructural sobre identidad/contacto, finalidades y conservación;
no determina la jurisdicción de este proyecto ni acredita cumplimiento.
El bloque inicial de páginas públicas no agregó servicios, dependencias o datos
a PostgreSQL.

La revisión del 2026-10-25 añade a privacidad la explicación opcional de IA,
desactivada por defecto. El texto usa condiciones: solo habría envío tras
configurar y habilitar el módulo y confirmar una consulta. El estado revisado
el 2026-10-26 registra la configuración del producto habilitada por el usuario,
con clave de producto y `openrouter/free`; la API ya se reinició con el alias
corregido y quedó habilitada para una prueba explícita desde la interfaz.
La lectura del catálogo autenticado no acredita inferencia real y no
se envió un análisis al proveedor. Describe el contexto reducido y las exclusiones, resultados temporales
sin historial de IA y condiciones externas aún por revisar. No cambia el estado
de borrador ni resuelve responsable, país, contacto o políticas finales.
[Contrato del módulo](AI_EXPLANATIONS.md).

Pruebas y limitaciones en [CHANGELOG.md](../CHANGELOG.md). La captura opcional de
estas páginas usa `E2E_CAPTURE_INFORMATION=1` con datos ficticios y salida ignorada.
