/** A PowerShell command containing only a root origin accepted by the agent. */
export function agentPairingCommand(configuredUrl: string | undefined): string | null {
  if (!configuredUrl || configuredUrl.length > 2048 || !/^https?:\/\/[A-Za-z0-9.[\]:-]+\/?$/.test(configuredUrl)) return null;
  try {
    const url = new URL(configuredUrl);
    const loopback = ["localhost", "127.0.0.1", "[::1]"].includes(url.hostname);
    if (url.username || url.password || url.search || url.hash || url.pathname !== "/" || url.port === "0" ||
      (url.protocol !== "https:" && !(url.protocol === "http:" && loopback))) return null;
    return `.\\SecurityCenterAI.Agent.exe pair --api '${url.origin}'`;
  } catch { return null; }
}
