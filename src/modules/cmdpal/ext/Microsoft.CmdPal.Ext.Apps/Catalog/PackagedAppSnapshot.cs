// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Stores the immutable packaged-application data retained for catalog projection and policy.
/// </summary>
internal sealed record PackagedAppSnapshot : IAppCatalogPayload
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string UserModelId { get; init; } = string.Empty;

    public string PackageFamilyName { get; init; } = string.Empty;

    public string PackageFullName { get; init; } = string.Empty;

    public string PackageLocation { get; init; } = string.Empty;

    public string LogoPath { get; init; } = string.Empty;

    public string JumboLogoPath { get; init; } = string.Empty;

    public bool CanRunElevated { get; init; }

    public bool IsNonRemovable { get; init; }

    /// <summary>Captures only the packaged-application data needed after discovery completes.</summary>
    public static PackagedAppSnapshot From(IUWPApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return new PackagedAppSnapshot
        {
            Name = app.Name,
            Description = app.Description,
            UserModelId = app.UserModelId,
            PackageFamilyName = app.Package.FamilyName,
            PackageFullName = app.Package.FullName,
            PackageLocation = app.Package.Location,
            LogoPath = app.LogoType != LogoType.Error ? app.LogoPath : string.Empty,
            JumboLogoPath = app.JumboLogoType != LogoType.Error ? app.JumboLogoPath : string.Empty,
            CanRunElevated = app.CanRunElevated,
            IsNonRemovable = app.Package.IsNonRemovable,
        };
    }

    public AppItem ToAppItem()
    {
        return new AppItem
        {
            Name = Name,
            Subtitle = Description,
            Type = Resources.packaged_application,
            IcoPath = LogoPath,
            JumboIconPath = JumboLogoPath,
            DirPath = PackageLocation,
            UserModelId = UserModelId,
            IsPackaged = true,
            Commands = GetCommands(),
            AppIdentifier = UserModelId,
            PackageFamilyName = PackageFamilyName,
        };
    }

    private List<IContextItem> GetCommands()
    {
        List<IContextItem> commands = [];

        if (CanRunElevated)
        {
            commands.Add(new CommandContextItem(
                new RunAsAdminCommand(UserModelId, string.Empty, true))
            {
                RequestedShortcut = KeyChords.RunAsAdministrator,
            });
        }

        commands.Add(new CommandContextItem(new CopyPathCommand(PackageLocation))
        {
            RequestedShortcut = KeyChords.CopyFilePath,
        });
        commands.Add(new CommandContextItem(
            new OpenFileCommand(PackageLocation)
            {
                Icon = new("\uE838"),
                Name = Resources.open_location,
            })
        {
            RequestedShortcut = KeyChords.OpenFileLocation,
        });
        commands.Add(new CommandContextItem(new OpenInConsoleCommand(PackageLocation))
        {
            RequestedShortcut = KeyChords.OpenInConsole,
        });

        if (!IsNonRemovable)
        {
            commands.Add(new CommandContextItem(
                new UninstallApplicationConfirmation(Name, PackageFullName))
            {
                RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.Delete),
                IsCritical = true,
            });
        }

        return commands;
    }
}
