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
        if (input.FileRelativePath is not null)
        {
            // Reject links and invalid result trees before any copied file is
            // executed. Imported files already went through this same scanner.
            var file = Path.Combine(input.WorkingDirectory, WorkspacePath.ValidateRelative(input.FileRelativePath));
            if (!File.Exists(file) || !string.Equals(RuntimeFile.Resolve(file), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("The selected workload file is missing or redirected.");
            }
        }

        if (input.IsLinux)
        {
            return CreateLinux(input);
        }

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

        if (input.Kind == WorkloadKind.WindowsApplication)
        {
            executable = RuntimeFile.Resolve(input.ApplicationPath!);
            policy.Filesystem.ReadonlyPaths.Add(Path.GetDirectoryName(executable)!);
            var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (Directory.Exists(fonts))
            {
                policy.Filesystem.ReadonlyPaths.Add(fonts);
            }
        }

        // Telemetry is opt-in and remains omitted/off. The explicit telemetry
        // policy field requires the unreleased 0.9 schema.

        // The encoded argument is script content, never interpolated shell syntax.
        var workingDirectory = input.WorkingDirectory.Replace("'", "''", StringComparison.Ordinal);

        // A drive rooted at the granted directory keeps PowerShell's path
        // normalization inside that directory. Using the host C: drive makes
        // Set-Location inspect ungranted ancestors such as C:\Users.
        var arguments = string.Join(" ", input.Arguments.Select(argument => "'" + argument.Replace("'", "''", StringComparison.Ordinal) + "'"));
        var body = input.FileRelativePath is null ? input.Script : $"& '.\\{input.FileRelativePath.Replace("'", "''", StringComparison.Ordinal)}' {arguments}";
        var scriptArguments = input.FileRelativePath is null ? arguments : string.Empty;
        var script = $"$ProgressPreference='SilentlyContinue'; try {{ New-PSDrive -Name TryRun -PSProvider FileSystem -Root '{workingDirectory}' -Scope Global -ErrorAction Stop | Out-Null; Set-Location -LiteralPath 'TryRun:\\' -ErrorAction Stop }} catch {{ Write-Error 'Restricted workspace access is unavailable on this device.'; exit 125 }}; & {{\n{body}\n}} {scriptArguments}";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var command = $"{CommandEncoding.WindowsArgument(executable)} -NoLogo -NoProfile -NonInteractive -OutputFormat Text -EncodedCommand {encoded}";
        if (input.Kind == WorkloadKind.WindowsApplication)
        {
            command = CommandEncoding.WindowsArgument(executable) + " " + string.Join(" ", input.Arguments.Select(CommandEncoding.WindowsArgument));
        }
        else if (input.Kind == WorkloadKind.WindowsBatch)
        {
            var batchPath = input.FileRelativePath is null ? Path.Combine(input.TemporaryDirectory, "run.cmd") : Path.Combine(input.WorkingDirectory, input.FileRelativePath);
            if (input.FileRelativePath is null)
            {
                File.WriteAllText(batchPath, input.Script, new UTF8Encoding(false));
            }

            if (batchPath.Contains('%') || batchPath.Contains('!'))
            {
                throw new ArgumentException("The batch file path cannot contain expansion characters.");
            }

            var batchArguments = string.Join(" ", input.Arguments.Select(argument => "\"" + argument + "\""));
            command = $"{CommandEncoding.WindowsArgument(Path.Combine(Environment.SystemDirectory, "cmd.exe"))} /d /v:off /s /c \"\"{batchPath}\" {batchArguments}\"";
        }

        return new SandboxRequest(policy, command)
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

    private static SandboxRequest CreateLinux(ExecutionRequest input)
    {
        var work = CommandEncoding.LinuxPath(input.WorkingDirectory);
        var temporary = CommandEncoding.LinuxPath(input.TemporaryDirectory);
        var file = input.FileRelativePath is null ? temporary + (input.Kind == WorkloadKind.LinuxPython ? "/run.py" : "/run.sh") : work + "/" + input.FileRelativePath.Replace('\\', '/');
        if (input.FileRelativePath is null)
        {
            File.WriteAllText(Path.Combine(input.TemporaryDirectory, input.Kind == WorkloadKind.LinuxPython ? "run.py" : "run.sh"), input.Script.Replace("\r\n", "\n", StringComparison.Ordinal), new UTF8Encoding(false));
        }

        var interpreter = input.Kind switch
        {
            WorkloadKind.LinuxShell => CommandEncoding.ShellArgument(input.Interpreter ?? "/bin/sh") + " ",
            WorkloadKind.LinuxPython => CommandEncoding.ShellArgument(input.Interpreter ?? "python3") + " ",
            _ => string.Empty,
        };
        var arguments = string.Join(" ", input.Arguments.Select(CommandEncoding.ShellArgument));
        var executableSetup = input.Kind == WorkloadKind.LinuxApplication ? $"chmod u+x {CommandEncoding.ShellArgument(file)} && " : string.Empty;
        var command = $"cd {CommandEncoding.ShellArgument(work)} && {executableSetup}exec {interpreter}{CommandEncoding.ShellArgument(file)} {arguments}";
        var imageStore = Path.Combine(AppContext.BaseDirectory, "WslcImages");
        Directory.CreateDirectory(imageStore);
        return new SandboxRequest(
            new SandboxPolicy
            {
                Version = "0.8.0-alpha",
                Filesystem = new FilesystemPolicy { ReadwritePaths = [input.WorkingDirectory, input.TemporaryDirectory] },
                Network = new NetworkPolicy { AllowOutbound = false },

                // WSLC rejects a UI policy, even one containing false values.
                TimeoutMs = checked((uint)input.TimeoutSeconds * 1000),
            },
            command)
        {
            Experimental = true,
            Containment = new WslcContainment
            {
                Image = input.Image,
                ImageTarPath = input.ImageTarPath is null ? null : RuntimeFile.Resolve(input.ImageTarPath),
                StoragePath = imageStore,
                CpuCount = 2,
                MemoryMb = 2048,
            },
            WorkingDirectory = input.WorkingDirectory,
            InheritDefaultEnvironment = false,
            Environment = new Dictionary<string, string>
            {
                ["PATH"] = "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
                ["HOME"] = "/tmp",
                ["TMPDIR"] = temporary,
                ["LANG"] = "C.UTF-8",
            },
        };
    }
}
