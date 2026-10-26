import { test as base, expect } from '@playwright/test';
import { spawn } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { createServer, type ServerResponse } from 'node:http';
import { once } from 'node:events';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import type { AddressInfo } from 'node:net';

type Scenario = 'gateway-rejected' | 'provider-denied' | 'provider-timeout' | 'generated' | 'disabled' | 'unconfigured' | 'unavailable' | 'invalid-response' | 'quota' | 'availability-error' | 'availability-not-found' | 'malformed-response' | 'foreign-rule' | 'session-expired' | 'not-found';
type AiTestApp = {
  baseUrl: string; analysisPath: string; token: string;
  postBodies: unknown[]; scenario: Scenario; holdResponse: boolean;
  release: () => void; reset: () => void;
};
export const unsafeAiText = '<script>window.__aiScriptExecuted=true</script> [enlace](https://model-output.invalid)';
const ids = { user: randomUUID(), device: randomUUID(), analysis: randomUUID() };
const ruleIds = ['WIN-DEFENDER-ANTIVIRUS', 'WIN-DEFENDER-REALTIME', 'WIN-DEFENDER-SIGNATURES', 'WIN-FIREWALL-PROFILES',
  'WIN-SECURE-BOOT', 'WIN-TPM-READY', 'WIN-BITLOCKER-SYSTEM', 'WIN-UPDATE-SERVICE', 'WIN-UAC-ENABLED', 'WIN-SMB1-SERVER'];
const now = new Date().toISOString();
const findings = ruleIds.map((ruleId, index) => ({ id: randomUUID(), ruleId, ruleVersion: 1,
  title: index === 0 ? 'Estado del antivirus' : `Comprobación local ${index + 1}`, status: index === 0 ? 'unknown' : 'passed',
  severity: 'medium', evidence: 'EVIDENCE_LOCAL_ONLY <b>evidencia sintética</b>', recommendation: 'Revisar la guía local.', detectedAtUtc: now }));
const localGuide = {
  analysis: { id: ids.analysis, deviceId: ids.device, score: 90, analyzedAtUtc: now, source: 'windows-agent', scoringVersion: 1,
    coveragePercent: 90, passedCount: 9, attentionCount: 0, unknownCount: 1, notApplicableCount: 0,
    agentVersion: '0.0.0-synthetic', startedAtUtc: now, completedAtUtc: now, reportId: randomUUID(), findings },
  deviceName: 'Equipo sintético de prueba', method: 'local-guide', explanationVersion: 1, summary: 'Guía local disponible aunque la IA falle.',
  nextSteps: ['Revisá el estado desconocido sin suponer una protección desactivada.'], feedback: null,
  checks: findings.map(finding => ({ findingId: finding.id, meaning: 'No se pudo confirmar este estado.', whatWasChecked: 'Comprobación local limitada.',
    whyItMatters: 'Ayuda a revisar la protección.', suggestedAction: 'Revisá el estado en Windows.', verificationLimit: 'No garantiza seguridad completa.',
    references: [{ title: 'Documentación de Microsoft', url: 'https://learn.microsoft.com/windows/' }] })),
};

function json(response: ServerResponse, value: unknown, status = 200) {
  response.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
  response.end(JSON.stringify(value));
}

