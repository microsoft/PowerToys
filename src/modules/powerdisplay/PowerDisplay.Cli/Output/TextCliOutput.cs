// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using PowerDisplay.Cli.Properties;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.Output;

/// <summary>
/// Human-readable text output. Success lines go to stdout; warnings and errors go
/// to stderr so scripts that capture only stdout receive a clean stream.
/// </summary>
public sealed class TextCliOutput : ICliOutput
{
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;
    private readonly bool _quiet;

    public TextCliOutput(bool quiet = false)
        : this(Console.Out, Console.Error, quiet)
    {
    }

    public TextCliOutput(TextWriter stdout, TextWriter stderr, bool quiet = false)
    {
        _stdout = stdout;
        _stderr = stderr;
        _quiet = quiet;
    }

    public void WriteListResult(CliListResult result)
    {
        if (result.Monitors.Count == 0)
        {
            _stdout.WriteLine(Resources.Text_NoMonitorsDiscovered);
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
        var via = string.IsNullOrEmpty(result.Monitor.Method)
            ? string.Empty
            : $" [{result.Monitor.Method}]";
        var monitor = $"Monitor {result.Monitor.Number} ({result.Monitor.Name}){via}";
        var before = result.BeforeDisplay ?? "?";
        _stdout.WriteLine($"{monitor}: {result.Setting} {before} → {result.AfterDisplay}");
    }

    public void WriteGetResult(CliGetResult result)
    {
        if (result.Monitors.Count == 0)
        {
            _stdout.WriteLine(Resources.Text_NoMonitorsDiscovered);
            return;
        }

        for (int i = 0; i < result.Monitors.Count; i++)
        {
            var entry = result.Monitors[i];
            if (i > 0)
            {
                _stdout.WriteLine();
            }

            _stdout.WriteLine($"Monitor {entry.Monitor.Number} ({entry.Monitor.Name})");
            _stdout.WriteLine($"  protocol           {entry.Monitor.Method}");
            _stdout.WriteLine($"  id                 {entry.Monitor.Id}");
            foreach (var s in entry.Settings)
            {
                // Three honest states: the monitor can't do it, it can but discovery couldn't read
                // it, or here's the value.
                var rendered = !s.Supported ? Resources.Text_NotSupported
                    : s.Display ?? Resources.Text_Unknown;
                _stdout.WriteLine($"  {s.Setting,-18} {rendered}");
            }
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
            _stdout.WriteLine($"  {Resources.Text_NoVcpCapabilities}");
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
                        ? Resources.Text_NoValuesReported
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

    public void WriteProfileListResult(CliProfileListResult result)
    {
        if (result.Profiles.Count == 0)
        {
            _stdout.WriteLine(Resources.Text_NoProfilesSaved);
            return;
        }

        _stdout.WriteLine($"{"Name",-24} {"Monitors",-9} {"Last modified"}");
        foreach (var p in result.Profiles)
        {
            var name = Truncate(p.Name, 24);
            _stdout.WriteLine($"{name,-24} {p.MonitorCount,-9} {p.LastModified}");
        }
    }

    public void WriteApplyProfileResult(CliApplyProfileResult result)
    {
        _stdout.WriteLine(Resources.Text_AppliedProfile(result.Profile));
        foreach (var m in result.Monitors)
        {
            if (!m.Connected)
            {
                _stdout.WriteLine($"  Monitor {m.Monitor.Id}: {Resources.Text_NotConnectedSkipped}");
                continue;
            }

            var label = $"Monitor {m.Monitor.Number} ({m.Monitor.Name})";
            if (m.Changes.Count == 0)
            {
                _stdout.WriteLine($"  {label}: {Resources.Text_NoSettingsInProfile}");
                continue;
            }

            foreach (var c in m.Changes)
            {
                var detail = c.Status switch
                {
                    CliProfileChange.StatusApplied => $"{c.Setting} → {c.Display}",
                    CliProfileChange.StatusUnsupported => $"{c.Setting} {Resources.Text_NotSupported}",
                    CliProfileChange.StatusOutOfRange => $"{c.Setting} {c.Value} {Resources.Text_OutOfRangeSkipped}",
                    CliProfileChange.StatusHardwareFailure => $"{c.Setting} → {c.Value} {Resources.Text_Failed} ({c.Error})",
                    _ => $"{c.Setting}: {c.Status}",
                };
                _stdout.WriteLine($"  {label}: {detail}");
            }
        }
    }

    public void WriteError(CliErrorResult result)
    {
        _stderr.WriteLine($"Error: {result.Error.Message}");

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
            _stderr.WriteLine("  supported: " + string.Join(", ", result.Error.Supported.Select(v => $"{v.Name} ({v.Vcp})")));
        }

        if (!string.IsNullOrEmpty(result.Error.Hint))
        {
            _stderr.WriteLine($"  hint: {result.Error.Hint}");
        }
    }

    public void WriteWarning(string message)
    {
        if (!_quiet)
        {
            _stderr.WriteLine(message);
        }
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
        {
            return s ?? string.Empty;
        }

        return s[..(max - 1)] + "…";
    }
}
