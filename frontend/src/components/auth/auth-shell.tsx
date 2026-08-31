import { ShieldCheck } from "lucide-react";
import type { ReactNode } from "react";

type AuthShellProps = {
  title: string;
  subtitle: string;
  children: ReactNode;
};

export function AuthShell({ title, subtitle, children }: AuthShellProps) {
  return (
    <main className="min-h-screen bg-[#f5f7fb] text-slate-950">
      <div className="mx-auto grid min-h-screen w-full max-w-6xl grid-cols-1 lg:grid-cols-[0.95fr_1.05fr]">
        <section className="flex min-h-[280px] flex-col justify-between bg-[#101828] px-6 py-8 text-white sm:px-10 lg:min-h-screen">
          <div className="flex items-center gap-3">
            <div className="grid size-11 place-items-center rounded-lg bg-cyan-400 text-slate-950">
              <ShieldCheck aria-hidden="true" className="size-6" />
            </div>
            <div>
              <p className="text-base font-semibold">Security Center AI</p>
              <p className="text-sm text-slate-300">Copiloto de seguridad</p>
            </div>
          </div>

          <div className="max-w-xl py-12 lg:py-0">
            <p className="mb-4 text-sm font-medium uppercase tracking-[0.18em] text-cyan-300">
              MVP Frontend
            </p>
            <h1 className="text-4xl font-semibold leading-tight sm:text-5xl">
              Seguridad clara para usuarios comunes.
            </h1>
            <p className="mt-5 text-base leading-7 text-slate-300 sm:text-lg">
              Analiza riesgos, muestra un puntaje entendible y convierte hallazgos
              tecnicos en acciones concretas.
            </p>
          </div>

          <div className="grid grid-cols-3 gap-3 text-sm text-slate-300">
            <div className="border-t border-white/20 pt-3">JWT</div>
            <div className="border-t border-white/20 pt-3">Dashboard</div>
            <div className="border-t border-white/20 pt-3">Responsive</div>
          </div>
        </section>

        <section className="flex items-center justify-center px-4 py-10 sm:px-8">
          <div className="w-full max-w-md rounded-lg border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
            <div className="mb-8">
              <h2 className="text-2xl font-semibold text-slate-950">{title}</h2>
              <p className="mt-2 text-sm leading-6 text-slate-600">{subtitle}</p>
            </div>
            {children}
          </div>
        </section>
      </div>
    </main>
  );
}
