import "server-only";
import type { AuthFormState, AuthResponse, DashboardData, UserProfile } from "./contracts";
import { parseDashboard } from "./analyses";
import { NEW_PASSWORD_HELP } from "./new-password";

export class ApiError extends Error {
  constructor(public readonly status: number, message: string, public readonly fieldErrors?: AuthFormState["fieldErrors"]) {
    super(message);
    this.name = "ApiError";
  }
}

function apiUrl(path: string): URL {
  const configured = process.env.API_BASE_URL ?? "http://localhost:5169";
  const base = new URL(configured.endsWith("/") ? configured : configured + "/");
  if (!["http:", "https:"].includes(base.protocol) || base.username || base.password) {
    throw new Error("La dirección de la API no es válida.");
  }
  return new URL(path.replace(/^\//, ""), base);
}

export async function request(path: string, options: { body?: unknown; token?: string; method?: "GET" | "POST" | "DELETE"; timeoutMs?: number } = {}): Promise<unknown> {
  let response: Response;
  try {
    response = await fetch(apiUrl(path), {
      method: options.method ?? (options.body === undefined ? "GET" : "POST"),
      cache: "no-store",
      signal: AbortSignal.timeout(options.timeoutMs ?? 12_000),
      headers: {
        Accept: "application/json",
        ...(options.body === undefined ? {} : { "Content-Type": "application/json" }),
        ...(options.token ? { Authorization: `Bearer ${options.token}` } : {}),
      },
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    });
  } catch {
    throw new ApiError(0, "No pudimos conectar con el servicio. Intentá de nuevo en unos momentos.");
  }

  const body: unknown = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    if (response.status === 400 && ["/api/auth/register", "/api/auth/reset-password", "/api/account/password"].includes(path) &&
      body && typeof body === "object" && "code" in body && body.code === "password_policy")
      throw new ApiError(400, NEW_PASSWORD_HELP, { password: NEW_PASSWORD_HELP });
    if (response.status === 409 && path === "/api/auth/login")
      throw new ApiError(409, "Se alcanzó el límite de sesiones web. Cerrá una desde otro navegador o usá Recuperar contraseña para cerrar todos los accesos.");
    const messages: Record<number, string> = {
      400: "Revisá los campos indicados para continuar.",
      401: "El correo o la contraseña no son correctos.",
      409: "Ese correo ya está registrado. Iniciá sesión para continuar.",
      429: "Hubo demasiados intentos. Esperá un momento y volvé a intentar.",
    };
    throw new ApiError(response.status, messages[response.status] ?? "No pudimos completar la solicitud. Intentá de nuevo en unos momentos.");
  }
  return body;
}

export async function authenticate(mode: "login" | "register", credentials: { name?: string; email: string; password: string }): Promise<AuthResponse> {
  const result = await request(`/api/auth/${mode}`, { body: credentials });
  return validateAuth(result);
}

function validateAuth(result: unknown): AuthResponse {
  if (!result || typeof result !== "object") throw new ApiError(502, "No pudimos iniciar la sesión. Intentá nuevamente.");
  const auth = result as Partial<AuthResponse>;
  if (
    typeof auth.accessToken !== "string" || typeof auth.expiresAtUtc !== "string" ||
    !Number.isFinite(Date.parse(auth.expiresAtUtc)) || !auth.user ||
    typeof auth.user.id !== "string" || typeof auth.user.name !== "string" || typeof auth.user.email !== "string" ||
    typeof auth.user.emailVerified !== "boolean" || typeof auth.refreshToken !== "string" ||
    typeof auth.refreshExpiresAtUtc !== "string" || !Number.isFinite(Date.parse(auth.refreshExpiresAtUtc))
  ) throw new ApiError(502, "No pudimos iniciar la sesión. Intentá nuevamente.");
  return auth as AuthResponse;
}

export async function refreshAuth(refreshToken: string): Promise<AuthResponse> {
  return validateAuth(await request("/api/auth/refresh", { body: { refreshToken } }));
}

export async function revokeSession(refreshToken: string): Promise<void> {
  await request("/api/auth/logout", { body: { refreshToken } });
}

export async function revokeAllSessions(token: string): Promise<void> {
  await request("/api/auth/logout-all", { token, body: {} });
}

export async function fetchProfile(token: string): Promise<UserProfile> {
  const value = await request("/api/auth/me", { token }) as Partial<UserProfile> | null;
  if (!value || typeof value.id !== "string" || typeof value.name !== "string" ||
    typeof value.email !== "string" || typeof value.emailVerified !== "boolean") {
    throw new ApiError(502, "No pudimos consultar tu cuenta.");
  }
  return value as UserProfile;
}

export async function sendPasswordReset(email: string): Promise<void> {
  await request("/api/auth/forgot-password", { body: { email } });
}

export async function resetPassword(token: string, password: string): Promise<void> {
  await request("/api/auth/reset-password", { body: { token, password } });
}

export async function requestVerification(token: string): Promise<void> {
  await request("/api/auth/resend-verification", { token, body: {} });
}

export async function confirmEmail(token: string): Promise<void> {
  await request("/api/auth/verify-email", { body: { token } });
}

export async function fetchDashboard(token: string): Promise<DashboardData> {
  const data = parseDashboard(await request("/api/dashboard", { token }));
  if (!data) throw new ApiError(502, "No pudimos cargar tu panel.");
  return data;
}
