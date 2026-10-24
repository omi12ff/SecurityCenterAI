export type AccountDeletionState = {
  message?: string;
  fieldErrors?: { password?: string; confirmation?: string };
};

export function validateAccountDeletion(data: FormData):
  | { success: true; data: { password: string; confirmation: "ELIMINAR" } }
  | { success: false; state: AccountDeletionState } {
  const password = data.get("password"), confirmation = data.get("confirmation");
  const fieldErrors: NonNullable<AccountDeletionState["fieldErrors"]> = {};
  if (typeof password !== "string" || !password || password.length > 128)
    fieldErrors.password = "Ingresá tu contraseña actual, de hasta 128 caracteres.";
  if (confirmation !== "ELIMINAR") fieldErrors.confirmation = "Escribí ELIMINAR exactamente para confirmar.";
  if (Object.keys(fieldErrors).length) return { success: false,
    state: { message: "Revisá la contraseña y la confirmación para continuar.", fieldErrors } };
  return { success: true, data: { password: password as string, confirmation: "ELIMINAR" } };
}
