"use server";

import { revalidatePath } from "next/cache";
import { ApiError } from "@/lib/api";
import { isDeviceId } from "@/lib/devices";
import { isRating, type FeedbackState } from "@/lib/guidance";
import { saveFeedback } from "@/lib/guidance-api";
import { requireSession, redirectToRenewal } from "@/lib/session";

export async function saveFeedbackAction(_previous: FeedbackState, data: FormData): Promise<FeedbackState> {
  const deviceId = data.get("deviceId"), analysisId = data.get("analysisId"), rating = data.get("rating");
  const comment = typeof data.get("comment") === "string" ? (data.get("comment") as string).trim() : "";
  if (!isDeviceId(deviceId) || !isDeviceId(analysisId) || !isRating(rating) || comment.length > 1000 || /[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f-\u009f]/.test(comment))
    return { message: "Elegí una valoración válida y un comentario de hasta 1000 caracteres." };
  const session = await requireSession("/devices");
  try {
    await saveFeedback(deviceId, analysisId, session.token, rating, comment);
    revalidatePath(`/devices/${deviceId}/analyses/${analysisId}`);
    return { success: true, message: "Tu valoración privada quedó guardada. Podés actualizarla cuando quieras." };
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/devices");
    return { message: error instanceof ApiError && error.status === 404 ? "No encontramos ese análisis en tu cuenta." : "No pudimos guardar tu valoración. Revisá los datos y volvé a intentar." };
  }
}
