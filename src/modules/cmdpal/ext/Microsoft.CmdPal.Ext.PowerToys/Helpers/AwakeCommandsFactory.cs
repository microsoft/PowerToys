// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.ModuleContracts;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Helpers;

internal static class AwakeCommandsFactory
{
    private static readonly IReadOnlyList<AwakeCommandDefinition> PresetCommands =
    [
        new(
            Title: "Start Awake (display on)",
            Subtitle: "Keep the PC and display awake until Command Palette closes",
            Action: () => InvokeAwakeCliAsync("--use-parent-pid --display-on true"),
            Toast: "Awake running with display on"),
        new(
            Title: "Start Awake (allow display sleep)",
            Subtitle: "Keep the PC awake but let the display sleep",
            Action: () => InvokeAwakeCliAsync("--use-parent-pid --display-on false"),
            Toast: "Awake running with display allowed to sleep"),
        new(
            Title: "Start Awake indefinitely (display on)",
            Subtitle: "Keep the PC and display awake until you stop Awake",
            Action: () => InvokeAwakeCliAsync("--display-on true"),
            Toast: "Awake running indefinitely with display on"),
        new(
            Title: "Start Awake indefinitely (allow display sleep)",
            Subtitle: "Keep the PC awake indefinitely but let the display sleep",
            Action: () => InvokeAwakeCliAsync("--display-on false"),
            Toast: "Awake running indefinitely with display allowed to sleep"),
        new(
            Title: "Start Awake for 30 minutes",
            Subtitle: "Keeps the PC awake for 30 minutes",
            Action: () => InvokeAwakeCliAsync("--use-parent-pid --display-on true --time-limit 1800"),
            Toast: "Awake timer set for 30 minutes"),
        new(
            Title: "Start Awake for 2 hours",
            Subtitle: "Keeps the PC awake for two hours",
            Action: () => InvokeAwakeCliAsync("--use-parent-pid --display-on true --time-limit 7200"),
            Toast: "Awake timer set for 2 hours"),
    ];

    private static readonly IconInfo AwakeIcon = PowerToysResourcesHelper.IconFromSettingsIcon("Awake.png");

    internal static void PopulateModuleCommands(List<ICommandContextItem> moreCommands)
    {
        ArgumentNullException.ThrowIfNull(moreCommands);

        var stopCommand = new StopAwakeCommand();
        var stopContext = new CommandContextItem(stopCommand)
        {
            Title = "Set Awake to Off",
            Subtitle = "Switch Awake to passive mode",
        };
        moreCommands.Add(stopContext);
    }

    internal static IListItem[] GetSessionItems(string searchText)
    {
        var results = new List<IListItem>();

        var statusSubtitle = AwakeStatusService.GetStatusSubtitle();
        if (Matches("Awake: Current status", statusSubtitle, searchText))
        {
            ListItem? statusItem = null;
            var refreshCommand = new RefreshAwakeStatusCommand(subtitle =>
            {
                if (statusItem is not null)
                {
                    statusItem.Subtitle = subtitle;
                }
            });

            var statusNoOp = new NoOpCommand();
            statusNoOp.Name = "Awake status";

            statusItem = new ListItem(new CommandItem(statusNoOp))
            {
                Title = "Awake: Current status",
                Subtitle = statusSubtitle,
                Icon = AwakeIcon,
                MoreCommands =
                [
                    new CommandContextItem(refreshCommand)
                    {
                        Title = "Refresh status",
                        Subtitle = "Re-read current Awake state",
                    },
                ],
            };

            results.Add(statusItem);
        }

        foreach (var preset in PresetCommands)
        {
            if (!Matches(preset.Title, preset.Subtitle, searchText))
            {
                continue;
            }

            var command = new StartAwakeCommand(preset.Title, preset.Action, preset.Toast);
            var item = new ListItem(new CommandItem(command))
            {
                Title = preset.Title,
                Subtitle = preset.Subtitle,
                Icon = AwakeIcon,
            };
            results.Add(item);
        }

        foreach (var preset in AwakeStatusService.ReadCustomPresets())
        {
            var title = $"Start Awake for {FormatDuration(preset.Duration)}";
            var subtitle = $"Custom preset '{preset.Name}'";

            if (!Matches(title, subtitle, searchText))
            {
                continue;
            }

            var seconds = (int)Math.Round(preset.Duration.TotalSeconds);
            var command = new StartAwakeCommand(
                title,
                () => InvokeAwakeCliAsync($"--use-parent-pid --display-on true --time-limit {seconds}"),
                $"Awake timer set for {FormatDuration(preset.Duration)}");

            var item = new ListItem(new CommandItem(command))
            {
                Title = title,
                Subtitle = subtitle,
                Icon = AwakeIcon,
            };
            results.Add(item);
        }

        if (Matches("Bind Awake to another process", "Keep awake while a process is running", searchText))
        {
            var processPageItem = new CommandItem(new Pages.AwakeProcessListPage())
            {
                Title = "Bind Awake to another process",
                Subtitle = "Stop automatically when the target process exits",
                Icon = AwakeIcon,
            };

            results.Add(new ListItem(processPageItem)
            {
                Title = processPageItem.Title,
                Subtitle = processPageItem.Subtitle,
                Icon = processPageItem.Icon,
            });
        }

        if (Matches("Set Awake to Off", "Switch Awake to passive mode", searchText))
        {
            var stopCommand = new StopAwakeCommand();
            var stopItem = new ListItem(new CommandItem(stopCommand))
            {
                Title = "Set Awake to Off",
                Subtitle = "Switch Awake to passive mode",
                Icon = AwakeIcon,
            };
            results.Add(stopItem);
        }

        if (Matches("Open Awake settings", "Configure Awake in PowerToys", searchText))
        {
            var settingsCommand = new OpenPowerToysSettingsCommand("Awake", "Awake");
            var settingsItem = new ListItem(new CommandItem(settingsCommand))
            {
                Title = "Open Awake settings",
                Subtitle = "Configure Awake inside PowerToys",
                Icon = AwakeIcon,
            };
            results.Add(settingsItem);
        }

        return results.ToArray();
    }

