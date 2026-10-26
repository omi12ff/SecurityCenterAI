"use server";

import { ApiError } from "@/lib/api";
import { validateAiExplanationRequest, type AiExplanationState } from "@/lib/ai-explanation";
import { generateAiExplanation } from "@/lib/ai-explanation-api";
import { requireSession, redirectToRenewal } from "@/lib/session";

export async function generateAiExplanationAction(_previous: AiExplanationState, data: FormData): Promise<AiExplanationState> {
  const input = validateAiExplanationRequest(data);
  if (!input) return { message: "Usá el botón Ampliar explicación con IA desde el análisis que querés consultar." };
  const session = await requireSession("/devices");
  try {
    return { response: await generateAiExplanation(input.deviceId, input.analysisId, session.token) };
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/devices");
    const message = error instanceof ApiError && error.status === 404 ? "No encontramos ese análisis en tu cuenta. Volvé al historial para comprobarlo." :
      error instanceof ApiError && error.status === 403 ? "Verificá tu correo desde Mi cuenta antes de solicitar una explicación con IA." :
      error instanceof ApiError && error.status === 429 ? "Se alcanzó el límite diario o hay otra explicación en curso. Esperá antes de volver a intentar." :
      error instanceof ApiError && error.status === 400 ? "No pudimos solicitar esa explicación. Actualizá la página y volvé a usar el botón." :
      "No pudimos ampliar la explicación con IA. Podés volver a intentar o continuar con la guía local de abajo.";
    return { message };
  }
}
