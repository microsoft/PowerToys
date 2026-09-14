// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed record TaskEntryPoint(string RelativePath, WorkloadKind Kind, string? Interpreter = null)
{
    public string DisplayName => $"{RelativePath} · {Kind switch
    {
        WorkloadKind.WindowsApplication => "Windows application",
        WorkloadKind.WindowsPowerShell => "Windows PowerShell",
        WorkloadKind.WindowsBatch => "Windows Batch",
        WorkloadKind.LinuxShell => "Linux shell",
        WorkloadKind.LinuxPython => "Linux Python",
        _ => "Linux executable",
    }}";

    public string? WorkingSubdirectory => Path.GetDirectoryName(RelativePath) is { Length: > 0 } directory ? directory : null;
}