/** Production Next page and Server Action, isolated HTTP API; never contacts an AI service or user database. */
export const test = base.extend<{}, { aiApp: AiTestApp }>({
  aiApp: [async ({}, use) => {
    if (!existsSync(join(process.cwd(), '.next-qa', 'BUILD_ID')))
      throw new Error('Build the isolated Next output first with SECURITY_CENTER_WEB_QA=1 npm run build.');
    const claims = { sub: ids.user, name: 'Usuario sintético', email: 'ai-ui@example.invalid', email_verified: true,
      exp: Math.floor(Date.now() / 1000) + 3600 };
    const token = `${Buffer.from('{"alg":"synthetic"}').toString('base64url')}.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.synthetic`;
    let releasePending: (() => void) | undefined;
    const app: AiTestApp = {
      baseUrl: '', analysisPath: `/devices/${ids.device}/analyses/${ids.analysis}`, token,
      postBodies: [], scenario: 'generated', holdResponse: false,
      release: () => { releasePending?.(); releasePending = undefined; },
      reset: () => { app.release(); app.scenario = 'generated'; app.holdResponse = false; app.postBodies.length = 0; },
    };
    const api = createServer((request, response) => {
      void (async () => {
        if (request.headers.authorization !== `Bearer ${token}`) return json(response, { code: 'unauthorized' }, 401);
        if (request.url === '/api/auth/me') return json(response, { id: ids.user, name: claims.name, email: claims.email, emailVerified: true });
        if (request.url === `/api${app.analysisPath}/explanation`) return json(response, localGuide);
        if (request.url !== `/api${app.analysisPath}/ai-explanation`) return json(response, { code: 'not_found' }, 404);
        if (request.method === 'GET') {
          if (app.scenario === 'availability-error') return json(response, { code: 'unavailable' }, 503);
          if (app.scenario === 'availability-not-found') return json(response, { code: 'not_found' }, 404);
          const available = !['disabled', 'unconfigured'].includes(app.scenario);
          return json(response, { available, code: available ? 'available' : app.scenario,
            message: available ? 'La ampliación con IA está disponible.' : 'La ampliación con IA está desactivada o sin configurar. La guía local sigue disponible.',
            provider: available ? 'Proveedor sintético' : null, model: available ? 'modelo-prueba' : null,
            dailyLimit: 5, dataDisclosure: 'Solo reglas, estados, puntaje, cobertura y textos de la guía local.' });
        }
        let raw = ''; for await (const chunk of request) raw += chunk.toString();
        let body: unknown; try { body = JSON.parse(raw); } catch { return json(response, { code: 'invalid' }, 400); }
        app.postBodies.push(body);
        if (!body || typeof body !== 'object' || !('confirmed' in body) || body.confirmed !== true) return json(response, { code: 'consent' }, 400);
        if (app.holdResponse) await new Promise<void>(resolve => { releasePending = resolve; });
        if (app.scenario === 'quota') return json(response, { code: 'quota' }, 429);
        if (app.scenario === 'session-expired') return json(response, { code: 'unauthorized' }, 401);
        if (app.scenario === 'not-found') return json(response, { code: 'not_found' }, 404);
        if (app.scenario === 'malformed-response') return json(response, { status: 'generated', summary: unsafeAiText });
        const failureCode = app.scenario === 'gateway-rejected' ? 'gateway-request-rejected' : app.scenario === 'provider-denied' ? 'provider-model-denied' : app.scenario === 'provider-timeout' ? 'timeout' : null;
        const generated = !failureCode && !['unavailable', 'invalid-response', 'disabled', 'unconfigured'].includes(app.scenario);
        json(response, { method: generated ? 'ai-explanation' : 'local-guide', status: generated ? 'generated' : failureCode ? 'unavailable' : app.scenario, failureCode,
          explanationVersion: 1, summary: generated ? unsafeAiText : localGuide.summary,
          checks: generated ? findings.map((finding, index) => ({ ruleId: app.scenario === 'foreign-rule' && index === 0 ? 'INVENTED-RULE' : finding.ruleId,
            explanation: index === 0 ? 'Desconocido significa que no se pudo verificar; no implica protección desactivada.' : 'Texto sintético ampliado.' })) : [],
          provider: generated ? 'Proveedor sintético' : null, model: generated ? 'modelo-prueba' : null, generatedAtUtc: now,
          notice: generated ? 'La IA puede equivocarse. Los estados originales no cambian.' : 'La IA no pudo generar una explicación válida. Conservá la guía local.' });
      })().catch(() => { if (!response.headersSent) json(response, { code: 'fixture_error' }, 500); else response.end(); });
    });
    api.listen(0, '127.0.0.1'); await once(api, 'listening');
    const portProbe = createServer(); portProbe.listen(0, '127.0.0.1'); await once(portProbe, 'listening');
    const nextPort = (portProbe.address() as AddressInfo).port;
    await new Promise<void>(resolve => portProbe.close(() => resolve()));
    app.baseUrl = `http://127.0.0.1:${nextPort}`;
    const next = spawn(process.execPath, [join(process.cwd(), 'node_modules/next/dist/bin/next'), 'start', '--hostname', '127.0.0.1', '--port', String(nextPort)], {
      cwd: process.cwd(), windowsHide: true, stdio: 'ignore', env: { ...process.env, NODE_ENV: 'production', SECURITY_CENTER_WEB_QA: '1',
        API_BASE_URL: `http://127.0.0.1:${(api.address() as AddressInfo).port}` },
    });
    let startupError = false; next.on('error', () => { startupError = true; });
    try {
      const deadline = Date.now() + 45_000;
      let ready = false;
      while (Date.now() < deadline && !ready) {
        if (startupError || next.exitCode !== null) throw new Error('The isolated Next server could not start.');
        try { ready = (await fetch(`${app.baseUrl}/login`, { signal: AbortSignal.timeout(1000) })).ok; } catch { /* startup */ }
        if (!ready) await new Promise(resolve => setTimeout(resolve, 150));
      }
      if (!ready) throw new Error('The isolated Next server did not become ready.');
      await use(app);
    } finally {
      app.release(); next.kill();
      if (next.exitCode === null) await Promise.race([once(next, 'exit'), new Promise(resolve => setTimeout(resolve, 2000))]);
      if (next.exitCode === null) next.kill('SIGKILL');
      api.closeAllConnections(); await new Promise<void>(resolve => api.close(() => resolve()));
    }
  }, { scope: 'worker', timeout: 90_000 }],
});

export { expect };
