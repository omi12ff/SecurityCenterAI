// Node-only implementation. The server-only wrapper supplies the trusted configuration.
import { createHash } from "node:crypto";
import { lstat, open, type FileHandle } from "node:fs/promises";
import { basename, resolve } from "node:path";

export type AgentPackageInfo = { fileName: string; version: string; bytes: number; sha256: string };
const MAX_PACKAGE_BYTES = 128 * 1024 * 1024;

export function agentPackageStream(file: FileHandle): ReadableStream<Uint8Array> {
  // Node and lib.dom declare different pipeThrough overloads for the same Web Stream.
  return file.readableWebStream({ autoClose: true }) as unknown as ReadableStream<Uint8Array>;
}

/** No request paths or URLs: only a ZIP explicitly selected by the server operator. */
export async function openVerifiedAgentPackage(configuredPath: string | undefined): Promise<{ info: AgentPackageInfo; file: FileHandle } | null> {
  if (!configuredPath?.trim()) return null;
  let file: FileHandle | undefined;
  let checksum: FileHandle | undefined;
  try {
    const archivePath = resolve(configuredPath);
    const fileName = basename(archivePath);
    const match = /^SecurityCenterAI\.Agent-(\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)-win-x64\.zip$/.exec(fileName);
    if (!match || fileName.length > 150) return null;
    const checksumPath = archivePath + ".sha256";
    const [archiveEntry, checksumEntry] = await Promise.all([lstat(archivePath), lstat(checksumPath)]);
    if (!archiveEntry.isFile() || !checksumEntry.isFile() || archiveEntry.isSymbolicLink() || checksumEntry.isSymbolicLink()) return null;
    file = await open(archivePath, "r");
    checksum = await open(checksumPath, "r");
    const [before, checksumStat] = await Promise.all([file.stat(), checksum.stat()]);
    if (!before.isFile() || before.size < 22 || before.size > MAX_PACKAGE_BYTES || !checksumStat.isFile() || checksumStat.size > 512) return null;
    // Bounded positional reads leave the archive at offset zero for the response stream.
    const checksumBuffer = Buffer.alloc(513);
    const checksumRead = await checksum.read(checksumBuffer, 0, checksumBuffer.length, 0);
    const checksumText = checksumBuffer.subarray(0, checksumRead.bytesRead).toString("utf8");
    const expectedHash = checksumText.slice(0, 64);
    if (!/^[a-f0-9]{64}$/.test(expectedHash) ||
      ![`${expectedHash}  ${fileName}\n`, `${expectedHash}  ${fileName}\r\n`].includes(checksumText)) return null;
    const buffer = Buffer.alloc(64 * 1024);
    const hash = createHash("sha256");
    let offset = 0;
    while (offset < before.size) {
      const { bytesRead } = await file.read(buffer, 0, Math.min(buffer.length, before.size - offset), offset);
      if (bytesRead === 0 || (offset === 0 && (bytesRead < 4 || buffer.readUInt32LE(0) !== 0x04034b50))) return null;
      hash.update(buffer.subarray(0, bytesRead)); offset += bytesRead;
    }
    const after = await file.stat();
    if (after.size !== before.size || after.mtimeMs !== before.mtimeMs || after.ctimeMs !== before.ctimeMs || hash.digest("hex") !== expectedHash) return null;
    const result = { info: { fileName, version: match[1], bytes: before.size, sha256: expectedHash }, file };
    file = undefined; // The caller owns this handle, including when the client cancels.
    return result;
  } catch {
    // Missing, unreadable or inconsistent packages share a fixed public response.
    return null;
  } finally {
    await Promise.all([file?.close(), checksum?.close()]);
  }
}
