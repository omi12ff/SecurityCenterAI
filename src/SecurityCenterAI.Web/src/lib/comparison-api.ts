import "server-only";
import { ApiError, request } from "./api";
import { isDeviceId } from "./devices";
import { parseAnalysisComparison } from "./comparisons";

export async function fetchAnalysisComparison(deviceId: string, baselineId: string, targetId: string, token: string) {
  if (![deviceId, baselineId, targetId].every(isDeviceId)) throw new ApiError(404, "No encontramos esos análisis en tu cuenta.");
  let value;
  try {
    const query = new URLSearchParams({ baselineId, targetId });
    value = await request(`/api/devices/${deviceId}/analyses/compare?${query}`, { token });
  } catch (error) {
    if (error instanceof ApiError && error.status === 400) throw new ApiError(400, "Elegí dos análisis distintos y ordenalos por la fecha de recepción y de sus comprobaciones: primero el anterior.");
    throw error;
  }
  const comparison = parseAnalysisComparison(value);
  if (!comparison || comparison.deviceId.toLowerCase() !== deviceId.toLowerCase() ||
    comparison.baseline.id.toLowerCase() !== baselineId.toLowerCase() || comparison.target.id.toLowerCase() !== targetId.toLowerCase()) {
    throw new ApiError(502, "No pudimos consultar la comparación de estos análisis.");
  }
  return comparison;
}
