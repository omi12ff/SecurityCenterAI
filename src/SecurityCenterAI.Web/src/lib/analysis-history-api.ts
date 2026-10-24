import "server-only";
import { ApiError, request } from "./api";
import { isDeviceId } from "./devices";
import { parseAnalysisExport } from "./analysis-history";

function analysisPath(deviceId: string, analysisId: string): string {
  if (!isDeviceId(deviceId) || !isDeviceId(analysisId)) throw new ApiError(404, "No encontramos ese análisis en tu cuenta.");
  return `/api/devices/${deviceId}/analyses/${analysisId}`;
}

export async function exportAnalysis(deviceId: string, analysisId: string, token: string) {
  const result = parseAnalysisExport(await request(`${analysisPath(deviceId, analysisId)}/export`, { token }), deviceId, analysisId);
  if (!result) throw new ApiError(502, "No pudimos preparar una copia válida del análisis.");
  return result;
}

export async function deleteAnalysis(deviceId: string, analysisId: string, token: string): Promise<void> {
  await request(analysisPath(deviceId, analysisId), { token, method: "DELETE", body: { confirmed: true } });
}
