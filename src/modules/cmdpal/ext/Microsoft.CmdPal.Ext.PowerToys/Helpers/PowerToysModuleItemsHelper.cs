// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Pages;
using NoOpCommand = PowerToysExtension.Commands.NoOpCommand;

namespace Microsoft.CmdPal.Ext.PowerToys.Helpers;

internal static class PowerToysModuleItemsHelper
{
    private sealed record ModuleMetadata(
        string SettingsKey,
        string Title,
        string IconPath,
        string[] SearchTerms,
        string? Subtitle = null,
        string? LaunchEvent = null,
        string? LaunchExecutable = null,
        string? LaunchArguments = null,
        ModuleCommandMetadata[]? AdditionalLaunchCommands = null);

    private sealed record ModuleCommandMetadata(
        string Title,
        string? Subtitle = null,
        string? LaunchEvent = null,
        string? LaunchExecutable = null,
        string? LaunchArguments = null,
        string[]? SearchTerms = null);

    private sealed record CustomEntry(Func<ListItem> Factory, string[] SearchTerms)
    {
        public ListItem CreateItem() => Factory();
    }

    private static readonly ModuleMetadata[] Modules =
    [
        new(
            SettingsKey: "AdvancedPaste",
            Title: "Advanced Paste",
            IconPath: "Assets\\AdvancedPaste.png",
            SearchTerms: new[] { "advanced", "paste", "clipboard", "text" },
            Subtitle: "Open Advanced Paste settings"),
        new(
            SettingsKey: "AlwaysOnTop",
            Title: "Always On Top",
            IconPath: "Assets\\AlwaysOnTop.png",
            SearchTerms: new[] { "always", "top", "pin", "window" },
            Subtitle: "Toggle Always On Top for the active window"),
        new(
            SettingsKey: "Awake",
            Title: "Awake",
            IconPath: "Assets\\Awake.png",
            SearchTerms: new[] { "awake", "sleep", "keep", "monitor" },
            Subtitle: "Keep your PC awake",
            LaunchExecutable: "PowerToys.Awake.exe",
            LaunchArguments: "--use-parent-pid --display-on true"),
        new(
            SettingsKey: "ColorPicker",
            Title: "Color Picker",
            IconPath: "Assets\\ColorPicker.png",
            SearchTerms: new[] { "color", "picker", "eyedropper", "pick" },
            Subtitle: "Copy colors from anywhere",
            LaunchEvent: PowerToysEventNames.ColorPickerShow,
            LaunchExecutable: "PowerToys.ColorPickerUI.exe"),
        new(
            SettingsKey: "CropAndLock",
            Title: "Crop And Lock",
            IconPath: "Assets\\CropAndLock.png",
            SearchTerms: new[] { "crop", "lock", "thumbnail", "reparent" },
            Subtitle: "Configure Crop And Lock",
            AdditionalLaunchCommands: new[]
            {
                new ModuleCommandMetadata(
                    Title: "Crop active window (thumbnail)",
                    Subtitle: "Creates a static snapshot window",
                    LaunchEvent: PowerToysEventNames.CropAndLockThumbnail,
                    SearchTerms: new[] { "thumbnail", "snapshot", "crop" }),
                new ModuleCommandMetadata(
                    Title: "Crop active window (reparent)",
                    Subtitle: "Embeds the active window inside a new host",
                    LaunchEvent: PowerToysEventNames.CropAndLockReparent,
                    SearchTerms: new[] { "reparent", "embed", "live window" }),
            }),
        new(
            SettingsKey: "EnvironmentVariables",
            Title: "Environment Variables",
            IconPath: "Assets\\EnvironmentVariables.png",
            SearchTerms: new[] { "environment", "variables", "env", "path" },
            Subtitle: "Manage environment variables",
            LaunchEvent: PowerToysEventNames.EnvironmentVariablesShow,
            AdditionalLaunchCommands: new[]
            {
                new ModuleCommandMetadata(
                    Title: "Open as administrator",
                    Subtitle: "Launches the elevated editor",
                    LaunchEvent: PowerToysEventNames.EnvironmentVariablesShowAdmin,
                    SearchTerms: new[] { "admin", "administrator" }),
            }),
        new(
            SettingsKey: "FancyZones",
            Title: "FancyZones",
            IconPath: "Assets\\FancyZones.png",
            SearchTerms: new[] { "fancyzones", "zones", "layout", "window" },
            Subtitle: "Adjust FancyZones layouts",
            LaunchEvent: PowerToysEventNames.FancyZonesToggleEditor,
            LaunchExecutable: "PowerToys.FancyZonesEditor.exe"),
        new(
            SettingsKey: "FileExplorer",
            Title: "File Explorer Add-ons",
            IconPath: "Assets\\FileExplorerPreview.png",
            SearchTerms: new[] { "file explorer", "preview", "addons", "powerpreview" },
            Subtitle: "Configure File Explorer add-ons"),
        new(
            SettingsKey: "FileLocksmith",
            Title: "File Locksmith",
            IconPath: "Assets\\FileLocksmith.png",
            SearchTerms: new[] { "file", "locksmith", "lock", "unlock" },
            Subtitle: "Find which process locks a file"),
        new(
            SettingsKey: "Hosts",
            Title: "Hosts File Editor",
            IconPath: "Assets\\Hosts.png",
            SearchTerms: new[] { "hosts", "file", "editor" },
            Subtitle: "Edit the hosts file",
            LaunchEvent: PowerToysEventNames.HostsShow,
            AdditionalLaunchCommands: new[]
            {
                new ModuleCommandMetadata(
                    Title: "Open as administrator",
                    Subtitle: "Launches the elevated editor",
                    LaunchEvent: PowerToysEventNames.HostsShowAdmin,
                    SearchTerms: new[] { "admin", "administrator" }),
            }),
        new(
            SettingsKey: "ImageResizer",
            Title: "Image Resizer",
            IconPath: "Assets\\ImageResizer.png",
            SearchTerms: new[] { "image", "resize", "resizer", "photo" },
            Subtitle: "Resize images in bulk"),
        new(
            SettingsKey: "KBM",
            Title: "Keyboard Manager",
            IconPath: "Assets\\KeyboardManager.png",
            SearchTerms: new[] { "keyboard", "manager", "remap", "kbm", "shortcut" },
            Subtitle: "Remap keys and shortcuts"),
        new(
            SettingsKey: "MeasureTool",
            Title: "Screen Ruler",
            IconPath: "Assets\\ScreenRuler.png",
            SearchTerms: new[] { "screen", "ruler", "measure", "measuretool" },
            Subtitle: "Measure on-screen elements",
            LaunchEvent: PowerToysEventNames.MeasureToolTrigger),
        new(
            SettingsKey: "MouseUtils",
            Title: "Mouse Utilities",
            IconPath: "Assets\\MouseUtils.png",
            SearchTerms: new[] { "mouse", "utilities", "finder", "highlighter", "crosshairs" },
            Subtitle: "Configure mouse utilities"),
        new(
            SettingsKey: "MouseWithoutBorders",
            Title: "Mouse Without Borders",
            IconPath: "Assets\\MouseWithoutBorders.png",
            SearchTerms: new[] { "mouse", "borders", "multi pc", "mwob" },
            Subtitle: "Share mouse and keyboard across PCs"),
        new(
            SettingsKey: "NewPlus",
            Title: "New+",
            IconPath: "Assets\\NewPlus.png",
            SearchTerms: new[] { "new", "template", "file" },
            Subtitle: "Create templates quickly"),
        new(
            SettingsKey: "Peek",
            Title: "Peek",
            IconPath: "Assets\\Peek.png",
            SearchTerms: new[] { "peek", "preview", "quick look" },
            Subtitle: "Preview files instantly",
            LaunchEvent: PowerToysEventNames.PeekShow),
        new(
            SettingsKey: "PowerAccent",
            Title: "Quick Accent",
            IconPath: "Assets\\QuickAccent.png",
            SearchTerms: new[] { "accent", "quick", "characters", "diacritics", "poweraccent" },
            Subtitle: "Insert accented characters"),
        new(
            SettingsKey: "PowerOCR",
            Title: "Text Extractor",
            IconPath: "Assets\\TextExtractor.png",
            SearchTerms: new[] { "text", "extractor", "ocr", "copy" },
            Subtitle: "Extract text from the screen",
            LaunchEvent: PowerToysEventNames.PowerOcrShow,
            LaunchExecutable: "PowerToys.PowerOCR.exe"),
        new(
            SettingsKey: "PowerRename",
            Title: "PowerRename",
            IconPath: "Assets\\PowerRename.png",
            SearchTerms: new[] { "rename", "files", "powerrename" },
            Subtitle: "Batch rename files",
            LaunchExecutable: "PowerRename.exe"),
        new(
            SettingsKey: "RegistryPreview",
            Title: "Registry Preview",
            IconPath: "Assets\\RegistryPreview.png",
            SearchTerms: new[] { "registry", "preview", "reg" },
            Subtitle: "Inspect and edit registry files",
            LaunchEvent: PowerToysEventNames.RegistryPreviewTrigger),
        new(
            SettingsKey: "Run",
            Title: "PowerToys Run",
            IconPath: "Assets\\PowerToysRun.png",
            SearchTerms: new[] { "run", "launcher", "search", "powertoys run", "powerlauncher" },
            Subtitle: "Quickly search and launch",
            LaunchEvent: PowerToysEventNames.PowerToysRunInvoke),
        new(
            SettingsKey: "ShortcutGuide",
            Title: "Shortcut Guide",
            IconPath: "Assets\\ShortcutGuide.png",
            SearchTerms: new[] { "shortcut", "guide", "keys", "help" },
            Subtitle: "View available shortcuts",
            LaunchEvent: PowerToysEventNames.ShortcutGuideTrigger),
        new(
            SettingsKey: "CmdPal",
            Title: "Command Palette",
            IconPath: "Assets\\CmdPal.png",
            SearchTerms: new[] { "command", "palette", "cmdpal", "prompt" },
            Subtitle: "Open the Command Palette",
            LaunchEvent: PowerToysEventNames.CommandPaletteShow),
        new(
            SettingsKey: "CmdNotFound",
            Title: "Command Not Found",
            IconPath: "Assets\\CommandNotFound.png",
            SearchTerms: new[] { "command", "not", "found", "terminal" },
            Subtitle: "Suggest commands when mistyped"),
        new(
            SettingsKey: "Workspaces",
            Title: "Workspaces",
            IconPath: "Assets\\Workspaces.png",
            SearchTerms: new[] { "workspace", "layouts", "projects" },
            Subtitle: "Manage PowerToys workspaces",
            LaunchEvent: PowerToysEventNames.WorkspacesLaunchEditor,
            AdditionalLaunchCommands: new[]
            {
                new ModuleCommandMetadata(
                    Title: "Trigger Workspaces hotkey",
                    Subtitle: "Invokes the configured Workspaces action",
                    LaunchEvent: PowerToysEventNames.WorkspacesHotkey,
                    SearchTerms: new[] { "hotkey", "shortcut" }),
            }),
        new(
            SettingsKey: "ZoomIt",
            Title: "ZoomIt",
            IconPath: "Assets\\ZoomIt.png",
            SearchTerms: new[] { "zoom", "zoomit", "presentation" },
            Subtitle: "Configure ZoomIt"),
        new(
            SettingsKey: "Overview",
            Title: "General",
            IconPath: "Assets\\PowerToys.png",
            SearchTerms: new[] { "general", "overview", "about" },
            Subtitle: "General PowerToys settings"),
        new(
            SettingsKey: "Dashboard",
            Title: "Dashboard",
            IconPath: "Assets\\PowerToys.png",
            SearchTerms: new[] { "dashboard", "status", "summary" },
            Subtitle: "View overall status"),
    ];

