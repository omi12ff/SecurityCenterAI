import { randomUUID } from 'node:crypto';
import { accountApi } from './api.ts';

export type SubmittedStatus = 'passed' | 'attention' | 'unknown' | 'notApplicable';
export type SubmittedCheck = { ruleId: string; ruleVersion: number; status: SubmittedStatus; evidence: string; observedAtUtc: string };
export type AgentReport = {
  reportId: string; schemaVersion: 2; agentVersion: string; startedAtUtc: string; completedAtUtc: string; checks: SubmittedCheck[];
};
export type AnalysisReceipt = {
  analysisId: string; reportId: string; score: number | null; coveragePercent: number;
  passedCount: number; attentionCount: number; unknownCount: number; notApplicableCount: number;
  scoringVersion: number; receivedAtUtc: string; alreadyReceived: boolean;
};
export type StoredAnalysis = {
  id: string; score: number | null; reportId: string; source: string; scoringVersion: number; coveragePercent: number;
  passedCount: number; attentionCount: number; unknownCount: number; notApplicableCount: number;
  findings: { ruleId: string; status: SubmittedStatus; evidence: string; title: string; recommendation: string; severity: string }[];
};

const ruleIds = [
  'WIN-DEFENDER-ANTIVIRUS', 'WIN-DEFENDER-REALTIME', 'WIN-DEFENDER-SIGNATURES', 'WIN-FIREWALL-PROFILES',
  'WIN-SECURE-BOOT', 'WIN-TPM-READY', 'WIN-BITLOCKER-SYSTEM', 'WIN-UPDATE-SERVICE', 'WIN-UAC-ENABLED', 'WIN-SMB1-SERVER',
];

/** Synthetic observations only: never reads the host or an existing user report. */
export function syntheticReport(statuses: Record<string, SubmittedStatus> = {}): AgentReport {
  const completedAtUtc = new Date().toISOString();
  return {
    reportId: randomUUID(), schemaVersion: 2, agentVersion: '0.2.0-e2e',
    startedAtUtc: new Date(Date.now() - 10_000).toISOString(), completedAtUtc,
    checks: ruleIds.map(ruleId => ({
      ruleId, ruleVersion: 1, status: statuses[ruleId] ?? 'passed',
      evidence: 'Evidencia sintética: <script>window.__analysisScriptExecuted=true</script>', observedAtUtc: completedAtUtc,
    })),
  };
}

export function submitReport(deviceToken: string, body: unknown) {
  return accountApi('/api/agent/analyses', { token: deviceToken, scheme: 'Device', body });
}
