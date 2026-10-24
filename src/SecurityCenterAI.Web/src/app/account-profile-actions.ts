"use server";

import { revalidatePath } from "next/cache";
import { ApiError, request } from "@/lib/api";
import { validateProfileName, type ProfileNameState } from "@/lib/account-profile";
import { requireSession, redirectToRenewal } from "@/lib/session";

export async function updateProfileNameAction(_previous: ProfileNameState, data: FormData): Promise<ProfileNameState> {
  const validation = validateProfileName(data);
  if (!validation.success) return validation.state;
  const session = await requireSession("/account");
  try { await request("/api/account/profile/name", { token: session.token, body: { name: validation.name } }); }
  catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/account");
    if (error instanceof ApiError && error.status === 429)
      return { message: "Hubo demasiados intentos. Esperá un momento antes de volver a intentar." };
    return { message: "No pudimos confirmar el cambio. Actualizá el perfil para revisar el nombre guardado." };
  }
  revalidatePath("/account");
  revalidatePath("/dashboard");
  revalidatePath("/devices", "layout");
  return { success: true, message: "Tu nombre se actualizó." };
}
