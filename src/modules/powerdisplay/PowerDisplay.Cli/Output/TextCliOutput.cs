// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;

namespace PowerDisplay.Cli.Output;

/// <summary>
/// Human-readable text output. Success lines go to stdout; warnings and errors go
/// to stderr so scripts that capture only stdout receive a clean stream.
/// </summary>
public sealed class TextCliOutput : ICliOutput
{
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    public TextCliOutput()
        : this(Console.Out, Console.Error)
    {
    }

    public TextCliOutput(TextWriter stdout, TextWriter stderr)
    {
        _stdout = stdout;
        _stderr = stderr;
    }

    public void WriteListResult(CliListResult result)
    {
        if (result.Monitors.Count == 0)
        {
            _stdout.WriteLine("No monitors discovered.");
            return;
        }

        _stdout.WriteLine($"{"#",-3} {"Name",-22} {"Method",-7} {"Monitor ID"}");
        foreach (var m in result.Monitors)
        {
            var name = Truncate(m.Name, 22);
            _stdout.WriteLine($"{m.Number,-3} {name,-22} {m.Method,-7} {m.Id}");
        }
    }

    public void WriteSetResult(CliSetResult result)
    {
        var monitor = $"Monitor {result.Monitor.Number} ({result.Monitor.Name})";
        var before = result.BeforeDisplay is null ? "?" : result.BeforeDisplay;
        _stdout.WriteLine($"{monitor}: {result.Setting} {before} → {result.AfterDisplay}");
    }

    public void WriteGetResult(CliGetResult result)
    {
        var monitor = $"Monitor {result.Monitor.Number} ({result.Monitor.Name})";
        _stdout.WriteLine(monitor);
        foreach (var s in result.Settings)
        {
            if (!s.Supported)
            {
                _stdout.WriteLine($"  {s.Setting,-18} (not supported)");
                continue;
            }

            _stdout.WriteLine($"  {s.Setting,-18} {s.Display}");
        }
    }

    public void WriteCapabilitiesResult(CliCapabilitiesResult result)
    {
        var monitor = $"Monitor {result.Monitor.Number} ({result.Monitor.Name})";
        _stdout.WriteLine($"{monitor} via {result.CommunicationMethod}");
        if (!string.IsNullOrEmpty(result.Model))
        {
            _stdout.WriteLine($"  Model: {result.Model}");
        }

        if (!string.IsNullOrEmpty(result.MccsVersion))
        {
            _stdout.WriteLine($"  MCCS:  {result.MccsVersion}");
        }

        if (result.VcpCodes.Count == 0)
        {
            _stdout.WriteLine("  No VCP capabilities reported.");
        }
        else
        {
            _stdout.WriteLine("  VCP codes:");
            foreach (var code in result.VcpCodes)
            {
                if (code.Continuous)
                {
                    _stdout.WriteLine($"    {code.Code} {code.Name} (continuous)");
                }
                else
                {
                    var values = code.DiscreteValues is null
                        ? "(no values reported)"
                        : string.Join(", ", code.DiscreteValues);
                    _stdout.WriteLine($"    {code.Code} {code.Name}: {values}");
                }
            }
        }

        if (!string.IsNullOrEmpty(result.RawCapabilities))
        {
            _stdout.WriteLine($"  Raw: {result.RawCapabilities}");
        }
    }

    public void WriteError(CliErrorResult result)
    {
        var sb = new StringBuilder();
        sb.Append("Error: ").Append(result.Error.Message);
        _stderr.WriteLine(sb.ToString());

        if (result.Monitor is { Number: > 0 })
        {
            _stderr.WriteLine($"  monitor: Monitor {result.Monitor.Number} ({result.Monitor.Name})");
        }

        if (!string.IsNullOrEmpty(result.Error.ExpectedRange))
        {
            _stderr.WriteLine($"  expected: integer in {result.Error.ExpectedRange}");
        }

        if (result.Error.Supported is { Count: > 0 })
        {
            var sb2 = new StringBuilder("  supported: ");
            for (int i = 0; i < result.Error.Supported.Count; i++)
            {
                if (i > 0)
                {
                    sb2.Append(", ");
                }

                var v = result.Error.Supported[i];
                sb2.Append(v.Name).Append(' ').Append('(').Append(v.Vcp).Append(')');
            }

            _stderr.WriteLine(sb2.ToString());
        }

        if (!string.IsNullOrEmpty(result.Error.Hint))
        {
            _stderr.WriteLine($"  hint: {result.Error.Hint}");
        }
    }

    public void WriteWarning(string message) => _stderr.WriteLine(message);

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
        {
            return s ?? string.Empty;
        }

        return s[..(max - 1)] + "…";
    }
}
