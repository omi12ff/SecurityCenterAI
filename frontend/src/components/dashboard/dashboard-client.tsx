"use client";

import {
  AlertTriangle,
  BarChart3,
  CheckCircle2,
  History,
  Loader2,
  LogOut,
  MonitorCog,
  ShieldAlert,
  ShieldCheck,
  UserRound,
} from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { API_URL, apiFetch } from "@/lib/api";
import { clearSession, getAccessToken, getCurrentUser } from "@/lib/auth";
import { normalizeDashboard } from "@/lib/dashboard";
import type { AuthUser, DashboardSummary, RecentAnalysis } from "@/types/api";

const statusStyles: Record<RecentAnalysis["status"], string> = {
  safe: "bg-emerald-50 text-emerald-700 border-emerald-200",
  warning: "bg-amber-50 text-amber-700 border-amber-200",
  danger: "bg-red-50 text-red-700 border-red-200",
  unknown: "bg-slate-50 text-slate-700 border-slate-200",
};

function scoreCopy(score: number | null) {
  if (score === null) {
    return { label: "Sin datos", helper: "Ejecuta un analisis para calcular el puntaje." };
  }

  if (score >= 80) {
    return { label: "Proteccion saludable", helper: "Mantienes buenos habitos de seguridad." };
  }

  if (score >= 50) {
    return { label: "Atencion recomendada", helper: "Hay ajustes importantes por revisar." };
  }

  return { label: "Riesgo alto", helper: "Prioriza las recomendaciones criticas." };
}

