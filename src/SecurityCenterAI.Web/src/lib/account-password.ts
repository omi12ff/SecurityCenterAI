import { validNewPasswordLength, NEW_PASSWORD_HELP } from "./new-password.ts";
export type PasswordChangeState = { message?: string; fieldErrors?: Partial<Record<"currentPassword" | "newPassword" | "confirmNewPassword" | "confirmed", string>> };

export function validatePasswordChange(data: FormData):
  | { success: true; data: { currentPassword: string; newPassword: string; confirmed: true } }
  | { success: false; state: PasswordChangeState } {
  const current = data.get("currentPassword"), next = data.get("newPassword"), repeated = data.get("confirmNewPassword");
  const fieldErrors: NonNullable<PasswordChangeState["fieldErrors"]> = {};
  if (typeof current !== "string" || !current || current.length > 128)
    fieldErrors.currentPassword = "Ingresá tu contraseña actual, de hasta 128 caracteres.";
  if (!validNewPasswordLength(next))
    fieldErrors.newPassword = NEW_PASSWORD_HELP;
  else if (next === current) fieldErrors.newPassword = "Elegí una contraseña diferente de la actual.";
  if (typeof repeated !== "string" || repeated !== next)
    fieldErrors.confirmNewPassword = "Las contraseñas nuevas deben coincidir.";
  if (data.get("confirmed") !== "yes") fieldErrors.confirmed = "Confirmá el cierre de las sesiones web.";
  if (Object.keys(fieldErrors).length) return { success: false, state: { message: "Revisá los campos para continuar.", fieldErrors } };
  return { success: true, data: { currentPassword: current as string, newPassword: next as string, confirmed: true } };
}
