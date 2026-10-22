using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SecurityCenterAI.Agent.Checks;

/// <summary>
/// Executes only the fixed local read-only queries below. Raw shell output and errors never
/// become report evidence. No script, path, command or argument comes from report/user data.
/// </summary>
public sealed class PowerShellWindowsProbe : IWindowsProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(12);
    private const int MaximumOutputCharacters = 8192;

    public async Task<ProbeResult> ProbeAsync(WindowsProbeKind kind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            return new ProbeResult(ProbeOutcome.NotApplicable);

        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (string.IsNullOrEmpty(systemDirectory) || !Path.IsPathFullyQualified(systemDirectory))
            return new ProbeResult(ProbeOutcome.Unavailable);

        // Sysnative bypasses WOW64 redirection if a 32-bit process runs on a 64-bit OS.
        var nativeDirectory = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
            ? Path.Combine(Path.GetDirectoryName(systemDirectory)!, "Sysnative")
            : systemDirectory;
        var powershellDirectory = Path.Combine(nativeDirectory, "WindowsPowerShell", "v1.0");
        var executable = Path.Combine(powershellDirectory, "powershell.exe");
        if (!File.Exists(executable))
            return new ProbeResult(ProbeOutcome.Unavailable);

        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = nativeDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("-NoLogo");
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(BuildScript(kind));
        // Never auto-load modules from the current directory, user profile, or inherited paths.
        start.Environment["PSModulePath"] = Path.Combine(powershellDirectory, "Modules");

        using var process = new Process { StartInfo = start };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        try
        {
            if (!process.Start())
                return new ProbeResult(ProbeOutcome.Unavailable);

            var outputTask = ReadBoundedAsync(process.StandardOutput, MaximumOutputCharacters, timeout.Token);
            var errorTask = ReadBoundedAsync(process.StandardError, 2048, timeout.Token);
            await Task.WhenAll(process.WaitForExitAsync(timeout.Token), outputTask, errorTask);
            cancellationToken.ThrowIfCancellationRequested();
            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(await errorTask))
                return new ProbeResult(ProbeOutcome.Unavailable);

            return ParseEnvelope(await outputTask);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StopOwnedProcess(process);
            throw;
        }
        catch (OperationCanceledException)
        {
            StopOwnedProcess(process);
            return new ProbeResult(ProbeOutcome.Timeout);
        }
        catch (OutputLimitException)
        {
            StopOwnedProcess(process);
            return new ProbeResult(ProbeOutcome.InvalidOutput);
        }
        catch (UnauthorizedAccessException)
        {
            StopOwnedProcess(process);
            return new ProbeResult(ProbeOutcome.AccessDenied);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            StopOwnedProcess(process);
            return new ProbeResult(ProbeOutcome.Unavailable);
        }
    }

    private static ProbeResult ParseEnvelope(string output)
    {
        try
        {
            using var document = JsonDocument.Parse(output, new JsonDocumentOptions { MaxDepth = 5 });
            var envelope = document.RootElement;
            if (envelope.ValueKind != JsonValueKind.Object || !envelope.TryGetProperty("outcome", out var outcome)
                || outcome.ValueKind != JsonValueKind.String)
                return new ProbeResult(ProbeOutcome.InvalidOutput);

            return outcome.GetString() switch
            {
                "success" when envelope.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                    => new ProbeResult(ProbeOutcome.Success, data.GetRawText()),
                "denied" => new ProbeResult(ProbeOutcome.AccessDenied),
                "notApplicable" => new ProbeResult(ProbeOutcome.NotApplicable),
                "unavailable" => new ProbeResult(ProbeOutcome.Unavailable),
                _ => new ProbeResult(ProbeOutcome.InvalidOutput)
            };
        }
        catch (JsonException)
        {
            return new ProbeResult(ProbeOutcome.InvalidOutput);
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int maximum, CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var buffer = new char[512];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0)
                return output.ToString();
            if (output.Length + count > maximum)
                throw new OutputLimitException();
            output.Append(buffer, 0, count);
        }
    }

    private static void StopOwnedProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            // This handle belongs exclusively to this query. Never search for or kill by name.
        }
    }

    private static string BuildScript(WindowsProbeKind kind)
    {
        var body = kind switch
        {
            WindowsProbeKind.DefenderAntivirus => """
                $status = Defender\Get-MpComputerStatus -ErrorAction Stop
                @{ antivirusEnabled = (As-Boolean $status.AntivirusEnabled); serviceEnabled = (As-Boolean $status.AMServiceEnabled) }
                """,
            WindowsProbeKind.DefenderRealtime => """
                $status = Defender\Get-MpComputerStatus -ErrorAction Stop
                @{ realtimeEnabled = (As-Boolean $status.RealTimeProtectionEnabled) }
                """,
            WindowsProbeKind.DefenderSignatures => """
                $status = Defender\Get-MpComputerStatus -ErrorAction Stop
                $updated = $null
                if ($status.AntivirusSignatureLastUpdated -is [datetime]) {
                    $updated = $status.AntivirusSignatureLastUpdated.ToUniversalTime().ToString('o', [Globalization.CultureInfo]::InvariantCulture)
                }
                @{ signatureUpdatedAtUtc = $updated }
                """,
            WindowsProbeKind.FirewallProfiles => """
                $profiles = @(NetSecurity\Get-NetFirewallProfile -PolicyStore ActiveStore -ErrorAction Stop)
                function Profile-Enabled([string]$name) {
                    $found = @($profiles | Microsoft.PowerShell.Core\Where-Object { $_.Name -eq $name })
                    if ($found.Count -ne 1) { return $null }
                    return (As-Boolean $found[0].Enabled)
                }
                @{ domain = (Profile-Enabled 'Domain'); private = (Profile-Enabled 'Private'); public = (Profile-Enabled 'Public') }
                """,
            WindowsProbeKind.SecureBoot => """
                @{ enabled = (As-Boolean (SecureBoot\Confirm-SecureBootUEFI -ErrorAction Stop)) }
                """,
            WindowsProbeKind.Tpm => """
                $status = TrustedPlatformModule\Get-Tpm -ErrorAction Stop
                @{ present = (As-Boolean $status.TpmPresent); ready = (As-Boolean $status.TpmReady) }
                """,
            WindowsProbeKind.BitLocker => """
                $systemVolume = [IO.Path]::GetPathRoot([Environment]::SystemDirectory).TrimEnd('\')
                $volumes = @(BitLocker\Get-BitLockerVolume -MountPoint $systemVolume -ErrorAction Stop)
                if ($volumes.Count -ne 1) { throw [InvalidOperationException]::new() }
                $status = $volumes[0]
                @{ protectionStatus = (As-Integer $status.ProtectionStatus); volumeStatus = (As-Integer $status.VolumeStatus); encryptionPercentage = (As-Integer $status.EncryptionPercentage) }
                """,
            WindowsProbeKind.WindowsUpdateService => """
                $services = @(CimCmdlets\Get-CimInstance -Namespace 'root/cimv2' -ClassName Win32_Service -Filter "Name='wuauserv'" -Property StartMode,State -ErrorAction Stop)
                if ($services.Count -ne 1) { throw [InvalidOperationException]::new() }
                @{ startMode = $services[0].StartMode; state = $services[0].State }
                """,
            WindowsProbeKind.Uac => """
                $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey('SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System', $false)
                try {
                    $enabled = $null
                    if ($null -ne $key) { $enabled = $key.GetValue('EnableLUA', $null) }
                    @{ enabled = (As-Integer $enabled) }
                } finally { if ($null -ne $key) { $key.Dispose() } }
                """,
            WindowsProbeKind.Smb1 => """
                $status = SmbShare\Get-SmbServerConfiguration -ErrorAction Stop
                @{ enabled = (As-Boolean $status.EnableSMB1Protocol) }
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        // Body is selected solely from this enum. No interpolation of external strings is allowed.
        return """
            & {
                $ErrorActionPreference = 'Stop'
                $ProgressPreference = 'SilentlyContinue'
                $WarningPreference = 'SilentlyContinue'
                $VerbosePreference = 'SilentlyContinue'
                $DebugPreference = 'SilentlyContinue'
                [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
                function As-Boolean($value) {
                    if ($null -eq $value) { return $null }
                    if ($value -is [bool]) { return $value }
                    if ($value -is [Enum]) {
                        if ($value.ToString() -eq 'True') { return $true }
                        if ($value.ToString() -eq 'False') { return $false }
                    }
                    return $null
                }
                function As-Integer($value) {
                    if ($null -eq $value) { return $null }
                    if ($value -is [int] -or $value -is [uint32] -or $value -is [Enum]) { return [int]$value }
                    return $null
                }
                try {
                    $data = & {
            """ + body + """

                    }
                    @{ outcome = 'success'; data = $data } | Microsoft.PowerShell.Utility\ConvertTo-Json -Compress -Depth 4
                } catch {
                    $category = $_.CategoryInfo.Category.ToString()
                    if ($_.Exception -is [UnauthorizedAccessException] -or $category -eq 'PermissionDenied' -or $category -eq 'SecurityError') {
                        @{ outcome = 'denied' } | Microsoft.PowerShell.Utility\ConvertTo-Json -Compress
                    } elseif ($_.Exception -is [PlatformNotSupportedException]) {
                        @{ outcome = 'notApplicable' } | Microsoft.PowerShell.Utility\ConvertTo-Json -Compress
                    } else {
                        @{ outcome = 'unavailable' } | Microsoft.PowerShell.Utility\ConvertTo-Json -Compress
                    }
                }
            }
            """;
    }

    private sealed class OutputLimitException : Exception { }
}
