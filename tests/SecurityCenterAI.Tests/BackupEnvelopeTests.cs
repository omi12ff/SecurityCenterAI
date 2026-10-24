using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using SecurityCenterAI.Api.Services.Backups;

namespace SecurityCenterAI.Tests;

public sealed class BackupEnvelopeTests
{
    private const string Password = "ficticia con acentos ñ y símbolos 🔐 2026";

    [Fact]
    public void RoundTripUsesUtf8PasswordAndDifferentRandomnessForEachFile()
    {
        var original = Encoding.UTF8.GetBytes("fictitious@example.invalid: información privada de prueba");
        var first = BackupEnvelope.Encrypt(original, Password);
        var second = BackupEnvelope.Encrypt(original, Password);
        Assert.False(first.SequenceEqual(second));
        Assert.Equal(original, BackupEnvelope.Decrypt(first, Password));
        Assert.DoesNotContain("fictitious@example.invalid", Encoding.UTF8.GetString(first));
    }

    [Theory]
    [InlineData(8)] // salt
    [InlineData(40)] // nonce
    [InlineData(60)] // tag
    [InlineData(76)] // ciphertext
    public void AlteredAuthenticatedBytesAreRejected(int offset)
    {
        var file = BackupEnvelope.Encrypt("PGDMP fixture"u8, Password);
        file[offset] ^= 0x01;
        Assert.Throws<AuthenticationTagMismatchException>(() => BackupEnvelope.Decrypt(file, Password));
    }

    [Fact]
    public void WrongPasswordNeverReturnsPlaintext()
    {
        var file = BackupEnvelope.Encrypt("PGDMP fixture"u8, Password);
        Assert.Throws<AuthenticationTagMismatchException>(() => BackupEnvelope.Decrypt(file, "otra contrasena ficticia muy larga"));
    }

    [Fact]
    public void TruncatedOrForgedLengthFailsBeforeAnUnboundedAllocation()
    {
        var file = BackupEnvelope.Encrypt("PGDMP fixture"u8, Password);
        Assert.Throws<InvalidDataException>(() => BackupEnvelope.Decrypt(file[..30], Password));
        BinaryPrimitives.WriteInt64LittleEndian(file.AsSpan(52, 8), long.MaxValue);
        Assert.Throws<InvalidDataException>(() => BackupEnvelope.Decrypt(file, Password));
    }

    [Fact]
    public void ManifestCannotDescribeDifferentDumpBytes()
    {
        var inventory = new BackupInventory(new string('A', 64), [new("users", 1, new string('B', 64))]);
        var manifest = new BackupManifest(1, 16, DateTime.UtcNow, inventory, new string('C', 64));
        var payload = BackupPackage.Pack(manifest, "PGDMP fixture"u8);
        Assert.Throws<InvalidDataException>(() => BackupPackage.Unpack(payload));
    }

    [Fact]
    public void OutputDirectoryCannotChangePermissionsOnTheRepositoryOrUnrelatedFolders()
    {
        var repository = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fictitious-repository"));
        Assert.Throws<InvalidOperationException>(() => BackupFiles.ValidateDirectory(repository, repository));
        Assert.Throws<InvalidOperationException>(() => BackupFiles.ValidateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), repository));
    }

    [Fact]
    public async Task InvalidCliArgumentsAreRejectedWithoutReadingAPasswordOrOpeningADatabase() =>
        Assert.Equal(2, await BackupCommand.RunAsync(["--backup-verify"]));
}