export function DashboardClient() {
  const router = useRouter();
  const [user] = useState<AuthUser | null>(() =>
    typeof window === "undefined" ? null : getCurrentUser(),
  );
  const [dashboard, setDashboard] = useState<DashboardSummary | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const token = getAccessToken();
    if (!token) {
      router.replace("/login");
      return;
    }

    async function loadDashboard() {
      try {
        const data = await apiFetch<Record<string, unknown>>("/api/dashboard", {
          headers: { Authorization: `Bearer ${token}` },
        });
        setDashboard(normalizeDashboard(data));
      } catch (requestError) {
        setError(
          requestError instanceof Error
            ? requestError.message
            : "No se pudo cargar el dashboard.",
        );
      } finally {
        setIsLoading(false);
      }
    }

    void loadDashboard();
  }, [router]);

  const score = dashboard?.securityScore ?? null;
  const scoreInfo = useMemo(() => scoreCopy(score), [score]);
  const scoreWidth = score === null ? 0 : Math.max(0, Math.min(score, 100));

  function logout() {
    clearSession();
    router.replace("/login");
  }

  return (
    <main className="min-h-screen bg-[#f5f7fb] text-slate-950">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-7xl flex-col gap-4 px-4 py-4 sm:px-6 md:flex-row md:items-center md:justify-between">
          <div className="flex items-center gap-3">
            <div className="grid size-11 place-items-center rounded-lg bg-[#101828] text-cyan-300">
              <ShieldCheck aria-hidden="true" className="size-6" />
            </div>
            <div>
              <p className="text-base font-semibold">Security Center AI</p>
              <p className="text-sm text-slate-600">
                {user?.name ? `Sesion de ${user.name}` : "Dashboard de seguridad"}
              </p>
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Link
              className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-slate-300 bg-white px-4 text-sm font-semibold text-slate-800 transition hover:bg-slate-50"
              href="/profile"
            >
              <UserRound aria-hidden="true" className="size-4" />
              Perfil
            </Link>
            <button
              className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-slate-300 bg-white px-4 text-sm font-semibold text-slate-800 transition hover:bg-slate-50"
              type="button"
              onClick={logout}
            >
              <LogOut aria-hidden="true" className="size-4" />
              Salir
            </button>
          </div>
        </div>
      </header>

      <div className="mx-auto w-full max-w-7xl px-4 py-6 sm:px-6 lg:py-8">
        {isLoading ? (
          <div className="flex min-h-[420px] items-center justify-center rounded-lg border border-slate-200 bg-white">
            <div className="flex items-center gap-3 text-slate-700">
              <Loader2 aria-hidden="true" className="size-5 animate-spin" />
              Cargando dashboard...
            </div>
          </div>
        ) : error ? (
          <div className="rounded-lg border border-red-200 bg-red-50 p-5 text-red-800">
            <div className="flex items-start gap-3">
              <AlertTriangle aria-hidden="true" className="mt-0.5 size-5 shrink-0" />
              <div>
                <h1 className="text-base font-semibold">No se pudo conectar con la API</h1>
                <p className="mt-1 text-sm leading-6">{error}</p>
                <p className="mt-3 text-sm leading-6">
                  Verifica que el backend este activo en{" "}
                  <code className="rounded bg-red-100 px-1.5 py-0.5">{API_URL}</code>
                  {" "}o actualiza <code className="rounded bg-red-100 px-1.5 py-0.5">.env.local</code>.
                </p>
              </div>
            </div>
          </div>
        ) : (
          <div className="space-y-6">
            <section className="grid grid-cols-1 gap-4 lg:grid-cols-[1.4fr_0.8fr]">
              <div className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
                <div className="flex flex-col gap-6 sm:flex-row sm:items-end sm:justify-between">
                  <div>
                    <p className="text-sm font-medium uppercase tracking-[0.16em] text-[#155eef]">
                      Puntaje de seguridad
                    </p>
                    <h1 className="mt-3 text-3xl font-semibold sm:text-4xl">
                      {score === null ? "--" : score}
                      <span className="text-lg text-slate-500"> / 100</span>
                    </h1>
                    <p className="mt-2 text-base font-medium text-slate-800">{scoreInfo.label}</p>
                    <p className="mt-1 text-sm leading-6 text-slate-600">{scoreInfo.helper}</p>
                  </div>
                  <div className="grid size-28 place-items-center rounded-lg bg-cyan-50 text-cyan-700">
                    <ShieldAlert aria-hidden="true" className="size-12" />
                  </div>
                </div>
                <div className="mt-6 h-3 overflow-hidden rounded-full bg-slate-100">
                  <div
                    className="h-full rounded-full bg-[#155eef] transition-all"
                    style={{ width: `${scoreWidth}%` }}
                  />
                </div>
              </div>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-1">
                <div className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
                  <div className="flex items-center justify-between">
                    <p className="text-sm font-medium text-slate-600">Analisis totales</p>
                    <BarChart3 aria-hidden="true" className="size-5 text-[#155eef]" />
                  </div>
                  <p className="mt-4 text-3xl font-semibold">{dashboard?.totalAnalyses ?? 0}</p>
                </div>
                <div className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
                  <div className="flex items-center justify-between">
                    <p className="text-sm font-medium text-slate-600">Estado del MVP</p>
                    <CheckCircle2 aria-hidden="true" className="size-5 text-emerald-600" />
                  </div>
                  <p className="mt-4 text-xl font-semibold">Frontend conectado</p>
                </div>
              </div>
            </section>

            <section className="rounded-lg border border-cyan-200 bg-cyan-50 p-5 text-cyan-950 shadow-sm">
              <div className="flex items-start gap-3">
                <MonitorCog aria-hidden="true" className="mt-0.5 size-5 shrink-0" />
                <div>
                  <h2 className="font-semibold">Version web temprana</h2>
                  <p className="mt-2 text-sm leading-6">
                    Esta interfaz web ya puede manejar cuenta, sesion, dashboard e historial.
                    El navegador no puede inspeccionar por si solo antivirus, firewall o archivos
                    locales; esas senales llegaran cuando exista el Desktop Agent.
                  </p>
                </div>
              </div>
            </section>

            <section className="rounded-lg border border-slate-200 bg-white shadow-sm">
              <div className="flex items-center gap-3 border-b border-slate-200 px-5 py-4">
                <History aria-hidden="true" className="size-5 text-[#155eef]" />
                <h2 className="text-base font-semibold">Historial reciente</h2>
              </div>

              {dashboard?.recentAnalyses.length ? (
                <div className="divide-y divide-slate-200">
                  {dashboard.recentAnalyses.map((analysis) => (
                    <article
                      className="grid gap-3 px-5 py-4 sm:grid-cols-[1fr_auto] sm:items-center"
                      key={analysis.id}
                    >
                      <div>
                        <h3 className="font-medium text-slate-950">{analysis.title}</h3>
                        <p className="mt-1 text-sm leading-6 text-slate-600">{analysis.summary}</p>
                        {analysis.createdAt ? (
                          <p className="mt-2 text-xs text-slate-500">{analysis.createdAt}</p>
                        ) : null}
                      </div>
                      <div className="flex items-center gap-2 sm:justify-end">
                        {analysis.score !== null ? (
                          <span className="rounded-md border border-slate-200 bg-white px-2.5 py-1 text-xs font-semibold text-slate-700">
                            {analysis.score}/100
                          </span>
                        ) : null}
                        <span
                          className={`w-fit rounded-md border px-2.5 py-1 text-xs font-semibold ${statusStyles[analysis.status]}`}
                        >
                          {analysis.status}
                        </span>
                      </div>
                    </article>
                  ))}
                </div>
              ) : (
                <div className="px-5 py-12 text-center">
                  <div className="mx-auto grid size-12 place-items-center rounded-lg bg-slate-100 text-slate-500">
                    <History aria-hidden="true" className="size-6" />
                  </div>
                  <h3 className="mt-4 text-base font-semibold">Aun no hay analisis</h3>
                  <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-slate-600">
                    Cuando el agente o el backend envien resultados, apareceran aqui con su
                    estado y resumen.
                  </p>
                </div>
              )}
            </section>
          </div>
        )}
      </div>
    </main>
  );
}
