import { isDeviceId, parseDeviceAnalysis, type DeviceAnalysis } from "./devices.ts";
import { parseFeedback, type Feedback } from "./guidance.ts";

export type AnalysisExport = {
  exportVersion: 1;
  exportedAtUtc: string;
  device: { id: string; name: string };
  analysis: Omit<DeviceAnalysis, "deviceId">;
  feedback: Feedback | null;
};
export type AnalysisHistoryState = { message?: string };

// Cada nivel se reconstruye para que la descarga contenga únicamente el contrato público.
export function parseAnalysisExport(value: unknown, deviceId: string, analysisId: string): AnalysisExport | null {
  if (!isDeviceId(deviceId) || !isDeviceId(analysisId) || !value || typeof value !== "object") return null;
  const item = value as Record<string, unknown>;
  if (item.exportVersion !== 1 || typeof item.exportedAtUtc !== "string" || !Number.isFinite(Date.parse(item.exportedAtUtc)) ||
    !item.device || typeof item.device !== "object") return null;
  const device = item.device as Record<string, unknown>;
  if (!isDeviceId(device.id) || device.id.toLowerCase() !== deviceId.toLowerCase() ||
    typeof device.name !== "string" || !device.name.trim() || device.name.length > 100) return null;
  const analysis = parseDeviceAnalysis(item.analysis);
  const feedback = item.feedback === null ? null : parseFeedback(item.feedback);
  if (!analysis || analysis.id.toLowerCase() !== analysisId.toLowerCase() ||
    (analysis.deviceId !== null && analysis.deviceId.toLowerCase() !== deviceId.toLowerCase()) ||
    (item.feedback !== null && !feedback)) return null;
  const { deviceId: _deviceId, ...exportedAnalysis } = analysis;
  return { exportVersion: 1, exportedAtUtc: item.exportedAtUtc, device: { id: device.id, name: device.name }, analysis: exportedAnalysis, feedback };
}
