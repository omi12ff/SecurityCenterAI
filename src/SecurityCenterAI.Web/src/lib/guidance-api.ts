import "server-only";
import { ApiError, request } from "./api";
import { isDeviceId } from "./devices";
import { parseExplanation, parseFeedback, type Feedback } from "./guidance";

function path(deviceId: string, analysisId: string) {
  if (!isDeviceId(deviceId) || !isDeviceId(analysisId)) throw new ApiError(404, "No encontramos ese análisis en tu cuenta.");
  return `/api/devices/${deviceId}/analyses/${analysisId}`;
}
export async function fetchExplanation(deviceId: string, analysisId: string, token: string) {
  const result = parseExplanation(await request(`${path(deviceId, analysisId)}/explanation`, { token }));
  if (!result) throw new ApiError(502, "No pudimos consultar la guía de este análisis.");
  return result;
}
export async function saveFeedback(deviceId: string, analysisId: string, token: string, rating: Feedback["rating"], comment: string) {
  const result = parseFeedback(await request(`${path(deviceId, analysisId)}/feedback`, { token, body: { rating, comment } }));
  if (!result) throw new ApiError(502, "No pudimos confirmar tu valoración. Actualizá la página para consultar si se guardó.");
  return result;
}
