import Link from "next/link";
import { InformationLinks } from "@/components/information-links";

export default function NotFoundPage() {
  return <main className="standalone-page"><section className="standalone-card"><p className="eyebrow">SECURITY CENTER AI</p><h1>Esta página no está disponible</h1><p>Podés volver al inicio para continuar.</p><Link className="button button--primary" href="/">Volver al inicio</Link><InformationLinks /></section></main>;
}
