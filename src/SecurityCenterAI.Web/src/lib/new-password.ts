// Length feedback only; the API owns the local denylist.
export const NEW_PASSWORD_HELP = "Usá entre 15 y 128 caracteres y evitá contraseñas comunes o previsibles.";
export function validNewPasswordLength(value: unknown): value is string {
  return typeof value === "string" && !!value.trim() && value.length <= 128 && Array.from(value).length >= 15;
}
