import { isAnalysisDate } from "./analyses.ts";
import { isDeviceId } from "./devices.ts";

export const activityLabels = {
  "account.registered": "Cuenta creada",
  "session.started": "Sesión iniciada",
  "session.revoked": "Sesión cerrada",
  "sessions.revoked": "Cierre de todas las sesiones",
  "session.replay-blocked": "Renovación reutilizada; sesión cerrada",
  "email.verified": "Correo verificado",
  "email.changed": "Correo cambiado",
  "password.reset": "Contraseña restablecida",
  "password.changed": "Contraseña cambiada desde Mi cuenta",
  "device.registered": "Dispositivo registrado",
  "device.code-issued": "Nuevo código de vinculación generado",
  "device.paired": "Dispositivo vinculado",
  "device.revoked": "Acceso de un dispositivo revocado",
  "analysis.deleted": "Análisis eliminado",
} as const;

export type SecurityActivity = { id: string; action: keyof typeof activityLabels; occurredAtUtc: string };
export type SecurityActivityList = { events: SecurityActivity[]; hasMore: boolean; limit: 50; nextBefore: string | null };
export type ActivityQuery = { action: keyof typeof activityLabels | ""; from: string; to: string; before: string };
const record = (value: unknown): value is Record<string, unknown> => !!value && typeof value === "object" && !Array.isArray(value);

export function parseSecurityActivityEvents(value: unknown): SecurityActivity[] | null {
  if (!Array.isArray(value)) return null;
  const ids = new Set<string>();
  const events: SecurityActivity[] = [];
  for (const item of value) {
    if (!record(item) || !isDeviceId(item.id) || item.id === "00000000-0000-0000-0000-000000000000" ||
      ids.has(item.id.toLowerCase()) || typeof item.action !== "string" ||
      !Object.hasOwn(activityLabels, item.action) || !isAnalysisDate(item.occurredAtUtc)) return null;
    ids.add(item.id.toLowerCase());
    events.push({ id: item.id, action: item.action as SecurityActivity["action"], occurredAtUtc: item.occurredAtUtc });
  }
  return events;
}

export function parseSecurityActivityList(value: unknown): SecurityActivityList | null {
  if (!record(value) || value.limit !== 50 || typeof value.hasMore !== "boolean") return null;
  const events = parseSecurityActivityEvents(value.events);
  if (!events || events.length > 50 || value.hasMore && events.length !== 50) return null;
  if (events.some((event, index) => index > 0 && Date.parse(event.occurredAtUtc) > Date.parse(events[index - 1].occurredAtUtc))) return null;
  const nextBefore = value.nextBefore ?? null;
  if (value.hasMore ? !isDeviceId(nextBefore) || nextBefore.toLowerCase() !== events.at(-1)?.id.toLowerCase() : nextBefore !== null) return null;
  return { events, hasMore: value.hasMore, limit: 50, nextBefore: nextBefore as string | null };
}

function isCalendarDate(value: string): boolean {
  if (!/^(?!0000)\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const date = new Date(`${value}T00:00:00Z`);
  return Number.isFinite(date.getTime()) && date.toISOString().slice(0, 10) === value;
}

export function parseActivityQuery(value: Record<string, string | string[] | undefined>): ActivityQuery | null {
  const fields = [value.action, value.from, value.to, value.before];
  if (fields.some(field => field !== undefined && typeof field !== "string")) return null;
  const [action, from, to, before] = fields.map(field => field as string | undefined ?? "");
  if (action && !Object.hasOwn(activityLabels, action) || from && !isCalendarDate(from) || to && !isCalendarDate(to) ||
    to === "9999-12-31" || from && to && from > to || before && (!isDeviceId(before) || before === "00000000-0000-0000-0000-000000000000")) return null;
  return { action: action as ActivityQuery["action"], from, to, before: before.toLowerCase() };
}

export function activityQueryString(query: ActivityQuery): string {
  const params = new URLSearchParams();
  for (const key of ["action", "from", "to", "before"] as const) if (query[key]) params.set(key, query[key]);
  return params.toString();
}

export function activityHref(query: ActivityQuery): string {
  const search = activityQueryString(query);
  return `/account/activity${search ? `?${search}` : ""}`;
}
