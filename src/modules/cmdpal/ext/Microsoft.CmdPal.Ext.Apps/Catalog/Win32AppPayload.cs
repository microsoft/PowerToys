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
/// Stores the immutable Win32 metadata for one source representation in the catalog and its cache.
/// </summary>
/// <remarks>Source-specific values are translated into presentation and launch data by <see cref="ToAppItem"/>.</remarks>
internal sealed record Win32AppPayload : IAppCatalogPayload
{
    /// <summary>Gets the source filename stem retained for released command IDs.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the Shell display name, or an empty string when unavailable.</summary>
    /// <remarks>Presentation falls back to <see cref="Name"/> when this value is empty or whitespace.</remarks>
    public string DisplayName { get; init => field = value ?? string.Empty; } = string.Empty;

    /// <summary>Gets the native icon location, which may include a resource index.</summary>
    public string IconLocation { get; init; } = string.Empty;

    /// <summary>Gets the shortcut description or executable file description used as the app's subtitle.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the discovered entry's path, its resolved shortcut target, or a supported game URL.</summary>
    /// <remarks>An execution alias retains its own path here; its resolved target is held in <see cref="AppExecutionAliasTargetPath"/>.</remarks>
    public string TargetPath { get; init; } = string.Empty;

    /// <summary>Gets the directory containing the discovered entry, not its target or working directory.</summary>
    public string ParentDirectory { get; init; } = string.Empty;

    /// <summary>Gets the original shortcut path used for activation when a filesystem target was resolved.</summary>
    /// <remarks>Empty for direct entries and shortcuts without a resolved filesystem target.</remarks>
    public string LnkFilePath { get; init; } = string.Empty;

    /// <summary>Gets the shortcut arguments retained for launch identity, search rules and diagnostics.</summary>
    /// <remarks>Activating the shortcut applies its configured arguments without appending this value separately.</remarks>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>Gets the shortcut's configured working directory, which may be relative or empty.</summary>
    /// <remarks>Only a directory that changes launch behavior is used to distinguish otherwise identical apps.</remarks>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>Gets the packaged application's AUMID obtained from a shortcut or app execution alias.</summary>
    /// <remarks>This enables cross-source deduplication; the Win32 representation still uses file or shortcut activation.</remarks>
    public string PackagedAppUserModelId { get; init; } = string.Empty;

    /// <summary>Gets the explicit Windows application ID retained as metadata, independent of launch identity.</summary>
    /// <remarks>This ID can identify an unpackaged desktop app and does not imply packaged activation.</remarks>
    public string ExplicitAppUserModelId { get; init => field = value ?? string.Empty; } = string.Empty;

    /// <summary>Gets the executable target resolved from an app execution alias, or an empty string when unavailable.</summary>
    public string AppExecutionAliasTargetPath { get; init; } = string.Empty;

    /// <summary>Gets the reader's classification used for the type label and available context actions.</summary>
    public Win32AppType AppType { get; init; }

    /// <summary>Gets the localized display name, falling back to the source filename stem.</summary>
    private string EffectiveDisplayName => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;

    /// <summary>Gets the original shortcut path when available, otherwise the file target or game URL.</summary>
    private string LaunchPath => !string.IsNullOrEmpty(LnkFilePath) ? LnkFilePath : TargetPath;

    /// <summary>Gets the normalized working directory that changes launch behavior, or an empty string for a default directory.</summary>
    private string DistinctWorkingDirectory => AppIdentity.GetDistinctWorkingDirectory(
        !string.IsNullOrWhiteSpace(AppExecutionAliasTargetPath) ? AppExecutionAliasTargetPath : TargetPath,
        WorkingDirectory,
        ExplicitAppUserModelId);

