using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SecurityCenterAI.Api.Services.Backups;

// Version 1 is deliberately bounded for the local development database.
// Only authenticated encrypted bytes are written to disk by the caller.
public static class BackupEnvelope
{
    public const int MaxPlaintextBytes = 64 * 1024 * 1024;
    public const int HeaderBytes = 60; // magic/version (8), salt (32), nonce (12), length (8)
    public const int TagBytes = 16;
    private const int Iterations = 600_000;
    private static ReadOnlySpan<byte> Magic => "SCAIBK01"u8;

    public static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 16 or > 1024 || password.Contains('\r') || password.Contains('\n'))
            throw new ArgumentException("La contraseña de la copia debe tener entre 16 y 1024 caracteres, en una línea.");
    }

    public static byte[] Encrypt(ReadOnlySpan<byte> plaintext, string password)
    {
        ValidatePassword(password);
        if (plaintext.Length is <= 0 or > MaxPlaintextBytes) throw new InvalidDataException("Copia fuera del límite local de 64 MiB.");
        var envelope = new byte[HeaderBytes + TagBytes + plaintext.Length];
        Magic.CopyTo(envelope);
        RandomNumberGenerator.Fill(envelope.AsSpan(8, 32));
        RandomNumberGenerator.Fill(envelope.AsSpan(40, 12));
        BinaryPrimitives.WriteInt64LittleEndian(envelope.AsSpan(52, 8), plaintext.Length);
        var key = Derive(password, envelope.AsSpan(8, 32));
        try
        {
            using var aes = new AesGcm(key, TagBytes);
            aes.Encrypt(envelope.AsSpan(40, 12), plaintext, envelope.AsSpan(HeaderBytes + TagBytes),
                envelope.AsSpan(HeaderBytes, TagBytes), envelope.AsSpan(0, HeaderBytes));
            return envelope;
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public static byte[] Decrypt(ReadOnlySpan<byte> envelope, string password)
    {
        ValidatePassword(password);
        if (envelope.Length <= HeaderBytes + TagBytes || !envelope[..8].SequenceEqual(Magic))
            throw new InvalidDataException("Formato de copia no admitido.");
        var length = BinaryPrimitives.ReadInt64LittleEndian(envelope.Slice(52, 8));
        if (length is <= 0 or > MaxPlaintextBytes || envelope.Length != HeaderBytes + TagBytes + length)
            throw new InvalidDataException("Longitud de copia no admitida.");
        var plaintext = new byte[(int)length];
        var key = Derive(password, envelope.Slice(8, 32));
        try
        {
            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(envelope.Slice(40, 12), envelope[(HeaderBytes + TagBytes)..],
                envelope.Slice(HeaderBytes, TagBytes), plaintext, envelope[..HeaderBytes]);
            return plaintext;
        }
        catch { CryptographicOperations.ZeroMemory(plaintext); throw; }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static byte[] Derive(string password, ReadOnlySpan<byte> salt)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        try { return Rfc2898DeriveBytes.Pbkdf2(bytes, salt, Iterations, HashAlgorithmName.SHA256, 32); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
