"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle, LogIn } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { apiFetch } from "@/lib/api";
import { saveSession } from "@/lib/auth";
import type { AuthResponse } from "@/types/api";

const loginSchema = z.object({
  email: z.string().email("Ingresa un correo valido."),
  password: z.string().min(1, "La contrasena es obligatoria."),
});

type LoginFormValues = z.infer<typeof loginSchema>;

export function LoginForm() {
  const router = useRouter();
  const [serverError, setServerError] = useState("");
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "" },
  });

  async function onSubmit(values: LoginFormValues) {
    setServerError("");

    try {
      const auth = await apiFetch<AuthResponse>("/api/auth/login", {
        method: "POST",
        body: JSON.stringify(values),
      });
      saveSession(auth);
      router.push("/dashboard");
    } catch (error) {
      setServerError(
        error instanceof Error
          ? error.message
          : "Credenciales incorrectas. Revisa los datos e intentalo de nuevo.",
      );
    }
  }

  return (
    <form className="space-y-5" onSubmit={handleSubmit(onSubmit)}>
      {serverError ? (
        <div className="flex gap-3 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">
          <AlertCircle aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
          <span>{serverError}</span>
        </div>
      ) : null}

      <div>
        <label className="text-sm font-medium text-slate-800" htmlFor="email">
          Correo electronico
        </label>
        <input
          className="mt-2 h-11 w-full rounded-md border border-slate-300 px-3 text-sm outline-none transition focus:border-cyan-500 focus:ring-4 focus:ring-cyan-100"
          id="email"
          type="email"
          autoComplete="email"
          {...register("email")}
        />
        {errors.email ? <p className="mt-2 text-sm text-red-700">{errors.email.message}</p> : null}
      </div>

      <div>
        <label className="text-sm font-medium text-slate-800" htmlFor="password">
          Contrasena
        </label>
        <input
          className="mt-2 h-11 w-full rounded-md border border-slate-300 px-3 text-sm outline-none transition focus:border-cyan-500 focus:ring-4 focus:ring-cyan-100"
          id="password"
          type="password"
          autoComplete="current-password"
          {...register("password")}
        />
        {errors.password ? (
          <p className="mt-2 text-sm text-red-700">{errors.password.message}</p>
        ) : null}
      </div>

      <button
        className="flex h-11 w-full items-center justify-center gap-2 rounded-md bg-[#155eef] px-4 text-sm font-semibold text-white transition hover:bg-[#004eeb] disabled:cursor-not-allowed disabled:bg-slate-400"
        type="submit"
        disabled={isSubmitting}
      >
        <LogIn aria-hidden="true" className="size-4" />
        {isSubmitting ? "Ingresando..." : "Iniciar sesion"}
      </button>

      <p className="text-center text-sm text-slate-600">
        No tienes cuenta?{" "}
        <Link className="font-semibold text-[#155eef] hover:underline" href="/register">
          Crear cuenta
        </Link>
      </p>
    </form>
  );
}
