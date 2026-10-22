import type { AuthFormState } from "./contracts";
export type { AuthFormState } from "./contracts";

type Credentials = { name?: string; email: string; password: string };
type ValidationState = AuthFormState & {
  message: string;
  fieldErrors: NonNullable<AuthFormState["fieldErrors"]>;
  values: { name?: string; email: string };
};
type ValidationResult =
  | { success: true; data: Credentials }
  | { success: false; state: ValidationState };

export function validateCredentials(mode: "login" | "register", formData: FormData): ValidationResult {
  const text = (key: string) => {
    const value = formData.get(key);
    return typeof value === "string" ? value : "";
  };
  const name = text("name").trim();
  const email = text("email").trim().toLowerCase();
  const password = text("password");
  const errors: NonNullable<AuthFormState["fieldErrors"]> = {};

  if (!email || email.length > 320 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    errors.email = "Ingresá un correo electrónico válido.";
  }
  if (!password) errors.password = "Ingresá tu contraseña.";
  if (password.length > 128) errors.password = "Usá una contraseña de hasta 128 caracteres.";
  if (mode === "register") {
    if (name.length < 2 || name.length > 100) errors.name = "Usá un nombre de entre 2 y 100 caracteres.";
    if (password.length < 8) errors.password = "Usá al menos 8 caracteres.";
    if (text("confirmPassword") !== password) errors.confirmPassword = "Las contraseñas deben coincidir.";
  }
  if (Object.keys(errors).length) {
    return {
      success: false,
      state: {
        message: "Revisá los campos indicados para continuar.",
        fieldErrors: errors,
        values: mode === "register" ? { name, email } : { email },
      },
    };
  }
  return { success: true, data: mode === "register" ? { name, email, password } : { email, password } };
}
