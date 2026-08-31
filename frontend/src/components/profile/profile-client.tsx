"use client";

import {
  AlertTriangle,
  ArrowLeft,
  CalendarDays,
  Loader2,
  LogOut,
  Mail,
  MonitorCog,
  ShieldCheck,
  UserRound,
} from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { apiFetch } from "@/lib/api";
import { clearSession, getAccessToken } from "@/lib/auth";
import type { ProfileResponse } from "@/types/api";

function formatDate(value: string) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat("es", {
    dateStyle: "long",
    timeStyle: "short",
  }).format(date);
}

export function ProfileClient() {
  const router = useRouter();
  const [profile, setProfile] = useState<ProfileResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const token = getAccessToken();

    if (!token) {
      router.replace("/login");
      return;
    }

    async function loadProfile() {
      try {
        const data = await apiFetch<ProfileResponse>("/api/profile", {
          headers: { Authorization: `Bearer ${token}` },
        });
        setProfile(data);
      } catch (requestError) {
        setError(
          requestError instanceof Error ? requestError.message : "No se pudo cargar el perfil.",
        );
      } finally {
        setIsLoading(false);
      }
    }

    void loadProfile();
  }, [router]);

  function logout() {
    clearSession();
    router.replace("/login");
  }

  return (
    <main className="min-h-screen bg-[#f5f7fb] text-slate-950">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-4 sm:px-6">
          <Link
            className="inline-flex h-10 items-center gap-2 rounded-md px-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-100"
            href="/dashboard"
          >
            <ArrowLeft aria-hidden="true" className="size-4" />
            Dashboard
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
      </header>

      <div className="mx-auto w-full max-w-5xl px-4 py-6 sm:px-6 lg:py-8">
        {isLoading ? (
          <div className="flex min-h-[340px] items-center justify-center rounded-lg border border-slate-200 bg-white">
            <div className="flex items-center gap-3 text-slate-700">
              <Loader2 aria-hidden="true" className="size-5 animate-spin" />
              Cargando perfil...
            </div>
          </div>
        ) : error ? (
          <div className="rounded-lg border border-red-200 bg-red-50 p-5 text-red-800">
            <div className="flex items-start gap-3">
              <AlertTriangle aria-hidden="true" className="mt-0.5 size-5 shrink-0" />
              <div>
                <h1 className="text-base font-semibold">No se pudo cargar el perfil</h1>
                <p className="mt-1 text-sm leading-6">{error}</p>
              </div>
            </div>
          </div>
        ) : profile ? (
          <div className="space-y-6">
            <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
              <div className="flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-center gap-4">
                  <div className="grid size-14 place-items-center rounded-lg bg-[#101828] text-cyan-300">
                    <UserRound aria-hidden="true" className="size-7" />
                  </div>
                  <div>
                    <p className="text-sm font-medium uppercase tracking-[0.16em] text-[#155eef]">
                      Perfil
                    </p>
                    <h1 className="mt-1 text-2xl font-semibold">{profile.name}</h1>
                  </div>
                </div>
                <span className="inline-flex w-fit items-center gap-2 rounded-md border border-emerald-200 bg-emerald-50 px-3 py-1.5 text-sm font-semibold text-emerald-700">
                  <ShieldCheck aria-hidden="true" className="size-4" />
                  Cuenta activa
                </span>
              </div>
            </section>

            <section className="grid grid-cols-1 gap-4 md:grid-cols-2">
              <article className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
                <div className="flex items-center gap-3 text-slate-600">
                  <Mail aria-hidden="true" className="size-5 text-[#155eef]" />
                  <p className="text-sm font-medium">Correo electronico</p>
                </div>
                <p className="mt-4 break-all text-lg font-semibold">{profile.email}</p>
              </article>

              <article className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
                <div className="flex items-center gap-3 text-slate-600">
                  <CalendarDays aria-hidden="true" className="size-5 text-[#155eef]" />
                  <p className="text-sm font-medium">Cuenta creada</p>
                </div>
                <p className="mt-4 text-lg font-semibold">{formatDate(profile.createdAtUtc)}</p>
              </article>
            </section>

            <section className="rounded-lg border border-amber-200 bg-amber-50 p-5 text-amber-950">
              <div className="flex items-start gap-3">
                <MonitorCog aria-hidden="true" className="mt-0.5 size-5 shrink-0" />
                <div>
                  <h2 className="font-semibold">Alcance de esta version web</h2>
                  <p className="mt-2 text-sm leading-6">
                    Desde el navegador podemos mostrar cuenta, dashboard, historial y formularios.
                    El analisis automatico del equipo todavia necesita el Desktop Agent para leer
                    informacion local como antivirus, firewall o actualizaciones.
                  </p>
                </div>
              </div>
            </section>
          </div>
        ) : null}
      </div>
    </main>
  );
}