    private static readonly CustomEntry[] CustomEntries =
    [
        new(
            () => new ListItem(new CommandItem(new WorkspacesListPage()))
            {
                Title = "Workspaces list",
                Subtitle = "Browse individual workspaces",
                Icon = IconHelpers.FromRelativePath("Assets\\Workspaces.png"),
            },
            new[] { "workspace", "workspaces", "layout" }),
        new(
            () => new ListItem(new CommandItem(new AwakeSessionsPage()))
            {
                Title = "Awake actions",
                Subtitle = "Start, stop, or schedule Awake",
                Icon = IconHelpers.FromRelativePath("Assets\\Awake.png"),
            },
            new[] { "awake", "keep awake", "sleep", "prevent", "timer" }),
    ];

    internal static IListItem[] GetModuleItems(string? searchText)
    {
        var results = new List<IListItem>();

        foreach (var module in Modules)
        {
            if (Matches(module, searchText))
            {
                results.Add(CreateModuleItem(module));
            }
        }

        foreach (var entry in CustomEntries)
        {
            var item = entry.CreateItem();
            if (Matches(item, entry.SearchTerms, searchText))
            {
                results.Add(item);
            }
        }

        var workspaceItems = WorkspaceItemsHelper.GetWorkspaceItems(searchText);
        if (workspaceItems.Length > 0)
        {
            results.AddRange(workspaceItems);
        }

        return results.ToArray();
    }

