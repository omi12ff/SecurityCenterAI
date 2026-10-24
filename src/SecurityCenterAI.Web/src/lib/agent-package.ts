import "server-only";
import { openVerifiedAgentPackage } from "./agent-package-file";

export function openConfiguredAgentPackage() {
  return openVerifiedAgentPackage(process.env.AGENT_PACKAGE_PATH);
}

export async function agentPackageInfo() {
  const prepared = await openConfiguredAgentPackage();
  if (!prepared) return null;
  try { return prepared.info; }
  finally { await prepared.file.close(); }
}
