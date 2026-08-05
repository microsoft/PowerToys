// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Stores the immutable Win32 application data retained for catalog projection and policy.
/// </summary>
internal sealed record Win32AppPayload : IAppCatalogPayload
{
    public string Name { get; init; } = string.Empty;

    public string IcoPath { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public string ParentDirectory { get; init; } = string.Empty;

    public string LnkFilePath { get; init; } = string.Empty;

    public Win32Program.ApplicationType AppType { get; init; }

    /// <summary>Captures only the application data needed after discovery completes.</summary>
    public static Win32AppPayload From(Win32Program program)
    {
        ArgumentNullException.ThrowIfNull(program);

        return new Win32AppPayload
        {
            Name = program.Name,
            IcoPath = program.IcoPath,
            Description = program.Description,
            FullPath = program.FullPath,
            ParentDirectory = program.ParentDirectory,
            LnkFilePath = program.LnkFilePath,
            AppType = program.AppType,
        };
    }

    /// <inheritdoc />
    public AppItem ToAppItem()
    {
        var iconPath = string.IsNullOrEmpty(IcoPath)
            && AppType != Win32Program.ApplicationType.InternetShortcutApplication
                ? FullPath
                : IcoPath;

        return new AppItem
        {
            Name = Name,
            Subtitle = Description,
            Type = GetApplicationType(),
            IcoPath = iconPath,
            ExePath = LaunchPath,
            DirPath = ParentDirectory,
            Commands = GetCommands(),
            AppIdentifier = $"{Name}|{FullPath}",
            FullExecutablePath = FullPath,
        };
    }

    private string LaunchPath => !string.IsNullOrEmpty(LnkFilePath) ? LnkFilePath : FullPath;

    private string GetApplicationType()
    {
        return AppType switch
        {
            Win32Program.ApplicationType.Win32Application
                or Win32Program.ApplicationType.ShortcutApplication
                or Win32Program.ApplicationType.ApprefApplication => Resources.application,
            Win32Program.ApplicationType.InternetShortcutApplication => Resources.internet_shortcut_application,
            Win32Program.ApplicationType.WebApplication => Resources.web_application,
            Win32Program.ApplicationType.RunCommand => Resources.run_command,
            Win32Program.ApplicationType.Folder => Resources.folder,
            Win32Program.ApplicationType.GenericFile => Resources.file,
            _ => string.Empty,
        };
    }

    private List<IContextItem> GetCommands()
    {
        List<IContextItem> commands = [];

        if (AppType != Win32Program.ApplicationType.InternetShortcutApplication
            && AppType != Win32Program.ApplicationType.Folder
            && AppType != Win32Program.ApplicationType.GenericFile)
        {
            commands.Add(new CommandContextItem(new RunAsAdminCommand(LaunchPath, ParentDirectory, packaged: false))
            {
                RequestedShortcut = KeyChords.RunAsAdministrator,
            });
            commands.Add(new CommandContextItem(new RunAsUserCommand(LaunchPath, ParentDirectory))
            {
                RequestedShortcut = KeyChords.RunAsDifferentUser,
            });
        }

        commands.Add(new CommandContextItem(new CopyPathCommand(FullPath))
        {
            RequestedShortcut = KeyChords.CopyFilePath,
        });
        commands.Add(new CommandContextItem(new ShowFileInFolderCommand(LaunchPath)
        {
            Name = Resources.open_location,
        })
        {
            RequestedShortcut = KeyChords.OpenFileLocation,
        });
        commands.Add(new CommandContextItem(new OpenInConsoleCommand(ParentDirectory))
        {
            RequestedShortcut = KeyChords.OpenInConsole,
        });

        if ((AppType is Win32Program.ApplicationType.ShortcutApplication
                or Win32Program.ApplicationType.ApprefApplication
                or Win32Program.ApplicationType.Win32Application)
            && !PathHelpers.IsSystemRootPath(FullPath)
            && !IsShortcutTarget())
        {
            commands.Add(new CommandContextItem(new UninstallApplicationConfirmation(Name))
            {
                RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.Delete),
                IsCritical = true,
            });
        }

        return commands;
    }

    private bool IsShortcutTarget()
    {
        if (!PathHelpers.IsShortcutFile(FullPath))
        {
            return false;
        }

        return PathHelpers.IsShortcutFile($"{Name}|{FullPath}");
    }
}
