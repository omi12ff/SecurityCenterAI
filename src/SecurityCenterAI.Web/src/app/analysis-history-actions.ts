"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { ApiError } from "@/lib/api";
import { deleteAnalysis } from "@/lib/analysis-history-api";
import type { AnalysisHistoryState } from "@/lib/analysis-history";
import { isDeviceId } from "@/lib/devices";
import { requireSession, redirectToRenewal } from "@/lib/session";

export async function deleteAnalysisAction(_previous: AnalysisHistoryState, data: FormData): Promise<AnalysisHistoryState> {
  const deviceId = data.get("deviceId"), analysisId = data.get("analysisId");
  if (!isDeviceId(deviceId) || !isDeviceId(analysisId)) return { message: "No encontramos ese análisis en tu cuenta." };
  if (data.get("confirmed") !== "yes") return { message: "Confirmá que querés eliminar este análisis para continuar." };
  const session = await requireSession("/devices");
  try {
    await deleteAnalysis(deviceId, analysisId, session.token);
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/devices");
    return { message: error instanceof ApiError && error.status === 404
      ? "No encontramos ese análisis en tu cuenta. Puede haberse eliminado; actualizá el historial del equipo."
      : "No pudimos confirmar la eliminación. Actualizá el historial antes de volver a intentar." };
  }
  revalidatePath("/dashboard");
  revalidatePath("/devices");
  revalidatePath(`/devices/${deviceId}`);
  revalidatePath(`/devices/${deviceId}/compare`);
  revalidatePath(`/devices/${deviceId}/analyses/${analysisId}`);
  redirect(`/devices/${deviceId}?deleted=1`);
}
