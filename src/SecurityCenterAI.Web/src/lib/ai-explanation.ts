import { isDeviceId } from "./devices.ts";

export type AiExplanationAvailability = {
  available: boolean; code: string; message: string; provider: string | null;
  model: string | null; dailyLimit: number; dataDisclosure: string;
};
export type AiExplanationResponse = {
  method: "ai-explanation" | "local-guide";
  status: "generated" | "disabled" | "unconfigured" | "unavailable" | "invalid-response" | "unsupported";
  explanationVersion: 1; summary: string; checks: { ruleId: string; explanation: string }[];
  provider: string | null; model: string | null; generatedAtUtc: string; notice: string; failureCode?: string | null;
};
export type AiExplanationState = { response?: AiExplanationResponse; message?: string };

const isText = (value: unknown, limit: number): value is string =>
  typeof value === "string" && value.trim().length > 0 && value.length <= limit &&
  !/[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f-\u009f]/.test(value);
const nullableLabel = (value: unknown): value is string | null => value === null || isText(value, 200);
const statuses: AiExplanationResponse["status"][] = ["generated", "disabled", "unconfigured", "unavailable", "invalid-response", "unsupported"];

export function parseAiExplanationAvailability(value: unknown): AiExplanationAvailability | null {
  if (!value || typeof value !== "object") return null;
  const v = value as Record<string, unknown>;
  if (typeof v.available !== "boolean" || !isText(v.code, 80) || !isText(v.message, 2000) ||
    !nullableLabel(v.provider) || !nullableLabel(v.model) || !Number.isInteger(v.dailyLimit) ||
    (v.dailyLimit as number) < 0 || (v.dailyLimit as number) > 10000 || !isText(v.dataDisclosure, 4000)) return null;
  return { available: v.available, code: v.code, message: v.message, provider: v.provider, model: v.model,
    dailyLimit: v.dailyLimit as number, dataDisclosure: v.dataDisclosure };
}

export function parseAiExplanation(value: unknown): AiExplanationResponse | null {
  if (!value || typeof value !== "object") return null;
  const v = value as Record<string, unknown>;
  if (!["ai-explanation", "local-guide"].includes(v.method as string) || !statuses.includes(v.status as AiExplanationResponse["status"]) ||
    (v.method === "ai-explanation") !== (v.status === "generated") || v.explanationVersion !== 1 ||
    !isText(v.summary, 6000) || !isText(v.notice, 4000) || !nullableLabel(v.provider) || !nullableLabel(v.model) ||
    typeof v.generatedAtUtc !== "string" || !Number.isFinite(Date.parse(v.generatedAtUtc)) ||
    !Array.isArray(v.checks) || v.checks.length > 100 || (v.status === "generated" && v.checks.length === 0)) return null;
  const checks: AiExplanationResponse["checks"] = [];
  for (const item of v.checks) {
    if (!item || typeof item !== "object") return null;
    const check = item as Record<string, unknown>;
    if (!isText(check.ruleId, 160) || !isText(check.explanation, 4000) || checks.some(c => c.ruleId === check.ruleId)) return null;
    checks.push({ ruleId: check.ruleId, explanation: check.explanation });
  }
  // Explicit copy: provider diagnostics and unrecognized fields never reach the browser.
  return { method: v.method as AiExplanationResponse["method"], status: v.status as AiExplanationResponse["status"],
    explanationVersion: 1, summary: v.summary, checks, provider: v.provider, model: v.model,
    generatedAtUtc: v.generatedAtUtc, notice: v.notice,
    ...(typeof v.failureCode === "string" && failureCodes.includes(v.failureCode) ? { failureCode: v.failureCode } : {}) };
}

const failureCodes = ["provider-authentication", "provider-model-denied", "provider-rate-limited", "provider-budget",
  "gateway-request-rejected", "provider-unavailable", "gateway-unreachable", "timeout"];

export function aiExplanationFailureMessage(status: string, failureCode?: string | null): string {
  switch (failureCode) {
    case "provider-authentication": return "OmniRoute o su proveedor rechazaron la credencial. Revisá la conexión de OpenRouter y la clave local de la app.";
    case "provider-model-denied": return "La clave no permite usar el modelo solicitado. Revisá los permisos del modelo y de la conexión en OmniRoute.";
    case "provider-rate-limited": return "El proveedor alcanzó su cuota o está limitando las solicitudes. Esperá antes de volver a intentar.";
    case "provider-budget": return "El proveedor rechazó la solicitud por sus límites de crédito o presupuesto. Revisá ese límite; no hace falta cambiar a un modelo pago.";
    case "gateway-request-rejected": return "OmniRoute rechazó la solicitud. Revisá que el modelo use una ruta explícita de OpenRouter y esté permitido en la clave.";
    case "timeout": return "El modelo tardó más de lo permitido. Esperá un momento antes de volver a intentar.";
    case "gateway-unreachable": return "No pudimos conectar con OmniRoute. Comprobá que esté ejecutándose en esta PC.";
    case "provider-unavailable": return "El modelo o su proveedor no están disponibles en este momento. Podés volver a intentar más tarde.";
  }
  if (status === "invalid-response") return "El modelo respondió, pero el contenido no cumple el formato o los límites necesarios. No mostramos ese texto como una explicación válida.";
  if (status === "disabled") return "Las explicaciones con IA están desactivadas en esta instalación.";
  if (status === "unconfigured") return "La configuración de IA está incompleta. Revisá el modelo y la clave local de OmniRoute.";
  if (status === "unsupported") return "Este análisis no tiene el formato compatible con la explicación de IA.";
  return "El servicio de IA no pudo completar la solicitud. Podés volver a intentar más tarde.";
}

export function validateAiExplanationRequest(data: FormData): { deviceId: string; analysisId: string; confirmed: true } | null {
  const deviceId = data.get("deviceId"), analysisId = data.get("analysisId");
  if (!isDeviceId(deviceId) || !isDeviceId(analysisId) || data.getAll("requestExplanation").length !== 1 || data.get("requestExplanation") !== "true") return null;
  return { deviceId, analysisId, confirmed: true };
}
