import "server-only";
import { ApiError, request } from "./api";
import { isDeviceId, parseDevice, parseDeviceDetail, parsePairing, type DeviceRecord, type DevicePairing, type DeviceDetail } from "./devices";

async function deviceRequest(path: string, token: string, body?: unknown): Promise<unknown> {
  try { return await request(path, { token, body }); }
  catch (error) {
    if (error instanceof ApiError && error.status === 403) throw new ApiError(403, "Verificá tu correo desde Mi cuenta para vincular un equipo.");
    if (error instanceof ApiError && error.status === 404) throw new ApiError(404, "No encontramos ese equipo en tu cuenta.");
    if (error instanceof ApiError && error.status === 409) throw new ApiError(409, "El estado del equipo cambió. Actualizá la página antes de reintentar.");
    throw error;
  }
}
function devicePath(id: string): string {
  if (!isDeviceId(id)) throw new ApiError(404, "No encontramos ese equipo en tu cuenta.");
  return `/api/devices/${id}`;
}
export async function fetchDevices(token: string): Promise<DeviceRecord[]> {
  const result = await deviceRequest("/api/devices", token);
  if (!Array.isArray(result)) throw new ApiError(502, "No pudimos consultar tus equipos.");
  const devices = result.map(parseDevice);
  if (devices.some(item => !item)) throw new ApiError(502, "No pudimos consultar tus equipos.");
  return devices as DeviceRecord[];
}
export async function fetchDevice(id: string, token: string): Promise<DeviceDetail> {
  const result = parseDeviceDetail(await deviceRequest(devicePath(id), token));
  if (!result) throw new ApiError(502, "No pudimos consultar ese equipo.");
  return result;
}
export async function createDevice(name: string, token: string): Promise<DevicePairing> {
  let value;
  try { value = await deviceRequest("/api/devices", token, { name }); }
  catch (error) {
    if (error instanceof ApiError && error.status === 409) throw new ApiError(409, "Ya tenés un equipo activo. Revocá su acceso antes de agregar otro.");
    throw error;
  }
  const result = parsePairing(value);
  if (!result) throw new ApiError(502, "No pudimos preparar la vinculación. Actualizá la página antes de reintentar.");
  return result;
}
export async function regeneratePairing(id: string, token: string): Promise<DevicePairing> {
  const result = parsePairing(await deviceRequest(`${devicePath(id)}/pairing-code`, token, {}));
  if (!result) throw new ApiError(502, "No pudimos generar el código. Actualizá la página antes de reintentar.");
  return result;
}
export async function revokeDevice(id: string, token: string): Promise<void> {
  await deviceRequest(`${devicePath(id)}/revoke`, token, {});
}
