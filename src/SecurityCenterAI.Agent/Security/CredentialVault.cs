using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SecurityCenterAI.Agent.Security;

public sealed class CredentialVault
{
    private const int MaximumBytes = 16 * 1024;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SecurityCenterAI.Agent.Credential.v1");
    private readonly string directory;
    private readonly TimeProvider clock;

    public CredentialVault(string storageDirectory, TimeProvider clock)
    {
        directory = Path.GetFullPath(storageDirectory);
        this.clock = clock;
    }

    public string CredentialPath => Path.Combine(directory, "credential.enc");

    public IDisposable AcquireLock()
    {
        EnsureDirectory();
        try
        {
            var lockPath = Path.Combine(directory, ".agent.lock");
            if (File.Exists(lockPath)) EnsureNotLinked(lockPath);
            return new FileStream(lockPath, FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException) { throw new AgentException("Hay otra operación del agente en curso. Esperá y volvé a intentar."); }
    }

    public async Task<DeviceCredential?> LoadAsync(CancellationToken cancellationToken)
    {
        EnsureDirectory();
        if (!File.Exists(CredentialPath)) return null;
        EnsureNotLinked(CredentialPath);
        byte[]? plaintext = null;
        try
        {
            await using var stream = new FileStream(CredentialPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > MaximumBytes) throw Invalid();
            var ciphertext = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(ciphertext, cancellationToken);
            plaintext = ProtectedData.Unprotect(ciphertext, Entropy, DataProtectionScope.CurrentUser);
            if (plaintext.Length > MaximumBytes) throw Invalid();
            var stored = JsonSerializer.Deserialize<StoredCredential>(plaintext) ?? throw Invalid();
            if (stored.SchemaVersion != 1) throw Invalid();
            var credential = new DeviceCredential(ApiEndpoint.Parse(stored.ApiBaseUrl), stored.DeviceId,
                stored.DeviceToken, stored.ExpiresAtUtc);
            credential.ValidateExpiry(clock);
            return credential;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or
            NotSupportedException or ArgumentException or EndOfStreamException)
        {
            throw Invalid();
        }
        finally { if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext); }
    }

    public async Task SaveAsync(DeviceCredential credential, CancellationToken cancellationToken)
    {
        EnsureDirectory();
        credential.ValidateExpiry(clock);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(new StoredCredential(1,
            credential.ApiBaseUri.AbsoluteUri, credential.DeviceId, credential.DeviceToken, credential.ExpiresAtUtc));
        var temporary = Path.Combine(directory, $"credential-{Guid.NewGuid():N}.tmp");
        var created = false;
        try
        {
            var ciphertext = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                await stream.WriteAsync(ciphertext, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(CredentialPath)) EnsureNotLinked(CredentialPath);
            File.Move(temporary, CredentialPath, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (created && File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public Task ForgetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDirectory();
        if (File.Exists(CredentialPath))
        {
            EnsureNotLinked(CredentialPath);
            File.Delete(CredentialPath);
        }
        return Task.CompletedTask;
    }

    private void EnsureDirectory()
    {
        // Reject junctions/symlinks in the storage chain; never follow an alternate credential location.
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw Invalid();
        Directory.CreateDirectory(directory);
    }

    private static void EnsureNotLinked(string path)
    {
        if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw Invalid();
    }

    private static AgentException Invalid() => new(
        "No se pudo leer la credencial local de forma segura. Usá forget y vinculá de nuevo con un código nuevo.");

    private sealed record StoredCredential(int SchemaVersion, string ApiBaseUrl, Guid DeviceId,
        string DeviceToken, DateTimeOffset ExpiresAtUtc);
}
