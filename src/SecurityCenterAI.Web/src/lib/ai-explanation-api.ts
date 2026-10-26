import "server-only";
import { ApiError, request } from "./api";
import { isDeviceId } from "./devices";
import { parseAiExplanation, parseAiExplanationAvailability } from "./ai-explanation";

function path(deviceId: string, analysisId: string) {
  if (!isDeviceId(deviceId) || !isDeviceId(analysisId)) throw new ApiError(404, "No encontramos ese análisis en tu cuenta.");
  return `/api/devices/${deviceId}/analyses/${analysisId}/ai-explanation`;
}

export async function fetchAiExplanationAvailability(deviceId: string, analysisId: string, token: string) {
  const result = parseAiExplanationAvailability(await request(path(deviceId, analysisId), { token }));
  if (!result) throw new ApiError(502, "No pudimos consultar la disponibilidad de la explicación con IA.");
  return result;
}

export async function generateAiExplanation(deviceId: string, analysisId: string, token: string) {
  const result = parseAiExplanation(await request(path(deviceId, analysisId), { token, body: { confirmed: true }, timeoutMs: 65_000 }));
  if (!result) throw new ApiError(502, "La respuesta de IA no pudo validarse. Podés seguir usando la guía local.");
  return result;
}