    private static ListItem CreateModuleItem(ModuleMetadata metadata)
    {
        var mainCommand = CreatePrimaryCommand(metadata, out var usesSettingsForPrimary);

        var listItem = new ListItem(mainCommand)
        {
            Title = metadata.Title,
            Subtitle = metadata.Subtitle ?? (usesSettingsForPrimary ? "Open module settings" : $"Launch {metadata.Title}"),
            Icon = IconHelpers.FromRelativePath(metadata.IconPath),
        };

        var moreCommands = new List<ICommandContextItem>();
        if (string.Equals(metadata.SettingsKey, "Awake", StringComparison.OrdinalIgnoreCase))
        {
            listItem.Subtitle = AwakeStatusService.BuildSubtitle();
            AwakeCommandsFactory.PopulateModuleCommands(moreCommands);
        }

        if (metadata.AdditionalLaunchCommands is { Length: > 0 })
        {
            foreach (var additional in metadata.AdditionalLaunchCommands)
            {
                var additionalCommand = new LaunchModuleCommand(
                    metadata.Title,
                    additional.LaunchEvent,
                    additional.LaunchExecutable,
                    additional.LaunchArguments,
                    additional.Title);

                var contextItem = new CommandContextItem(additionalCommand);

                if (!string.IsNullOrWhiteSpace(additional.Title))
                {
                    contextItem.Title = additional.Title;
                }

                if (!string.IsNullOrWhiteSpace(additional.Subtitle))
                {
                    contextItem.Subtitle = additional.Subtitle;
                }

                moreCommands.Add(contextItem);
            }
        }

        if (!usesSettingsForPrimary && !string.IsNullOrEmpty(metadata.SettingsKey))
        {
            moreCommands.Add(new CommandContextItem(new OpenPowerToysSettingsCommand(metadata.Title, metadata.SettingsKey)));
        }

        if (moreCommands.Count > 0)
        {
            listItem.MoreCommands = moreCommands.ToArray();
        }

        return listItem;
    }

