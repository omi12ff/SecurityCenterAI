using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace SecurityCenterAI.Api.Services.Backups;

public static class BackupFiles
{
    public static string ValidateDirectory(string directory, string repository)
    {
        var resolved = Path.GetFullPath(directory);
        var store = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecurityCenterAI", "Backups");
        if (!Within(resolved, store))
            throw new InvalidOperationException("La carpeta debe estar dentro de LocalAppData/SecurityCenterAI/Backups.");
        if (Within(resolved, Path.GetFullPath(repository)) || Path.GetPathRoot(resolved) == resolved)
            throw new InvalidOperationException("Guardá las copias fuera del repositorio.");
        foreach (var variable in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
        {
            var sync = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrEmpty(sync) && Within(resolved, Path.GetFullPath(sync)))
                throw new InvalidOperationException("Guardá las copias fuera de OneDrive.");
        }
        for (var parent = new DirectoryInfo(resolved); parent is not null; parent = parent.Parent)
            if (parent.Exists && parent.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException("La carpeta de copias no admite enlaces o uniones.");
        return resolved;
    }

    [SupportedOSPlatform("windows")]
    public static string PrepareDirectory(string directory, string repository)
    {
        var resolved = ValidateDirectory(directory, repository);
        var folder = Directory.CreateDirectory(resolved);
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        using var identity = WindowsIdentity.GetCurrent();
        foreach (var sid in new[] { identity.User!, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
        folder.SetAccessControl(acl);
        return resolved;
    }

    private static bool Within(string target, string root) =>
        target.Equals(root, StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