    /// <summary>Captures only the application data needed after discovery completes.</summary>
    /// <param name="program">The transient file and shortcut metadata read by discovery.</param>
    /// <returns>An immutable payload with execution alias identity and target data flattened into strings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="program"/> is <see langword="null"/>.</exception>
    public static Win32AppPayload From(Win32AppMetadata program)
    {
        ArgumentNullException.ThrowIfNull(program);

        return new Win32AppPayload
        {
            Name = program.Name,
            DisplayName = program.DisplayName,
            IconLocation = program.IconLocation,
            Description = program.Description,
            TargetPath = program.TargetPath,
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
    /// <remarks>Associates this entry with a packaged app only when arguments and a distinct working directory do not change launch behavior.</remarks>
    string? IAppCatalogPayload.GetCanonicalIdentityHint()
    {
        return string.IsNullOrEmpty(Arguments) && string.IsNullOrEmpty(DistinctWorkingDirectory) && !string.IsNullOrWhiteSpace(PackagedAppUserModelId)
            ? AppIdentity.ForPackaged(PackagedAppUserModelId)
            : null;
    }

    /// <inheritdoc />
    /// <remarks>Returns a safe executable association only for an eligible app type without launch-specific arguments or a distinct working directory.</remarks>
    string? IAppCatalogPayload.GetCanonicalTargetPath()
    {
        if (!string.IsNullOrEmpty(Arguments)
            || !string.IsNullOrEmpty(DistinctWorkingDirectory)
            || AppType is not (Win32AppType.Win32Application or Win32AppType.RunCommand or Win32AppType.WebApplication))
        {
            return null;
        }

        var targetPath = !string.IsNullOrWhiteSpace(PackagedAppUserModelId)
            ? AppExecutionAliasTargetPath
            : TargetPath;
        return string.IsNullOrWhiteSpace(targetPath) ? null : targetPath;
    }

    /// <inheritdoc />
    /// <remarks>The containing <see cref="AppCatalogItem"/> supplies canonical identity, aliases and search terms after this projection.</remarks>
    public AppItem ToAppItem()
    {
        var iconPath = string.IsNullOrEmpty(IconLocation)
            && AppType != Win32AppType.InternetShortcutApplication
                ? TargetPath
                : IconLocation;

        return new AppItem
        {
            Name = EffectiveDisplayName,
            Subtitle = Description,
            AppTypeLabel = GetApplicationTypeLabel(),
            IsWebApp = AppType == Win32AppType.WebApplication,
            IconSource = iconPath,
            LaunchTarget = LaunchPath,
            LaunchArguments = Arguments,
            DirectoryPath = ParentDirectory,
            AppUserModelId = !string.IsNullOrWhiteSpace(PackagedAppUserModelId)
                ? PackagedAppUserModelId
                : ExplicitAppUserModelId,
            Commands = GetCommands(),
            ResolvedTarget = !string.IsNullOrWhiteSpace(AppExecutionAliasTargetPath) ? AppExecutionAliasTargetPath : TargetPath,
        };
    }

    /// <summary>Gets the released name-based command ID for resolving references to this source representation.</summary>
    /// <remarks>The canonical typed command ID is derived separately from the containing catalog item's identity.</remarks>
    /// <returns>The legacy ID derived from <see cref="Name"/>, <see cref="Description"/> and <see cref="LaunchPath"/>.</returns>
    public string GetCommandId()
    {
        return AppCommand.GenerateId(Name, Description, LaunchPath);
    }

    /// <summary>Gets the localized presentation label for <see cref="AppType"/>.</summary>
    private string GetApplicationTypeLabel()
    {
        return AppType switch
        {
            Win32AppType.Win32Application
                or Win32AppType.ShortcutApplication
                or Win32AppType.ApprefApplication => Resources.application,
            Win32AppType.InternetShortcutApplication => Resources.internet_shortcut_application,
            Win32AppType.WebApplication => Resources.web_application,
            Win32AppType.RunCommand => Resources.run_command,
            Win32AppType.Folder => Resources.folder,
            Win32AppType.GenericFile => Resources.file,
            _ => string.Empty,
        };
    }

    /// <summary>Builds context actions appropriate for the target type and discovered entry's location.</summary>
    private List<IContextItem> GetCommands()
    {
        List<IContextItem> commands = [];

        if (AppType != Win32AppType.InternetShortcutApplication
            && AppType != Win32AppType.Folder
            && AppType != Win32AppType.GenericFile)
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

        commands.Add(new CommandContextItem(new CopyPathCommand(TargetPath))
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

        if ((AppType is Win32AppType.ShortcutApplication
                or Win32AppType.ApprefApplication
                or Win32AppType.Win32Application)
            && !PathHelpers.IsSystemRootPath(TargetPath)
            && !PathHelpers.IsShortcutFile(TargetPath))
        {
            commands.Add(new CommandContextItem(new UninstallApplicationConfirmation(EffectiveDisplayName))
            {
                RequestedShortcut = KeyChords.Delete,
                IsCritical = true,
            });
        }

        return commands;
    }
}
