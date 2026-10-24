import "server-only";
import { ApiError, request } from "./api";
import { parseAccountSessionList, MAX_SESSION_PAGE, isSessionId } from "./account-sessions";

export async function fetchAccountSessions(token: string, page: number) {
  if (!Number.isInteger(page) || page < 1 || page > MAX_SESSION_PAGE) throw new ApiError(400, "Página no válida.");
  const result = parseAccountSessionList(await request(`/api/account/sessions?page=${page}`, { token }));
  if (!result || result.page !== page) throw new ApiError(502, "No pudimos comprobar las sesiones recibidas.");
  return result;
}

export async function revokeAccountSession(token: string, id: string): Promise<{ currentSession: boolean }> {
  if (!isSessionId(id)) throw new ApiError(400, "Sesión no válida.");
  const result = await request(`/api/account/sessions/${id}/revoke`, { token, body: { confirmed: true } });
  if (!result || typeof result !== "object" || !("currentSession" in result) || typeof result.currentSession !== "boolean")
    throw new ApiError(502, "No pudimos confirmar el cierre de la sesión.");
  return { currentSession: result.currentSession };
}
