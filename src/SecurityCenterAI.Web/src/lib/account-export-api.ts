import "server-only";
import { ApiError, request } from "./api";
import { parseAccountExport } from "./account-export";

export async function exportAccount(token: string, expectedUserId: string) {
  const result = parseAccountExport(await request("/api/account/export", { token }), expectedUserId);
  if (!result) throw new ApiError(502, "No pudimos preparar una copia válida de los datos de tu cuenta.");
  return result;
}
