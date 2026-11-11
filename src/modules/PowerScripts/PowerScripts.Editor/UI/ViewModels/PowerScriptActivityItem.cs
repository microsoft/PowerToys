// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace PowerScripts.UI.ViewModels;

public sealed class PowerScriptActivityItem : Observable
{
    private static readonly CompositeFormat DetailsFormat = CompositeFormat.Parse(PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityDetails"));
    private static readonly CompositeFormat ExitCodeFormat = CompositeFormat.Parse(PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityExitCode"));
    private static readonly CompositeFormat DurationFormat = CompositeFormat.Parse(PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityDuration"));

    public required Guid Id { get; set; }

    public required string ScriptId { get; set; }

    public required string ScriptName { get; set; }

    public required string ConsumerId { get; set; }

    public required DateTimeOffset StartedUtc { get; set; }

    public required string Status { get; set; }

    public int? ExitCode { get; set; }

    public double? DurationMilliseconds { get; set; }

    public string Details => string.Format(
        CultureInfo.CurrentCulture,
        DetailsFormat,
        Source,
        StartedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
        ScriptId);

    public string Source => ConsumerId switch
    {
        "advanced-paste" => PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityAdvancedPaste"),
        "keyboard-manager" => PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityKeyboardManager"),
        "command-palette" => PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityCommandPalette"),
        "file-explorer" => PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityFileExplorer"),
        "command-line" => PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityCommandLine"),
        "powerscripts-editor" => PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityEditor"),
        _ => ConsumerId,
    };

    public string Outcome
    {
        get
        {
            var status = PowerScriptsViewModel.GetResourceString(Status switch
            {
                "running" => "PowerScripts_ActivityRunning",
                "succeeded" => "PowerScripts_ActivitySucceeded",
                "failed" => "PowerScripts_ActivityFailed",
                "interrupted" => "PowerScripts_ActivityInterrupted",
                _ => "PowerScripts_ActivityUnknown",
            });
            if (ExitCode is int exitCode && exitCode != 0)
            {
                status = string.Format(CultureInfo.CurrentCulture, ExitCodeFormat, status, exitCode);
            }

            return DurationMilliseconds is double milliseconds
                ? string.Format(CultureInfo.CurrentCulture, DurationFormat, status, milliseconds / 1000)
                : status;
        }
    }

    public void UpdateStatus(PowerScriptActivityItem snapshot)
    {
        Status = snapshot.Status;
        ExitCode = snapshot.ExitCode;
        DurationMilliseconds = snapshot.DurationMilliseconds;
        OnPropertyChanged(nameof(Outcome));
    }
}
