import "server-only";
import { ApiError, request } from "./api";
import { activityQueryString, parseSecurityActivityList, type ActivityQuery } from "./security-activity";

export async function fetchSecurityActivity(token: string, query: ActivityQuery) {
  const search = activityQueryString(query);
  const value = parseSecurityActivityList(await request(`/api/account/security-activity${search ? `?${search}` : ""}`, { token }));
  if (!value) throw new ApiError(502, "No pudimos comprobar el historial de actividad recibido.");
  return value;
}