    private static InvokableCommand CreatePrimaryCommand(ModuleMetadata metadata, out bool usesSettings)
    {
        usesSettings = false;

        if (!string.IsNullOrEmpty(metadata.LaunchEvent) || !string.IsNullOrEmpty(metadata.LaunchExecutable))
        {
            return new LaunchModuleCommand(metadata.Title, metadata.LaunchEvent, metadata.LaunchExecutable, metadata.LaunchArguments);
        }

        if (!string.IsNullOrEmpty(metadata.SettingsKey))
        {
            usesSettings = true;
            return new OpenPowerToysSettingsCommand(metadata.Title, metadata.SettingsKey);
        }

        return new NoOpCommand();
    }

    private static bool Matches(ModuleMetadata metadata, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        if (Contains(metadata.Title, searchText) || Contains(metadata.Subtitle, searchText))
        {
            return true;
        }

        if (metadata.AdditionalLaunchCommands is { Length: > 0 })
        {
            foreach (var additional in metadata.AdditionalLaunchCommands)
            {
                if (Contains(additional.Title, searchText) || Contains(additional.Subtitle, searchText))
                {
                    return true;
                }

                if (additional.SearchTerms is { Length: > 0 } && additional.SearchTerms.Any(term => Contains(term, searchText)))
                {
                    return true;
                }
            }
        }

        return metadata.SearchTerms.Any(term => Contains(term, searchText));
    }

    private static bool Matches(ListItem item, IReadOnlyCollection<string> searchTerms, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        if (Contains(item.Title, searchText) || Contains(item.Subtitle, searchText))
        {
            return true;
        }

        return searchTerms.Any(term => Contains(term, searchText));
    }

    private static bool Contains(string? source, string query)
    {
        return !string.IsNullOrEmpty(source) && source.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }
}
