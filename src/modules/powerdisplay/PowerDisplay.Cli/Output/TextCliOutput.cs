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

        _stdout.WriteLine("# | Name | Method | Monitor ID");
        foreach (var m in result.Monitors)
        {
            _stdout.WriteLine($"{m.Number} | {m.Name} | {m.Method} | {m.Id}");
        }
    }

    public void WriteSetResult(CliSetResult result)
    {
        var via = string.IsNullOrEmpty(result.Monitor.Method)
            ? string.Empty
            : $" [{result.Monitor.Method}]";
        var monitor = $"{MonitorLabel(result.Monitor)}{via}";
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

            _stdout.WriteLine(MonitorLabel(entry.Monitor));
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
        var monitor = MonitorLabel(result.Monitor);
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

        _stdout.WriteLine("Id | Name | Monitors | Last modified");
        foreach (var p in result.Profiles)
        {
            _stdout.WriteLine($"{p.Id} | {p.Name} | {p.MonitorCount} | {p.LastModified}");
        }
    }

    public void WriteApplyProfileResult(CliApplyProfileResult result)
    {
        // apply-profile is best-effort: print a single confirmation line that never claims hardware
        // success. Per-setting outcomes are intentionally not reported (see CliApplyProfileResult /
        // ApplyProfileForCliAsync).
        _stdout.WriteLine(Resources.Text_ProfileProcessedBestEffort(result.Profile));
    }

    public void WriteError(CliErrorResult result)
    {
        var err = result.Error;
        var (message, hint) = CliErrorLocalizer.Localize(err);

        _stderr.WriteLine($"{Resources.Label_Error}: {message}");

        if (result.Monitor is { Number: > 0 })
        {
            _stderr.WriteLine($"  {Resources.Label_Monitor}: {MonitorLabel(result.Monitor)}");
        }

        if (!string.IsNullOrEmpty(err.ExpectedRange))
        {
            _stderr.WriteLine($"  {Resources.Label_Expected}: {Resources.Text_ExpectedInteger(err.ExpectedRange)}");
        }

        if (err.Supported is { Count: > 0 })
        {
            _stderr.WriteLine($"  {Resources.Label_Supported}: " + string.Join(", ", err.Supported.Select(v => $"{v.Name} ({v.Vcp})")));
        }

        if (!string.IsNullOrEmpty(err.Detail))
        {
            _stderr.WriteLine($"  {Resources.Label_Diagnostic}: {err.Detail}");
        }

        if (!string.IsNullOrEmpty(hint))
        {
            _stderr.WriteLine($"  {Resources.Label_Hint}: {hint}");
        }
    }

    public void WriteWarning(string message)
    {
        if (!_quiet)
        {
            _stderr.WriteLine(message);
        }
    }

    private static string MonitorLabel(CliMonitorRef m) => $"Monitor {m.Number} ({m.Name})";
}
