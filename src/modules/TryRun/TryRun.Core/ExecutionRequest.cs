// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerToys.TryRun.Core;

public sealed record ExecutionRequest(string Script, string WorkingDirectory, string TemporaryDirectory, int TimeoutSeconds)
{
    public const int MaximumScriptLength = 8192;

    public WorkloadKind Kind { get; init; }

    public string? ApplicationPath { get; init; }

    public string? FileRelativePath { get; init; }

    public string[] Arguments { get; init; } = [];

    public string Image { get; init; } = "alpine:3.22";

    public string? ImageTarPath { get; init; }

    public string? Interpreter { get; init; }

    public bool PrepareImage { get; init; }

    [JsonIgnore]
    public bool IsLinux => Kind is WorkloadKind.LinuxShell or WorkloadKind.LinuxPython or WorkloadKind.LinuxApplication;

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Script);
        ArgumentNullException.ThrowIfNull(Arguments);
        if (!Enum.IsDefined(Kind) || Arguments.Length > 64 || Arguments.Any(argument => argument is null || argument.Length > 4096 || argument.Contains('\0')) || Arguments.Sum(argument => argument.Length) > 8192)
        {
            throw new ArgumentException("Choose a supported workload and at most 64 arguments (8,192 characters total).");
        }

        if (Script.Length > MaximumScriptLength || Script.Contains('\0'))
        {
            throw new ArgumentException("The script must be at most 8192 characters and cannot contain null characters.");
        }

        if (TimeoutSeconds is < 1 or > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(TimeoutSeconds), "Choose a timeout between 1 and 300 seconds.");
        }

        ValidateDirectory(WorkingDirectory);
        ValidateDirectory(TemporaryDirectory);
        if (Interpreter is not null && (!IsLinux || Kind == WorkloadKind.LinuxApplication || string.IsNullOrWhiteSpace(Interpreter) || Interpreter.Length > 256 || Interpreter.Contains('\0')))
        {
            throw new ArgumentException("A Linux script runtime must be a single executable name or path inside the image.");
        }

        if (PrepareImage && !IsLinux)
        {
            throw new ArgumentException("Only Linux profiles use image preparation.");
        }

        if (Kind == WorkloadKind.WindowsApplication)
        {
            WorkspacePath.LocalPath(ApplicationPath!);
            if (!string.Equals(Path.GetExtension(ApplicationPath), ".exe", StringComparison.OrdinalIgnoreCase) || FileRelativePath is not null)
            {
                throw new ArgumentException("Choose a Windows .exe application.");
            }
        }
        else if (ApplicationPath is not null)
        {
            throw new ArgumentException("Host applications can only be used in the Windows application profile.");
        }

        if (FileRelativePath is not null)
        {
            WorkspacePath.ValidateRelative(FileRelativePath);
            var extension = Path.GetExtension(FileRelativePath);
            if ((Kind == WorkloadKind.WindowsPowerShell && !extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase)) ||
                (Kind == WorkloadKind.WindowsBatch && !extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Choose a .ps1 file for PowerShell or a .cmd/.bat file for Batch. Other runtimes can be selected as a Windows application.");
            }
        }
        else if (Kind == WorkloadKind.LinuxApplication || (Kind != WorkloadKind.WindowsApplication && string.IsNullOrWhiteSpace(Script)))
        {
            throw new ArgumentException("Enter a script or choose a file to run.");
        }

        if (Kind == WorkloadKind.WindowsBatch && (Arguments.Any(argument => argument.IndexOfAny(['%', '!', '^', '&', '|', '<', '>', '"', '\r', '\n']) >= 0) || FileRelativePath?.IndexOfAny(['%', '!', '"']) >= 0))
        {
            throw new ArgumentException("Batch file arguments cannot contain command expansion characters. Put complex commands in the batch script instead.");
        }

        if (IsLinux && (string.IsNullOrWhiteSpace(Image) || Image.Length > 256 || Image.Any(character => !char.IsAsciiLetterOrDigit(character) && !"-._/:@".Contains(character))))
        {
            throw new ArgumentException("Enter a valid cached Linux image reference.");
        }

        if (ImageTarPath is not null)
        {
            WorkspacePath.LocalPath(ImageTarPath);
            if (!IsLinux)
            {
                throw new ArgumentException("Image archives are only used by Linux profiles.");
            }
        }
    }

    private static void ValidateDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The working and temporary directories must be absolute local drive paths.");
        }
    }
}
