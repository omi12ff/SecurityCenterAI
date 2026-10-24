using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace SecurityCenterAI.Api.Services.Backups;

public sealed record BackupManifest(int Version, int PostgreSqlMajor, DateTime CreatedAtUtc, BackupInventory Inventory, string DumpSha256);

public static class BackupPackage
{
    private const int MaxManifestBytes = 512 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static byte[] Pack(BackupManifest manifest, ReadOnlySpan<byte> dump)
    {
        var metadata = JsonSerializer.SerializeToUtf8Bytes(manifest, Json);
        if (metadata.Length > MaxManifestBytes || 4L + metadata.Length + dump.Length > BackupEnvelope.MaxPlaintextBytes)
            throw new InvalidDataException("Copia fuera del límite local de 64 MiB.");
        var payload = new byte[4 + metadata.Length + dump.Length];
        BinaryPrimitives.WriteInt32LittleEndian(payload, metadata.Length);
        metadata.CopyTo(payload, 4); dump.CopyTo(payload.AsSpan(4 + metadata.Length));
        return payload;
    }

    public static (BackupManifest Manifest, byte[] Dump) Unpack(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4) throw new InvalidDataException("Copia incompleta.");
        var length = BinaryPrimitives.ReadInt32LittleEndian(payload);
        if (length is <= 0 or > MaxManifestBytes || 4L + length >= payload.Length)
            throw new InvalidDataException("Metadatos de copia inválidos.");
        var manifest = JsonSerializer.Deserialize<BackupManifest>(payload.Slice(4, length), Json)
            ?? throw new InvalidDataException("Metadatos de copia ausentes.");
        var dump = payload[(4 + length)..];
        if (manifest.Version != 1 || manifest.PostgreSqlMajor != 16 || manifest.Inventory?.Tables is null ||
            manifest.Inventory.Tables.Length is < 1 or > 1000 || manifest.Inventory.Tables.Any(table =>
                string.IsNullOrEmpty(table.Name) || table.Rows < 0 || table.Sha256?.Length != 64) ||
            !dump.StartsWith("PGDMP"u8) || Convert.ToHexString(SHA256.HashData(dump)) != manifest.DumpSha256)
            throw new InvalidDataException("El contenido de la copia no es válido.");
        return (manifest, dump.ToArray());
    }
}