    internal static IListItem[] GetProcessItems(string searchText)
    {
        var results = new List<IListItem>();

        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            return Array.Empty<IListItem>();
        }

        foreach (var process in processes.OrderBy(p => p.ProcessName, StringComparer.CurrentCultureIgnoreCase).Take(200))
        {
            try
            {
                var name = process.ProcessName;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var title = $"{name} ({process.Id})";
                if (!Matches(title, string.Empty, searchText))
                {
                    continue;
                }

                var command = new StartAwakeCommand(
                    $"Bind Awake to {title}",
                    () => InvokeAwakeCliAsync($"--pid {process.Id} --display-on true"),
                    $"Awake bound to PID {process.Id}");

                var item = new ListItem(new CommandItem(command))
                {
                    Title = title,
                    Subtitle = "Keep the PC awake while this process is running",
                    Icon = AwakeIcon,
                };
                results.Add(item);
            }
            catch
            {
                // Ignore processes that exit or cannot be inspected
            }
            finally
            {
                process.Dispose();
            }
        }

        return results.ToArray();
    }

    internal static bool Matches(string source, string subtitle, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return Contains(source, searchText) || Contains(subtitle, searchText);
    }

    private static bool Contains(string source, string searchText)
    {
        return !string.IsNullOrEmpty(source) && source.Contains(searchText, StringComparison.CurrentCultureIgnoreCase);
    }

    private static string FormatDuration(TimeSpan span)
    {
        if (span.TotalHours >= 1)
        {
            var hours = (int)Math.Floor(span.TotalHours);
            var minutes = span.Minutes;
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        }

        if (span.TotalMinutes >= 1)
        {
            return $"{(int)Math.Round(span.TotalMinutes)}m";
        }

        return span.TotalSeconds >= 1 ? $"{(int)Math.Round(span.TotalSeconds)}s" : "\u2014";
    }

    private static Task<OperationResult> InvokeAwakeCliAsync(string arguments)
    {
        try
        {
            var basePath = PowerToysPathResolver.GetPowerToysInstallPath();
            if (string.IsNullOrWhiteSpace(basePath))
            {
                return Task.FromResult(OperationResult.Fail("PowerToys install path not found."));
            }

            var exePath = Path.Combine(basePath, "PowerToys.Awake.exe");
            if (!File.Exists(exePath))
            {
                return Task.FromResult(OperationResult.Fail("Unable to locate PowerToys.Awake.exe."));
            }

            var startInfo = new ProcessStartInfo(exePath, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            Process.Start(startInfo);
            return Task.FromResult(OperationResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Fail($"Failed to invoke Awake: {ex.Message}"));
        }
    }

    private sealed record AwakeCommandDefinition(string Title, string Subtitle, Func<Task<OperationResult>> Action, string Toast);
}
