import type { Metadata } from "next";
import { InformationPage } from "@/components/information-page";
import { ProblemReportForm } from "@/components/problem-report-form";

export const metadata: Metadata = { title: "Ayuda y errores", referrer: "no-referrer" };

export default function ReportProblemPage() {
  return <InformationPage title="Ayuda y errores" description="Prepará un resumen del problema para revisarlo y decidir cómo compartirlo.">
    <aside className="information-draft" aria-label="Estado del canal de soporte"><strong>Canal de envío pendiente</strong><p>Todavía no hay un correo público o canal de recepción definido. Este formulario prepara un archivo en tu navegador: no crea un ticket, no envía correo ni comunica el problema al proyecto.</p></aside>
    <section><h2>Antes de preparar el reporte</h2><p>Indicá la pantalla, los pasos que seguiste, qué esperabas y qué ocurrió. Si citás un error, copiá solo el mensaje necesario y ocultá datos personales. No hace falta adjuntar una captura o un informe del equipo.</p><p>Si sospechás una vulnerabilidad, evitá publicarla o compartir instrucciones de explotación por un canal abierto. Coordiná un canal privado con quien administra esta prueba antes de enviar detalles; el proceso público de seguridad aún no está definido.</p></section>
    <ProblemReportForm />
    <section><h2>Cómo compartirlo después</h2><p>Revisá el JSON y acordá el destinatario y el canal antes de enviarlo por tu cuenta. Guardar o copiar el borrador no significa que alguien lo haya recibido. Podés limpiar la vista previa o cerrar la página para retirar el borrador de esta pantalla; tus descargas y el portapapeles se administran por separado.</p></section>
  </InformationPage>;
}
