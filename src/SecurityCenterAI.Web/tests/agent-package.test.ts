import { test, type TestContext } from "node:test";
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdtemp, readFile, rm, truncate, unlink, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { basename, dirname, join, resolve } from "node:path";
import { agentPackageStream, openVerifiedAgentPackage } from "../src/lib/agent-package-file.ts";
import { agentPairingCommand } from "../src/lib/agent-endpoint.ts";

const fileName = "SecurityCenterAI.Agent-0.4.1-preview.1-win-x64.zip";
const archive = Buffer.concat([Buffer.from([0x50, 0x4b, 0x03, 0x04]), Buffer.from("synthetic package fixture, not executable")]);
const hash = (value: Uint8Array) => createHash("sha256").update(value).digest("hex");

async function fixture(t: TestContext) {
  const directory = await mkdtemp(join(tmpdir(), "sca-agent-download-test-"));
  t.after(async () => {
    if (dirname(resolve(directory)) !== resolve(tmpdir()) || !basename(directory).startsWith("sca-agent-download-test-")) throw new Error("Unsafe test cleanup path.");
    await rm(directory, { recursive: true, force: true });
  });
  const path = join(directory, fileName);
  await writeFile(path, archive);
  await writeFile(path + ".sha256", `${hash(archive)}  ${fileName}\r\n`);
  return path;
}

test("a verified archive streams from byte zero and exposes only its public metadata", async t => {
  const path = await fixture(t);
  const prepared = await openVerifiedAgentPackage(path);
  assert.ok(prepared);
  assert.deepEqual(prepared.info, { fileName, version: "0.4.1-preview.1", bytes: archive.length, sha256: hash(archive) });
  const bytes = new Uint8Array(await new Response(agentPackageStream(prepared.file)).arrayBuffer());
  assert.equal(hash(bytes), hash(archive)); assert.equal(bytes.length, archive.length);
});

test("a missing configuration, file or checksum never provides an archive", async t => {
  assert.equal(await openVerifiedAgentPackage(undefined), null);
  assert.equal(await openVerifiedAgentPackage(""), null);
  const path = await fixture(t);
  await unlink(path + ".sha256");
  assert.equal(await openVerifiedAgentPackage(path), null);
  await unlink(path);
  assert.equal(await openVerifiedAgentPackage(path), null);
});

test("changed content and an unrelated checksum name are rejected", async t => {
  const path = await fixture(t);
  await writeFile(path, Buffer.concat([archive, Buffer.from("tampered")]));
  assert.equal(await openVerifiedAgentPackage(path), null);
  await writeFile(path, archive);
  await writeFile(path + ".sha256", `${hash(archive)}  other.zip\n`);
  assert.equal(await openVerifiedAgentPackage(path), null);
});

test("a matching hash does not permit non-ZIP data, extra checksum lines or oversized files", async t => {
  const path = await fixture(t);
  const wrongFormat = Buffer.from("a non-zip file with enough bytes to reach the minimum size");
  await writeFile(path, wrongFormat);
  await writeFile(path + ".sha256", `${hash(wrongFormat)}  ${fileName}\n`);
  assert.equal(await openVerifiedAgentPackage(path), null);
  await writeFile(path, archive);
  await writeFile(path + ".sha256", `${hash(archive)}  ${fileName}\nextra\n`);
  assert.equal(await openVerifiedAgentPackage(path), null);
  await writeFile(path + ".sha256", "x".repeat(514));
  assert.equal(await openVerifiedAgentPackage(path), null);
  await writeFile(path + ".sha256", `${hash(archive)}  ${fileName}\n`);
  await truncate(path, 128 * 1024 * 1024 + 1);
  assert.equal(await openVerifiedAgentPackage(path), null);
});

test("only the fixed agent naming convention is accepted, without reading arbitrary files", async t => {
  const path = await fixture(t);
  const other = join(dirname(path), "private.zip");
  await writeFile(other, archive); await writeFile(other + ".sha256", `${hash(archive)}  private.zip\n`);
  assert.equal(await openVerifiedAgentPackage(other), null);
  assert.deepEqual(await readFile(other), archive);
});

test("canceling a download closes its file descriptor", async t => {
  const path = await fixture(t);
  const prepared = await openVerifiedAgentPackage(path); assert.ok(prepared);
  await agentPackageStream(prepared.file).cancel();
  await assert.rejects(prepared.file.stat());
});

test("pairing commands accept root HTTPS and exact HTTP loopback hosts", () => {
  assert.equal(agentPairingCommand("http://localhost:5169"), ".\\SecurityCenterAI.Agent.exe pair --api 'http://localhost:5169'");
  assert.equal(agentPairingCommand("http://[::1]:5169/"), ".\\SecurityCenterAI.Agent.exe pair --api 'http://[::1]:5169'");
  assert.equal(agentPairingCommand("https://api.example.invalid/"), ".\\SecurityCenterAI.Agent.exe pair --api 'https://api.example.invalid'");
});

test("pairing commands reject shell syntax, credentials, non-root addresses and external HTTP", () => {
  for (const url of [undefined, "", "http://api.example.invalid", "http://localhost.example.invalid", "http://127.0.0.2", "http://localhost:0",
    "https://user:secret@example.invalid", "https://example.invalid/api", "https://example.invalid/?key=secret", "https://example.invalid/#token",
    "https://example.invalid/'$(Get-Content .env)'", "https://example.invalid/`id`", "https://example.invalid/\n", "file:///private.zip"])
    assert.equal(agentPairingCommand(url), null, "An unsafe or unsupported endpoint must not become a command.");
});
