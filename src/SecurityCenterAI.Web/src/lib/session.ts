import "server-only";
import { cookies } from "next/headers";
import { ApiError, fetchDashboard } from "./api";
import { redirect } from "next/navigation";
import type { AuthResponse, DashboardData } from "./contracts";
import { parseSessionToken, type SessionToken } from "./token";

const SESSION_COOKIE = "security_center_session";
const REFRESH_COOKIE = "security_center_refresh";

export async function readRefreshToken(): Promise<string | null> {
  const token = (await cookies()).get(REFRESH_COOKIE)?.value;
  return token && /^[A-Za-z0-9_-]{43,512}$/.test(token) ? token : null;
}

export async function requireSession(returnTo: "/account" | "/account/activity" | "/account/sessions" | "/dashboard" | "/devices" | "/tools/scam-check" | "/getting-started"): Promise<SessionToken> {
  const session = await readSession();
  if (session) return session;
  return redirectToRenewal(returnTo);
}

export async function redirectToRenewal(returnTo: "/account" | "/account/activity" | "/account/sessions" | "/dashboard" | "/devices" | "/tools/scam-check" | "/getting-started"): Promise<never> {
  if (await readRefreshToken()) redirect(`/session/renew?next=${encodeURIComponent(returnTo)}`);
  redirect("/login?reason=session-expired");
}

export async function readSession(): Promise<SessionToken | null> {
  const token = (await cookies()).get(SESSION_COOKIE)?.value;
  return token ? parseSessionToken(token) : null;
}

export async function createSession(auth: AuthResponse): Promise<void> {
  const session = parseSessionToken(auth.accessToken);
  if (!session || session.user.id !== auth.user.id) throw new Error("La sesión recibida no es válida.");
  const refreshExpires = new Date(auth.refreshExpiresAtUtc);
  const expires = new Date(Math.min(session.expiresAt.getTime(), Date.parse(auth.expiresAtUtc), refreshExpires.getTime()));
  if (expires.getTime() <= Date.now() || !Number.isFinite(refreshExpires.getTime()) ||
    refreshExpires.getTime() <= Date.now() || !/^[A-Za-z0-9_-]{43,512}$/.test(auth.refreshToken)) {
    throw new Error("La sesión recibida no es válida.");
  }
  const store = await cookies();
  const options = {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    path: "/",
  };
  store.set(SESSION_COOKIE, auth.accessToken, { ...options, expires });
  store.set(REFRESH_COOKIE, auth.refreshToken, { ...options, expires: refreshExpires });
}

export async function deleteSession(): Promise<void> {
  const store = await cookies();
  store.delete(SESSION_COOKIE);
  store.delete(REFRESH_COOKIE);
}

export async function loadDashboard(session: SessionToken): Promise<
  | { kind: "success"; data: DashboardData }
  | { kind: "unauthenticated" }
  | { kind: "unavailable" }
> {
  try {
    return { kind: "success", data: await fetchDashboard(session.token) };
  } catch (error) {
    return error instanceof ApiError && error.status === 401
      ? { kind: "unauthenticated" }
      : { kind: "unavailable" };
  }
}
