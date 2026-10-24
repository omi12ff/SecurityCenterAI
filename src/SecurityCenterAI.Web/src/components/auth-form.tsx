"use client";

import Link from "next/link";
import { useActionState, useState, type FormEvent } from "react";
import { loginAction, registerAction } from "@/app/actions";
import type { AuthFormState } from "@/lib/contracts";
import { Icon } from "./icons";

export function AuthForm({
  mode,
  notice,
}: {
  mode: "login" | "register";
  notice?: string;
}) {
  const isRegister = mode === "register";
  const [state, formAction, pending] = useActionState<AuthFormState, FormData>(
    isRegister ? registerAction : loginAction,
    {},
  );
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmation, setShowConfirmation] = useState(false);
  const errors = state.fieldErrors ?? {};
  const message = state.message && !Object.values(errors).includes(state.message)
    ? state.message
    : undefined;

  function validateConfirmation(event: FormEvent<HTMLFormElement>) {
    if (!isRegister) return;
    const form = event.currentTarget;
    const password = form.elements.namedItem("password") as HTMLInputElement | null;
    const confirmation = form.elements.namedItem("confirmPassword") as HTMLInputElement | null;
    if (!password || !confirmation) return;
    confirmation.setCustomValidity(
      confirmation.value && confirmation.value !== password.value
        ? "Las contraseñas no coinciden."
        : "",
    );
  }

  return (
    <form className="auth-form" aria-label={isRegister ? "Crear cuenta" : "Iniciar sesión"} action={formAction} onInput={validateConfirmation} aria-busy={pending}>
      {notice && !message && <p className="form-alert" role="status">{notice}</p>}
      {message && <p className="form-alert form-alert--error" role="alert"><Icon name="warning" /><span>{message}</span></p>}

      {isRegister && (
        <div className="field-group">
          <label className="field-label" htmlFor="register-name">Nombre completo</label>
          <div className="input-wrap">
            <Icon name="user" className="input-icon" />
            <input
              className="text-input"
              id="register-name"
              name="name"
              type="text"
              autoComplete="name"
              placeholder="Tu nombre y apellido"
              required
              minLength={2}
              maxLength={100}
              defaultValue={state.values?.name ?? ""}
              disabled={pending}
              aria-invalid={Boolean(errors.name)}
              aria-describedby={errors.name ? "name-error" : undefined}
            />
          </div>
          {errors.name && <p id="name-error" className="field-error" role="alert">{errors.name}</p>}
        </div>
      )}

      <div className="field-group">
        <label className="field-label" htmlFor={`${mode}-email`}>Correo electrónico</label>
        <div className="input-wrap">
          <Icon name="mail" className="input-icon" />
          <input
            className="text-input"
            id={`${mode}-email`}
            name="email"
            type="email"
            autoComplete="email"
            autoCapitalize="none"
            spellCheck={false}
            placeholder="nombre@ejemplo.com"
            required
            maxLength={320}
            defaultValue={state.values?.email ?? ""}
            disabled={pending}
            aria-invalid={Boolean(errors.email)}
            aria-describedby={errors.email ? "email-error" : undefined}
          />
        </div>
        {errors.email && <p id="email-error" className="field-error" role="alert">{errors.email}</p>}
      </div>

      <div className="field-group">
        <label className="field-label" htmlFor={`${mode}-password`}>Contraseña</label>
        <div className="input-wrap">
          <Icon name="lock" className="input-icon" />
          <input
            className="text-input"
            id={`${mode}-password`}
            name="password"
            type={showPassword ? "text" : "password"}
            autoComplete={isRegister ? "new-password" : "current-password"}
            placeholder={isRegister ? "Al menos 15 caracteres" : "Tu contraseña"}
            required
            minLength={isRegister ? 15 : undefined}
            maxLength={128}
            disabled={pending}
            aria-invalid={Boolean(errors.password)}
            aria-describedby={[isRegister ? "password-help" : "", errors.password ? "password-error" : ""].filter(Boolean).join(" ") || undefined}
          />
          <button
            className="password-toggle"
            type="button"
            onClick={() => setShowPassword(!showPassword)}
            aria-label={showPassword ? "Ocultar contraseña" : "Mostrar contraseña"}
            aria-pressed={showPassword}
            disabled={pending}
          >
            <Icon name={showPassword ? "eye-off" : "eye"} />
          </button>
        </div>
        {isRegister && <p id="password-help" className="input-help">Usá entre 15 y 128 caracteres. Podés usar una frase larga y pegar desde tu gestor.</p>}
        {errors.password && <p id="password-error" className="field-error" role="alert">{errors.password}</p>}
      </div>

      {isRegister && (
        <div className="field-group">
          <label className="field-label" htmlFor="register-confirm-password">Confirmar contraseña</label>
          <div className="input-wrap">
            <Icon name="lock" className="input-icon" />
            <input
              className="text-input"
              id="register-confirm-password"
              name="confirmPassword"
              type={showConfirmation ? "text" : "password"}
              autoComplete="new-password"
              placeholder="Volvé a escribir tu contraseña"
              required
              minLength={15}
              maxLength={128}
              disabled={pending}
              aria-invalid={Boolean(errors.confirmPassword)}
              aria-describedby={errors.confirmPassword ? "confirm-password-error" : undefined}
            />
            <button
              className="password-toggle"
              type="button"
              onClick={() => setShowConfirmation(!showConfirmation)}
              aria-label={showConfirmation ? "Ocultar confirmación de contraseña" : "Mostrar confirmación de contraseña"}
              aria-pressed={showConfirmation}
              disabled={pending}
            >
              <Icon name={showConfirmation ? "eye-off" : "eye"} />
            </button>
          </div>
          {errors.confirmPassword && <p id="confirm-password-error" className="field-error" role="alert">{errors.confirmPassword}</p>}
        </div>
      )}

      <button className="button button--primary button--full" type="submit" disabled={pending}>
        {pending ? <span className="spinner" aria-hidden="true" /> : null}
        <span>{pending ? (isRegister ? "Creando cuenta…" : "Iniciando sesión…") : (isRegister ? "Crear cuenta" : "Iniciar sesión")}</span>
        {!pending && <Icon name="arrow-right" className="button-icon" />}
      </button>
      {isRegister && <p className="input-help">Antes de probar la aplicación, consultá los borradores de <Link href="/privacy">privacidad</Link> y <Link href="/terms">términos</Link>. Sus datos del responsable y contacto todavía están pendientes.</p>}
      {!isRegister && <Link className="auth-switch" href="/forgot-password">¿Olvidaste tu contraseña?</Link>}
      <p className="auth-switch">
        {isRegister ? "¿Ya tenés una cuenta? " : "¿Todavía no tenés cuenta? "}
        <Link href={isRegister ? "/login" : "/register"}>{isRegister ? "Iniciar sesión" : "Crear cuenta"}</Link>
      </p>
    </form>
  );
}
