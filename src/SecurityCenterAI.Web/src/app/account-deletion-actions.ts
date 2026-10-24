"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { ApiError, request } from "@/lib/api";
import { validateAccountDeletion, type AccountDeletionState } from "@/lib/account-deletion";
import { deleteSession, requireSession, redirectToRenewal } from "@/lib/session";

export async function deleteAccountAction(_previous: AccountDeletionState, data: FormData): Promise<AccountDeletionState> {
  const validation = validateAccountDeletion(data);
  if (!validation.success) return validation.state;
  const session = await requireSession("/account");
  try {
    await request("/api/account", { method: "DELETE", token: session.token, body: validation.data });
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/account");
    if (error instanceof ApiError && error.status === 403)
      return { message: "No pudimos confirmar tu contraseña. Revisala o esperá antes de intentar de nuevo." };
    if (error instanceof ApiError && error.status === 429)
      return { message: "Hubo demasiados intentos. Esperá un momento antes de volver a intentar." };
    return { message: "No pudimos confirmar la eliminación. Revisá el estado de tu cuenta antes de volver a intentar." };
  }
  await deleteSession();
  revalidatePath("/account");
  revalidatePath("/dashboard");
  revalidatePath("/devices", "layout");
  redirect("/login?reason=account-deleted");
}
