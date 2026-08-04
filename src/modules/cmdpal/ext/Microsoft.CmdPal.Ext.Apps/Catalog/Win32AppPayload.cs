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

    /// <summary>Gets arguments that distinguish otherwise identical executable representations.</summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>Gets the shortcut working directory that distinguishes launch behavior.</summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>Gets the packaged application identity represented by this Win32 entry.</summary>
    public string PackagedAppUserModelId { get; init; } = string.Empty;

    /// <summary>Gets the executable target resolved from an app execution alias.</summary>
    public string AppExecutionAliasTargetPath { get; init; } = string.Empty;

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
            Arguments = program.Arguments,
            WorkingDirectory = program.WorkingDirectory,
            PackagedAppUserModelId = !string.IsNullOrWhiteSpace(program.PackagedAppUserModelId)
                ? program.PackagedAppUserModelId
                : program.AppExecutionAlias?.Aumid ?? string.Empty,
            ExplicitAppUserModelId = program.ExplicitAppUserModelId,
            AppExecutionAliasTargetPath = program.AppExecutionAlias?.TargetPath ?? string.Empty,
            AppType = program.AppType,
        };
    }

    /// <inheritdoc />
    string? IAppCatalogPayload.GetCanonicalIdentityHint()
    {
        return string.IsNullOrEmpty(Arguments) && string.IsNullOrEmpty(DistinctWorkingDirectory) && !string.IsNullOrWhiteSpace(PackagedAppUserModelId)
            ? AppIdentity.ForPackaged(PackagedAppUserModelId)
            : null;
    }

    /// <inheritdoc />
    string? IAppCatalogPayload.GetCanonicalTargetPath()
    {
        if (!string.IsNullOrEmpty(Arguments) || !string.IsNullOrEmpty(DistinctWorkingDirectory) || !Win32Program.UsesExecutableTargetIdentity(AppType))
        {
            return null;
        }

        var targetPath = !string.IsNullOrWhiteSpace(PackagedAppUserModelId)
            ? AppExecutionAliasTargetPath
            : FullPath;
        return string.IsNullOrWhiteSpace(targetPath) ? null : targetPath;
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
            Arguments = Arguments,
            DirPath = ParentDirectory,
            UserModelId = !string.IsNullOrWhiteSpace(PackagedAppUserModelId)
                ? PackagedAppUserModelId
                : ExplicitAppUserModelId,
            Commands = GetCommands(),
            AppIdentifier = $"{Name}|{FullPath}",
            FullExecutablePath = !string.IsNullOrWhiteSpace(AppExecutionAliasTargetPath) ? AppExecutionAliasTargetPath : FullPath,
        };
    }

    private string LaunchPath => !string.IsNullOrEmpty(LnkFilePath) ? LnkFilePath : FullPath;

    private string DistinctWorkingDirectory => Win32Program.GetDistinctWorkingDirectory(
        !string.IsNullOrWhiteSpace(AppExecutionAliasTargetPath) ? AppExecutionAliasTargetPath : FullPath,
        WorkingDirectory,
        ExplicitAppUserModelId);

    /// <inheritdoc />
    public string GetCommandId() => AppCommand.GenerateId(Name, Description, LaunchPath);

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

    /// <summary>Gets the explicit Windows application ID retained as metadata, independent of launch identity.</summary>
    public string ExplicitAppUserModelId { get; init => field = value ?? string.Empty; } = string.Empty;
}
