import type { UserProfile } from "./contracts";

export type SessionToken = { token: string; user: UserProfile; expiresAt: Date };

// Solo extrae claims y vencimiento; la API verifica la firma y autoriza cada consulta.
export function parseSessionToken(token: string, nowMs = Date.now()): SessionToken | null {
  try {
    const parts = token.split(".");
    if (parts.length !== 3 || parts.some((part) => !part || !/^[A-Za-z0-9_-]+$/.test(part))) return null;
    const claims: unknown = JSON.parse(Buffer.from(parts[1], "base64url").toString("utf8"));
    if (!claims || typeof claims !== "object") return null;
    const { sub, name, email, exp } = claims as Record<string, unknown>;
    if (
      typeof sub !== "string" || !/^[a-f\d]{8}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{12}$/i.test(sub) ||
      typeof name !== "string" || !name.trim() || typeof email !== "string" || !email.trim() ||
      typeof exp !== "number" || !Number.isFinite(exp) || exp * 1000 <= nowMs
    ) return null;
    const expiresAt = new Date(exp * 1000);
    if (!Number.isFinite(expiresAt.getTime())) return null;
    const verified = (claims as Record<string, unknown>).email_verified;
    return { token, user: { id: sub, name, email, emailVerified: verified === true || verified === "true" }, expiresAt };
  } catch {
    return null;
  }
}
