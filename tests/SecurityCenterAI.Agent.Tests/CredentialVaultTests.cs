using System.Text;
using SecurityCenterAI.Agent;
using SecurityCenterAI.Agent.Security;

namespace SecurityCenterAI.Agent.Tests;

public sealed class CredentialVaultTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 15, 0, 0, TimeSpan.Zero);
    private static readonly string Token = new('C', 43);

    [Fact]
    public async Task Save_EncryptsTheCredentialWithDpapiAndCanBeReadByTheCurrentWindowsUser()
    {
        using var directory = new IsolatedDirectory();
        var vault = new CredentialVault(directory.Path, new FixedClock(Now));
        var original = Credential();

        await vault.SaveAsync(original, CancellationToken.None);
        var encrypted = await File.ReadAllBytesAsync(vault.CredentialPath);
        var loaded = await vault.LoadAsync(CancellationToken.None);

        Assert.NotEmpty(encrypted);
        Assert.True(encrypted.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Token)) < 0);
        Assert.True(encrypted.AsSpan().IndexOf(Encoding.UTF8.GetBytes(original.ApiBaseUri.AbsoluteUri)) < 0);
        Assert.NotNull(loaded);
        Assert.Equal(original.ApiBaseUri, loaded.ApiBaseUri);
        Assert.Equal(original.DeviceId, loaded.DeviceId);
        Assert.Equal(original.ExpiresAtUtc, loaded.ExpiresAtUtc);
        Assert.True(loaded.DeviceToken == Token);
        Assert.False(loaded.ToString()!.Contains(Token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Load_RejectsTamperingWithoutReturningPartiallyRecoveredCredentials()
    {
        using var directory = new IsolatedDirectory();
        var vault = new CredentialVault(directory.Path, new FixedClock(Now));
        await vault.SaveAsync(Credential(), CancellationToken.None);
        var encrypted = await File.ReadAllBytesAsync(vault.CredentialPath);
        encrypted[^1] ^= 0x80;
        await File.WriteAllBytesAsync(vault.CredentialPath, encrypted);

        var exception = await Assert.ThrowsAsync<AgentException>(() => vault.LoadAsync(CancellationToken.None));

        Assert.False(exception.ToString().Contains(Token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Load_RejectsUnencryptedJsonRatherThanAcceptingAnImportedToken()
    {
        using var directory = new IsolatedDirectory();
        var vault = new CredentialVault(directory.Path, new FixedClock(Now));
        await File.WriteAllTextAsync(vault.CredentialPath,
            $"{{\"apiBaseUri\":\"https://api.example.invalid/\",\"deviceToken\":\"{Token}\"}}");

        var exception = await Assert.ThrowsAsync<AgentException>(() => vault.LoadAsync(CancellationToken.None));

        Assert.False(exception.ToString().Contains(Token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Load_RejectsAStoredCredentialAtItsExactExpirationTime()
    {
        using var directory = new IsolatedDirectory();
        var expiry = Now.AddDays(90);
        var vault = new CredentialVault(directory.Path, new FixedClock(Now));
        await vault.SaveAsync(Credential(expiry), CancellationToken.None);
        var laterVault = new CredentialVault(directory.Path, new FixedClock(expiry));

        await Assert.ThrowsAsync<AgentException>(() => laterVault.LoadAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(92)]
    public async Task Save_RejectsExpiredOrUnboundedCredentialsBeforeWritingThem(int days)
    {
        using var directory = new IsolatedDirectory();
        var vault = new CredentialVault(directory.Path, new FixedClock(Now));

        await Assert.ThrowsAsync<AgentException>(() =>
            vault.SaveAsync(Credential(Now.AddDays(days)), CancellationToken.None));

        Assert.False(File.Exists(vault.CredentialPath));
    }

    [Fact]
    public async Task Forget_RemovesOnlyTheLocalCredentialAndIsIdempotent()
    {
        using var directory = new IsolatedDirectory();
        var vault = new CredentialVault(directory.Path, new FixedClock(Now));
        var unrelatedPath = System.IO.Path.Combine(directory.Path, "unrelated.txt");
        await File.WriteAllTextAsync(unrelatedPath, "Keep this file.");
        Assert.Null(await vault.LoadAsync(CancellationToken.None));
        await vault.SaveAsync(Credential(), CancellationToken.None);

        await vault.ForgetAsync(CancellationToken.None);
        await vault.ForgetAsync(CancellationToken.None);

        Assert.False(File.Exists(vault.CredentialPath));
        Assert.Null(await vault.LoadAsync(CancellationToken.None));
        Assert.Equal("Keep this file.", await File.ReadAllTextAsync(unrelatedPath));
    }

    [Fact]
    public void Lock_PreventsConcurrentOperationsAcrossVaultInstancesAndReleasesOnDispose()
    {
        using var directory = new IsolatedDirectory();
        var firstVault = new CredentialVault(directory.Path, new FixedClock(Now));
        var secondVault = new CredentialVault(directory.Path, new FixedClock(Now));

        using (firstVault.AcquireLock())
        {
            Assert.Throws<AgentException>(() => secondVault.AcquireLock());
        }

        using var releasedLock = secondVault.AcquireLock();
        Assert.NotNull(releasedLock);
    }

    private static DeviceCredential Credential(DateTimeOffset? expiresAtUtc = null) =>
        new(new Uri("https://api.example.invalid/"), Guid.NewGuid(), Token, expiresAtUtc ?? Now.AddDays(90));

    private sealed class IsolatedDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "securitycenterai-agent-tests-" + Guid.NewGuid().ToString("N"));

        public IsolatedDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            // This directory is generated solely for this fixture. There is no recursive deletion.
            foreach (var file in Directory.EnumerateFiles(Path))
                File.Delete(file);
            Directory.Delete(Path);
        }
    }
}
