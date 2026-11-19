// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace PowerScripts.UI.ViewModels;

public sealed class PowerScriptActivityItem : Observable
{
    private const string MetaSeparator = " \u00B7 ";

    private static readonly CompositeFormat DurationFormat = CompositeFormat.Parse(PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityDurationValue"));
    private static readonly CompositeFormat ExitFormat = CompositeFormat.Parse(PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityExit"));

    public static IReadOnlyList<string> KnownSources { get; } = Array.AsReadOnly(new[]
    {
        "advanced-paste",
        "keyboard-manager",
        "command-palette",
        "file-explorer",
        "command-line",
        "powerscripts-editor",
    });

    public required Guid Id { get; set; }

    public required string ScriptId { get; set; }

    public required string ScriptName { get; set; }

    public required string ConsumerId { get; set; }

    public required DateTimeOffset StartedUtc { get; set; }

    public required string Status { get; set; }

    public int? ExitCode { get; set; }

    public double? DurationMilliseconds { get; set; }

    public bool IsRunning => Status == "running";

    public string Source => GetSourceName(ConsumerId);

    public string SourceIconPath => "ms-appx:///Assets/Settings/Icons/" + ConsumerId switch
    {
        "advanced-paste" => "AdvancedPaste.png",
        "keyboard-manager" => "KeyboardManager.png",
        "command-palette" => "CmdPal.png",
        "file-explorer" => "FileExplorerPreview.png",
        _ => "PowerToys.png",
    };

    public string StatusText => PowerScriptsViewModel.GetResourceString(Status switch
    {
        "running" => "PowerScripts_ActivityRunning",
        "succeeded" => "PowerScripts_ActivitySucceeded",
        "failed" => "PowerScripts_ActivityFailed",
        "interrupted" => "PowerScripts_ActivityInterrupted",
        _ => "PowerScripts_ActivityUnknown",
    });

    public string Meta
    {
        get
        {
            var when = IsRunning
                ? PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityNow")
                : StartedUtc.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
            var meta = Source + MetaSeparator + when;
            return !IsRunning && Status != "interrupted" && DurationMilliseconds is double milliseconds
                ? meta + MetaSeparator + string.Format(CultureInfo.CurrentCulture, DurationFormat, milliseconds / 1000)
                : meta;
        }
    }

    public bool HasExitCode => ExitCode is int exitCode && exitCode != 0;

    public string ExitText => HasExitCode
        ? string.Format(CultureInfo.CurrentCulture, ExitFormat, ExitCode)
        : string.Empty;

    public static string GetSourceName(string consumerId) => consumerId switch
    {
        "advanced-paste" => PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityAdvancedPaste"),
        "keyboard-manager" => PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityKeyboardManager"),
        "command-palette" => PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityCommandPalette"),
        "file-explorer" => PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityFileExplorer"),
        "command-line" => PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityCommandLine"),
        "powerscripts-editor" => PowerScriptsViewModel.GetResourceString("PowerScripts_ActivityEditor"),
        _ => consumerId,
    };

    // ListView uses this as the row's accessible name.
    public override string ToString() => HasExitCode
        ? string.Join(", ", ScriptName, StatusText, ExitText, Meta)
        : string.Join(", ", ScriptName, StatusText, Meta);

    public void UpdateStatus(PowerScriptActivityItem snapshot)
    {
        Status = snapshot.Status;
        ExitCode = snapshot.ExitCode;
        DurationMilliseconds = snapshot.DurationMilliseconds;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(Meta));
        OnPropertyChanged(nameof(HasExitCode));
        OnPropertyChanged(nameof(ExitText));
    }
}
