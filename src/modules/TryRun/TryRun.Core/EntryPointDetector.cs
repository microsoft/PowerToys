// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Text;

namespace PowerToys.TryRun.Core;

public static class EntryPointDetector
{
    public const int HeaderBytes = 4096;

    public static TaskEntryPoint? Detect(string relativePath, ReadOnlySpan<byte> header)
    {
        relativePath = WorkspacePath.ValidateRelative(relativePath);
        header = header[..Math.Min(header.Length, HeaderBytes)];
        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        var kind = extension switch
        {
            ".ps1" => WorkloadKind.WindowsPowerShell,
            ".cmd" or ".bat" => WorkloadKind.WindowsBatch,
            ".sh" => WorkloadKind.LinuxShell,
            ".py" => WorkloadKind.LinuxPython,
            _ => (WorkloadKind?)null,
        };
        if (kind is not null)
        {
            return new TaskEntryPoint(relativePath, kind.Value, kind == WorkloadKind.LinuxPython ? "python3" : kind == WorkloadKind.LinuxShell ? ShellRuntime(header) : null);
        }

        if (extension == ".exe" && header.Length >= 64 && header[0] == 'M' && header[1] == 'Z')
        {
            var offset = BinaryPrimitives.ReadInt32LittleEndian(header[60..]);
            if (offset >= 64 && offset <= header.Length - 24 && header.Slice(offset, 4).SequenceEqual("PE\0\0"u8) && (BinaryPrimitives.ReadUInt16LittleEndian(header[(offset + 22)..]) & 0x2000) == 0)
            {
                return new TaskEntryPoint(relativePath, WorkloadKind.WindowsApplication);
            }
        }

        if (header.Length >= 18 && header[..4].SequenceEqual("\u007fELF"u8) && header[4] is 1 or 2 && header[5] is 1 or 2)
        {
            var type = header[5] == 1 ? BinaryPrimitives.ReadUInt16LittleEndian(header[16..]) : BinaryPrimitives.ReadUInt16BigEndian(header[16..]);

            // ET_DYN also covers PIE executables; the user resolves ambiguity.
            if (type is 2 or 3 && extension != ".so")
            {
                return new TaskEntryPoint(relativePath, WorkloadKind.LinuxApplication);
            }
        }

        var runtime = ShebangRuntime(header);
        return runtime switch
        {
            "sh" or "bash" or "dash" => new TaskEntryPoint(relativePath, WorkloadKind.LinuxShell, runtime),
            "python" or "python3" => new TaskEntryPoint(relativePath, WorkloadKind.LinuxPython, runtime),
            _ => null,
        };
    }

    private static string ShellRuntime(ReadOnlySpan<byte> header)
    {
        var runtime = ShebangRuntime(header);
        return runtime is "sh" or "bash" or "dash" ? runtime : "/bin/sh";
    }

    private static string? ShebangRuntime(ReadOnlySpan<byte> header)
    {
        if (!header.StartsWith("#!"u8))
        {
            return null;
        }

        var newline = header.IndexOf((byte)'\n');
        var line = Encoding.UTF8.GetString(header[..(newline < 0 ? Math.Min(header.Length, 256) : Math.Min(newline, 256))]);
        var tokens = line[2..].Split([' ', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return null;
        }

        var executable = tokens[0].Split('/')[^1];

        // Metadata is only a hint. Never interpret env options or shell syntax.
        return executable == "env" && tokens.Length == 2 ? tokens[1] : executable;
    }
}
