"use server";

import { revalidatePath } from "next/cache";
import { ApiError } from "@/lib/api";
import { createDevice, regeneratePairing, revokeDevice } from "@/lib/device-api";
import { isDeviceId, type DeviceFormState } from "@/lib/devices";
import { requireSession, redirectToRenewal } from "@/lib/session";

const text = (data: FormData, key: string) => typeof data.get(key) === "string" ? data.get(key) as string : "";
async function failure(error: unknown): Promise<DeviceFormState> {
  if (error instanceof ApiError && error.status === 401) return redirectToRenewal("/devices");
  return { message: error instanceof ApiError ? error.message : "No pudimos completar la operación. Intentá de nuevo." };
}
function invalidate(id: string) {
  revalidatePath("/devices");
  revalidatePath(`/devices/${id}`);
}

export async function addDeviceAction(_previous: DeviceFormState, data: FormData): Promise<DeviceFormState> {
  const name = text(data, "name").trim();
  if (name.length < 2 || name.length > 100) return { message: "Usá un nombre de entre 2 y 100 caracteres.", name };
  const session = await requireSession("/devices");
  try {
    const pairing = await createDevice(name, session.token);
    invalidate(pairing.device.id);
    return { success: true, pairing };
  } catch (error) { return failure(error); }
}

export async function regenerateDeviceCodeAction(_previous: DeviceFormState, data: FormData): Promise<DeviceFormState> {
  const id = text(data, "deviceId");
  if (!isDeviceId(id)) return { message: "No encontramos ese equipo." };
  const session = await requireSession("/devices");
  try {
    const pairing = await regeneratePairing(id, session.token);
    invalidate(id);
    return { success: true, pairing };
  } catch (error) { return failure(error); }
}

export async function revokeDeviceAction(_previous: DeviceFormState, data: FormData): Promise<DeviceFormState> {
  const id = text(data, "deviceId");
  if (!isDeviceId(id) || text(data, "confirmed") !== "yes") return { message: "Confirmá la revocación para continuar." };
  const session = await requireSession("/devices");
  try {
    await revokeDevice(id, session.token);
    invalidate(id);
    return { success: true, message: "Se revocó el acceso del equipo. Su historial se conserva." };
  } catch (error) { return failure(error); }
}
