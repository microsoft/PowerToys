// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.Mxc.Sdk;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.Worker;

internal static class SandboxRequestFactory
{
    public static SandboxRequest Create(ExecutionRequest input)
    {
        input.Validate();
        RunSession.ValidateDirectories(input.WorkingDirectory, input.TemporaryDirectory);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var powerShellDirectory = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0");
        var executable = Path.Combine(powerShellDirectory, "powershell.exe");
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("Windows PowerShell is unavailable.", executable);
        }

        var profile = Path.Combine(input.TemporaryDirectory, "Profile");
        var appData = Path.Combine(profile, "AppData");
        Directory.CreateDirectory(appData);
        var policy = new SandboxPolicy
        {
            Version = "0.8.0-alpha",
            Filesystem = new FilesystemPolicy
            {
                ReadonlyPaths = [powerShellDirectory],
                ReadwritePaths = [input.WorkingDirectory, input.TemporaryDirectory],
                ClearPolicyOnExit = true,
            },
            Network = new NetworkPolicy { AllowOutbound = false, AllowLocalNetwork = false },
            Ui = new UiPolicy { AllowWindows = true, Clipboard = ClipboardPolicy.None, AllowInputInjection = false },
            TimeoutMs = checked((uint)input.TimeoutSeconds * 1000),
        };

        // Telemetry is opt-in and remains omitted/off. The explicit telemetry
        // policy field requires the unreleased 0.9 schema.

        // The encoded argument is script content, never interpolated shell syntax.
        var workingDirectory = input.WorkingDirectory.Replace("'", "''", StringComparison.Ordinal);

        // A drive rooted at the granted directory keeps PowerShell's path
        // normalization inside that directory. Using the host C: drive makes
        // Set-Location inspect ungranted ancestors such as C:\Users.
        var script = $"$ProgressPreference='SilentlyContinue'; try {{ New-PSDrive -Name TryRun -PSProvider FileSystem -Root '{workingDirectory}' -Scope Global -ErrorAction Stop | Out-Null; Set-Location -LiteralPath 'TryRun:\\' -ErrorAction Stop }} catch {{ Write-Error 'Restricted workspace access is unavailable on this device.'; exit 125 }}; & {{\n{input.Script}\n}}";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return new SandboxRequest(policy, $"\"{executable}\" -NoLogo -NoProfile -NonInteractive -OutputFormat Text -EncodedCommand {encoded}")
        {
            Containment = new ProcessContainerContainment
            {
                // PowerShell initializes desktop handles even for noninteractive
                // commands. See MXC docs/playground-limitations.md.
                Ui = new ProcessContainerUiPolicy { Isolation = ProcessContainerUiIsolation.Desktop },
            },
            WorkingDirectory = input.WorkingDirectory,
            InheritDefaultEnvironment = false,
            Environment = new Dictionary<string, string>
            {
                ["SystemRoot"] = windows,
                ["WINDIR"] = windows,
                ["PATH"] = powerShellDirectory + ";" + Environment.SystemDirectory,
                ["TEMP"] = input.TemporaryDirectory,
                ["TMP"] = input.TemporaryDirectory,
                ["USERPROFILE"] = profile,
                ["APPDATA"] = appData,
                ["LOCALAPPDATA"] = appData,
                ["PSModulePath"] = Path.Combine(powerShellDirectory, "Modules"),
            },
        };
    }
}
