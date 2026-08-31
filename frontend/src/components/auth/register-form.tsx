"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle, UserPlus } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { apiFetch } from "@/lib/api";
import { saveSession } from "@/lib/auth";
import type { AuthResponse } from "@/types/api";

const registerSchema = z.object({
  name: z.string().min(3, "Ingresa tu nombre completo."),
  email: z.string().email("Ingresa un correo valido."),
  password: z
    .string()
    .min(8, "Usa al menos 8 caracteres.")
    .regex(/[A-Z]/, "Incluye una letra mayuscula.")
    .regex(/[a-z]/, "Incluye una letra minuscula.")
    .regex(/[0-9]/, "Incluye un numero."),
});

type RegisterFormValues = z.infer<typeof registerSchema>;

export function RegisterForm() {
  const router = useRouter();
  const [serverError, setServerError] = useState("");
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { name: "", email: "", password: "" },
  });

  async function onSubmit(values: RegisterFormValues) {
    setServerError("");

    try {
      const auth = await apiFetch<AuthResponse>("/api/auth/register", {
        method: "POST",
        body: JSON.stringify(values),
      });
      saveSession(auth);
      router.push("/dashboard");
    } catch (error) {
      setServerError(
        error instanceof Error
          ? error.message
          : "No pudimos crear la cuenta. Revisa si el correo ya existe.",
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
        <label className="text-sm font-medium text-slate-800" htmlFor="name">
          Nombre completo
        </label>
        <input
          className="mt-2 h-11 w-full rounded-md border border-slate-300 px-3 text-sm outline-none transition focus:border-cyan-500 focus:ring-4 focus:ring-cyan-100"
          id="name"
          type="text"
          autoComplete="name"
          {...register("name")}
        />
        {errors.name ? <p className="mt-2 text-sm text-red-700">{errors.name.message}</p> : null}
      </div>

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
          autoComplete="new-password"
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
        <UserPlus aria-hidden="true" className="size-4" />
        {isSubmitting ? "Creando cuenta..." : "Crear cuenta"}
      </button>

      <p className="text-center text-sm text-slate-600">
        Ya tienes cuenta?{" "}
        <Link className="font-semibold text-[#155eef] hover:underline" href="/login">
          Iniciar sesion
        </Link>
      </p>
    </form>
  );
}
