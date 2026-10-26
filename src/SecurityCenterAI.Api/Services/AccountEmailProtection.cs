using System.Net.Mail;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace SecurityCenterAI.Api.Services;

public sealed class AccountEmailProtection(IDataProtectionProvider provider)
{
    public static void Configure(IServiceCollection services, IHostEnvironment environment)
    {
        if (environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        }
        else
        {
            if (!OperatingSystem.IsWindows())
                throw new InvalidOperationException("La cola cifrada actual requiere Windows y el perfil del usuario de la API. Configurá protección persistente de claves antes de desplegar en otro entorno.");
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(local) || !Path.IsPathFullyQualified(local))
                throw new InvalidOperationException("No se encontró un perfil local para proteger las claves del correo.");
            var directory = PrepareKeyDirectory(local);
            services.AddDataProtection().SetApplicationName("SecurityCenterAI.AccountEmail.v1")
                .PersistKeysToFileSystem(directory).ProtectKeysWithDpapi(protectToLocalMachine: false);
        }
        services.AddSingleton<AccountEmailProtection>();
    }

    [SupportedOSPlatform("windows")]
    private static DirectoryInfo PrepareKeyDirectory(string local)
    {
        var directory = new DirectoryInfo(Path.Combine(local, "SecurityCenterAI", "Api", "EmailKeys"));
        for (var parent = directory; parent is not null; parent = parent.Parent)
            if (parent.Exists && parent.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException("La carpeta de claves del correo no admite enlaces o uniones.");
        directory.Create();
        var permissions = new DirectorySecurity();
        permissions.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        using var identity = WindowsIdentity.GetCurrent();
        foreach (var sid in new[] { identity.User!, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
            permissions.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(permissions);
        return directory;
    }

    private IDataProtector Protector(Guid ownerId, Guid messageId) => provider.CreateProtector(
        "SecurityCenterAI.AccountEmail.Payload.v1", ownerId.ToString("N"), messageId.ToString("N"));

    public string Protect(Guid ownerId, Guid messageId, AccountEmail message)
    {
        Validate(message);
        return Protector(ownerId, messageId).Protect(JsonSerializer.Serialize(message));
    }

    public AccountEmail Unprotect(Guid ownerId, Guid messageId, string payload)
    {
        var message = JsonSerializer.Deserialize<AccountEmail>(Protector(ownerId, messageId).Unprotect(payload))
            ?? throw new CryptographicException("El correo protegido no es válido.");
        Validate(message);
        return message;
    }

    private static void Validate(AccountEmail message)
    {
        var hasToken = message.Purpose is AccountEmailPurpose.EmailVerification or AccountEmailPurpose.PasswordReset
            or AccountEmailPurpose.EmailChangeCurrent or AccountEmailPurpose.EmailChangeNew;
        if (!Enum.IsDefined(message.Purpose) || message.Recipient is null || message.Recipient.Length > 320 ||
            !MailAddress.TryCreate(message.Recipient, out var recipient) || recipient.Address != message.Recipient ||
            (hasToken ? !OpaqueToken.IsValid(message.Token) : message.Token != ""))
            throw new InvalidOperationException("El contenido del correo de cuenta no es válido.");
    }
}
