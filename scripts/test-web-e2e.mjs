import { spawn, spawnSync } from 'node:child_process';
import { randomBytes, randomUUID } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync, mkdirSync, openSync, closeSync } from 'node:fs';
import { createServer } from 'node:net';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

// Own database, processes and temporary credentials. Production/development
// services and their rate-limit settings are not changed by this runner.
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const webRoot = join(root, 'src', 'SecurityCenterAI.Web');
const nextEnvPath = join(webRoot, 'next-env.d.ts');
const originalNextEnv = existsSync(nextEnvPath) ? readFileSync(nextEnvPath) : null;
const args = process.argv.slice(2);
let configuration = 'WebE2eQa', noBuild = false, unavailablePackage = false;
const playwrightArgs = [];
for (let index = 0; index < args.length; index++) {
  if (args[index] === '--') { playwrightArgs.push(...args.slice(index + 1)); break; }
  if (args[index] === '--no-build') noBuild = true;
  else if (args[index] === '--agent-package-unavailable') unavailablePackage = true;
  else if (args[index] === '--configuration' && index + 1 < args.length) configuration = args[++index];
  else throw new Error('Unsupported E2E runner argument.');
}
if (!configuration || !/^[A-Za-z0-9][A-Za-z0-9_-]*$/.test(configuration)) throw new Error('Invalid test build configuration.');
const database = `scai_e2e_test_${randomUUID().replaceAll('-', '')}`;
if (!/^scai_e2e_test_[a-f0-9]{32}$/.test(database)) throw new Error('Invalid isolated database namespace.');
const artifactRoot = join(root, 'artifacts', 'web-e2e', database);
mkdirSync(artifactRoot, { recursive: true });
const processes = [];
const handles = [];
let databaseCreated = false;
let interrupted = false;
process.once('SIGINT', () => { interrupted = true; for (const child of processes) if (child.exitCode === null) child.kill(); });

function databaseSettings() {
  const names = new Set(['POSTGRES_DB', 'POSTGRES_USER', 'POSTGRES_PASSWORD']);
  const settings = {};
  for (const original of readFileSync(join(root, '.env'), 'utf8').split(/\r?\n/)) {
    const line = original.trim(); if (!line || line.startsWith('#')) continue;
    const split = line.indexOf('='); if (split < 1) throw new Error('Invalid local database configuration.');
    const name = line.slice(0, split).trim(); if (!names.has(name)) continue;
    if (Object.hasOwn(settings, name)) throw new Error('Duplicate local database setting.');
    let value = line.slice(split + 1).trim();
    if (value.startsWith('"') || value.startsWith("'")) {
      if (!value.endsWith(value[0]) || value.length < 2) throw new Error('Invalid quoted database setting.');
      value = value.slice(1, -1);
    }
    settings[name] = value;
  }
  if ([...names].some(name => !settings[name])) throw new Error('Missing local database configuration.');
  if (settings.POSTGRES_DB === database) throw new Error('Isolated database must differ from the active database.');
  return settings;
}

function dockerDatabase(action) {
  if (!/^scai_e2e_test_[a-f0-9]{32}$/.test(database)) throw new Error('Database ownership check failed.');
  const script = action === 'create' ? 'exec createdb --username="$POSTGRES_USER" --template=template0 "$1"' : 'exec dropdb --force --username="$POSTGRES_USER" "$1"';
  const child = spawnSync('docker', ['compose', 'exec', '-T', 'postgres', 'sh', '-c', script, 'owned-e2e', database],
    { cwd: root, windowsHide: true, encoding: 'utf8', timeout: 30_000 });
  if (child.error || child.status !== 0) throw new Error(`Could not ${action} the isolated E2E database; database credentials are not printed.`);
}

