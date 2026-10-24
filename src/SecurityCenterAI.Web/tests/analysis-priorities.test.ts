import assert from "node:assert/strict";
import test from "node:test";
import { randomUUID } from "node:crypto";
import { analysisPriorities } from "../src/lib/analysis-priorities.ts";
import { parseDeviceAnalysis, type DeviceAnalysis } from "../src/lib/devices.ts";
import type { CheckStatus } from "../src/lib/analyses.ts";

function report(overrides: { status: CheckStatus; severity?: string; ruleId?: string }[] = []): DeviceAnalysis {
  const now = new Date().toISOString();
  const findings = Array.from({ length: 10 }, (_, index) => ({ id: randomUUID(), ruleId: overrides[index]?.ruleId ?? `RULE-${index}`,
    ruleVersion: 1, title: `Comprobación ${index}`, severity: overrides[index]?.severity ?? "medium",
    status: overrides[index]?.status ?? "passed", evidence: "PRIVATE_EVIDENCE", recommendation: "PRIVATE_RECOMMENDATION", detectedAtUtc: now }));
  const count = (status: CheckStatus) => findings.filter(finding => finding.status === status).length;
  const known = count("passed") + count("attention"); const applicable = 10 - count("notApplicable");
  const parsed = parseDeviceAnalysis({ id: randomUUID(), reportId: randomUUID(), source: "windows-agent", score: known ? 75 : null,
    analyzedAtUtc: now, startedAtUtc: now, completedAtUtc: now, agentVersion: "0.4.1-preview.1", scoringVersion: 1,
    passedCount: count("passed"), attentionCount: count("attention"), unknownCount: count("unknown"), notApplicableCount: count("notApplicable"),
    coveragePercent: applicable ? Math.floor(100 * known / applicable) : 0, findings });
  assert.ok(parsed); return parsed;
}

test("attention uses severity and stable identities; unknown findings remain a separate group", () => {
  const analysis = report([{ status: "attention", severity: "low" }, { status: "attention", severity: "critical" },
    { status: "attention", severity: "high", ruleId: "RULE-Z" }, { status: "attention", severity: "high", ruleId: "RULE-A" },
    { status: "unknown", severity: "critical" }, { status: "unknown", severity: "low" }]);
  const priorities = analysisPriorities(analysis); assert.ok(priorities);
  assert.deepEqual(priorities.attention.map(item => item.id), [analysis.findings[1].id, analysis.findings[3].id, analysis.findings[2].id]);
  assert.equal(priorities.attentionTotal, 4); assert.equal(priorities.unknownTotal, 2);
  assert.ok(priorities.unknown.every(item => !priorities.attention.some(other => other.id === item.id)));
});

test("presentation preserves the report and does not copy evidence, recommendations or unknown fields", () => {
  const analysis = report([{ status: "attention" }, { status: "unknown" }]);
  const before = JSON.stringify(analysis); const priorities = analysisPriorities(analysis); assert.ok(priorities);
  assert.equal(JSON.stringify(analysis), before);
  assert.ok(!JSON.stringify(priorities).includes("PRIVATE_"));
  assert.deepEqual(Object.keys(priorities.attention[0]).sort(), ["id", "ruleId", "severity", "title"]);
});

test("unknown-only reports do not become confirmed problems or obtain a score", () => {
  const analysis = report(Array.from({ length: 10 }, () => ({ status: "unknown" as const })));
  const priorities = analysisPriorities(analysis); assert.ok(priorities);
  assert.equal(priorities.attentionTotal, 0); assert.equal(priorities.unknownTotal, 10);
  assert.equal(priorities.unknown.length, 3); assert.equal(analysis.score, null);
  assert.equal(analysis.coveragePercent, 0);
});

test("non-applicable checks do not become successful checks", () => {
  const analysis = report(Array.from({ length: 10 }, () => ({ status: "notApplicable" as const })));
  const priorities = analysisPriorities(analysis); assert.ok(priorities);
  assert.equal(priorities.applicableCount, 0); assert.equal(priorities.attentionTotal, 0); assert.equal(priorities.unknownTotal, 0);
  assert.equal(analysis.passedCount, 0); assert.equal(analysis.score, null);
});

test("passed checks have no invented priorities and legacy results do not acquire states", () => {
  const analysis = report(); const priorities = analysisPriorities(analysis); assert.ok(priorities);
  assert.equal(priorities.attentionTotal, 0); assert.equal(priorities.unknownTotal, 0); assert.equal(priorities.applicableCount, 10);
  assert.equal(analysisPriorities({ ...analysis, source: null }), null);
});

test("reordering input does not change priorities and unknown severity does not set its priority", () => {
  const analysis = report([{ status: "attention", severity: "high", ruleId: "RULE-B" }, { status: "attention", severity: "high", ruleId: "RULE-A" },
    { status: "unknown", severity: "critical", ruleId: "RULE-Z" }, { status: "unknown", severity: "low", ruleId: "RULE-C" }]);
  const priorities = analysisPriorities(analysis);
  assert.deepEqual(analysisPriorities({ ...analysis, findings: [...analysis.findings].reverse() }), priorities);
  assert.deepEqual(priorities?.unknown.map(item => item.ruleId), ["RULE-C", "RULE-Z"]);
});