async function freePort() {
  const server = createServer();
  await new Promise((accept, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', accept); });
  const port = server.address().port;
  await new Promise((accept, reject) => server.close(error => error ? reject(error) : accept()));
  return port;
}

function run(command, commandArgs, options = {}) {
  return new Promise((accept, reject) => {
    const child = spawn(command, commandArgs, { cwd: root, windowsHide: true, stdio: 'inherit', ...options });
    processes.push(child);
    child.once('error', () => reject(new Error('Could not start an E2E command.')));
    child.once('exit', code => code === 0 ? accept() : reject(new Error(`E2E command failed with code ${code}.`)));
  });
}

function start(command, commandArgs, cwd, env, name) {
  const out = openSync(join(artifactRoot, `${name}.stdout.log`), 'wx');
  const err = openSync(join(artifactRoot, `${name}.stderr.log`), 'wx');
  handles.push(out, err);
  const child = spawn(command, commandArgs, { cwd, env, windowsHide: true, stdio: ['ignore', out, err] });
  processes.push(child);
  child.on('error', () => { child.startFailed = true; });
  return child;
}

async function ready(address, child) {
  const until = Date.now() + 60_000;
  while (Date.now() < until) {
    if (interrupted) throw new Error('Isolated E2E interrupted.');
    if (child.startFailed || child.exitCode !== null) throw new Error('An isolated E2E server exited during startup; see ignored local logs.');
    try { if ((await fetch(address, { signal: AbortSignal.timeout(1500) })).ok) return; } catch { /* startup */ }
    await new Promise(accept => setTimeout(accept, 150));
  }
  throw new Error('An isolated E2E server did not become ready.');
}

async function stopOwnedProcesses() {
  for (const child of processes.reverse()) {
    if (child.exitCode !== null) continue;
    child.kill();
    await Promise.race([new Promise(accept => child.once('exit', accept)), new Promise(accept => setTimeout(accept, 3000))]);
    if (child.exitCode === null) child.kill('SIGKILL');
  }
  for (const handle of handles) closeSync(handle);
}

try {
  const settings = databaseSettings();
  const apiAssembly = join(root, 'src', 'SecurityCenterAI.Api', 'bin', configuration, 'net8.0', 'SecurityCenterAI.Api.dll');
  if (!noBuild) {
    await run('dotnet', ['build', 'src/SecurityCenterAI.Api/SecurityCenterAI.Api.csproj', '--configuration', configuration, '--nologo', '--verbosity', 'quiet']);
    await run(process.execPath, [join(webRoot, 'node_modules', 'next', 'dist', 'bin', 'next'), 'build', '--webpack'],
      { cwd: webRoot, env: { ...process.env, SECURITY_CENTER_WEB_QA: '1' } });
  }
  if (!existsSync(apiAssembly) || !existsSync(join(webRoot, '.next-qa', 'BUILD_ID'))) throw new Error('Build the selected API and QA web output before --no-build.');
  const apiPort = await freePort(), webPort = await freePort();
  if (apiPort === webPort) throw new Error('E2E server ports must differ; retry the runner.');
  const apiUrl = `http://127.0.0.1:${apiPort}`, webUrl = `http://127.0.0.1:${webPort}`;
  const quote = value => `"${value.replaceAll('"', '""')}"`;
  const connection = `Host=127.0.0.1;Port=5432;Database=${database};Username=${quote(settings.POSTGRES_USER)};Password=${quote(settings.POSTGRES_PASSWORD)}`;
  const env = { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_ENVIRONMENT: 'Development',
    ConnectionStrings__PostgreSQL: connection, Jwt__Key: randomBytes(64).toString('base64'),
    FrontendUrl: webUrl, Cors__AllowedOrigins__0: webUrl, AiExplanation__Enabled: 'false', Retention__Enabled: 'false',
    Email__Host: '127.0.0.1', Email__Port: '1025', RateLimits__WindowSeconds: '2',
    RateLimits__GlobalPermitLimit: '1200', RateLimits__AuthPermitLimit: '600',
    RateLimits__AnalysisPermitLimit: '600', RateLimits__AgentPairPermitLimit: '600',
    RateLimits__DevicePermitLimit: '600', RateLimits__AccountExportPermitLimit: '60',
    RateLimits__AccountDeletionPermitLimit: '60', RateLimits__AccountPasswordPermitLimit: '60',
    RateLimits__AccountEmailChangePermitLimit: '60', RateLimits__EmailPermitLimit: '400' };
  dockerDatabase('create'); databaseCreated = true;
  await run('dotnet', ['ef', 'database', 'update', '--project', 'src/SecurityCenterAI.Infrastructure', '--startup-project', 'src/SecurityCenterAI.Api', '--configuration', configuration, '--no-build'], { env });
  const api = start('dotnet', [apiAssembly, '--urls', apiUrl], join(root, 'src', 'SecurityCenterAI.Api'), env, 'api');
  await ready(`${apiUrl}/health/ready`, api);
  const webEnv = { ...process.env, SECURITY_CENTER_WEB_QA: '1', API_BASE_URL: apiUrl, AGENT_API_URL: apiUrl, NODE_ENV: 'production' };
  if (unavailablePackage) {
    webEnv.AGENT_PACKAGE_PATH = join(artifactRoot, 'intentionally-missing-agent.zip');
    webEnv.AGENT_API_URL = '';
  }
  const web = start(process.execPath, [join(webRoot, 'node_modules', 'next', 'dist', 'bin', 'next'), 'start', '--hostname', '127.0.0.1', '--port', String(webPort)],
    webRoot, webEnv, 'web');
  await ready(`${webUrl}/login`, web);
  console.log('Isolated E2E environment ready: own database, generated JWT key, local Mailpit and AI disabled. Main application unchanged.');
  const selectedTests = unavailablePackage && playwrightArgs.length === 0
    ? ['e2e/getting-started-unavailable.spec.ts'] : playwrightArgs;
  await run(process.execPath, [join(webRoot, 'node_modules', '@playwright', 'test', 'cli.js'), 'test', ...selectedTests],
    { cwd: webRoot, env: { ...process.env, E2E_BASE_URL: webUrl, E2E_API_BASE_URL: apiUrl,
      E2E_MAILPIT_URL: 'http://127.0.0.1:8025', E2E_DATABASE_NAME: database,
      E2E_AGENT_PACKAGE_UNAVAILABLE: unavailablePackage ? '1' : '0' } });
} catch (error) {
  console.error(error instanceof Error ? error.message : 'Isolated E2E failed.');
  process.exitCode = 1;
} finally {
  try { await stopOwnedProcesses(); }
  catch { console.error('Could not completely stop the owned E2E processes; inspect local logs.'); process.exitCode = 1; }
  if (databaseCreated) {
    try { dockerDatabase('drop'); console.log('Owned E2E database removed.'); }
    catch (error) { console.error(error.message); process.exitCode = 1; }
  }
  if (originalNextEnv !== null) {
    try { writeFileSync(nextEnvPath, originalNextEnv); }
    catch { console.error('Could not restore next-env.d.ts after E2E.'); process.exitCode = 1; }
  }
}
