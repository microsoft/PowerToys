// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Stores the immutable metadata for one manifest application in the catalog and its cache.
/// </summary>
/// <remarks>Package identity and installation data are flattened at capture time without retaining a live package object.</remarks>
internal sealed record PackagedAppPayload : IAppCatalogPayload
{
    /// <summary>Gets the application's display name after resolving any manifest resource reference.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the resolved manifest description used as the app's subtitle.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the application's AUMID used for packaged activation and canonical catalog identity.</summary>
    public string AppUserModelId { get; init; } = string.Empty;

    /// <summary>Gets the manifest-declared executable without assuming a resolved activation target.</summary>
    /// <remarks>This may be a relative path or empty. Only its filename contributes search metadata; activation uses <see cref="AppUserModelId"/>.</remarks>
    public string Executable { get; init => field = value ?? string.Empty; } = string.Empty;

    /// <summary>Gets the package family identity shared by versions and architectures of the package.</summary>
    public string PackageFamilyName { get; init; } = string.Empty;

    /// <summary>Gets the full identity of the installed package, including its version and architecture.</summary>
    /// <remarks>Used to identify the installed package for the uninstall action.</remarks>
    public string PackageFullName { get; init; } = string.Empty;

    /// <summary>Gets the package installation directory used by location and path context actions.</summary>
    public string PackageLocation { get; init; } = string.Empty;

    /// <summary>Gets the resolved small-logo file used for the row icon.</summary>
    public string LogoPath { get; init; } = string.Empty;

    /// <summary>Gets the resolved logo file used for the details icon.</summary>
    public string JumboLogoPath { get; init; } = string.Empty;

    /// <summary>Gets whether this application's manifest declares full-trust or medium-integrity execution.</summary>
    /// <remarks>Controls whether the packaged app exposes a Run as administrator context action.</remarks>
    public bool CanRunElevated { get; init; }

    /// <summary>Gets whether Windows marks the containing package as non-removable.</summary>
    /// <remarks>Suppresses the uninstall action when <see langword="true"/>.</remarks>
    public bool IsNonRemovable { get; init; }

    /// <summary>Captures only the packaged-application data needed after discovery completes.</summary>
    /// <param name="app">The resolved manifest metadata and its containing package.</param>
    /// <returns>An immutable payload with package identity, installation data and resolved logo files.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    public static PackagedAppPayload From(PackagedAppMetadata app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return new PackagedAppPayload
        {
            Name = app.Name,
            Description = app.Description,
            AppUserModelId = app.AppUserModelId,
            Executable = app.Executable,
            PackageFamilyName = app.Package.FamilyName,
            PackageFullName = app.Package.FullName,
            PackageLocation = app.Package.InstalledLocation,
            LogoPath = app.LogoPath,
            JumboLogoPath = app.JumboLogoPath,
            CanRunElevated = app.CanRunElevated,
            IsNonRemovable = app.Package.IsNonRemovable,
        };
    }

    /// <inheritdoc />
    /// <remarks>Translates manifest fields into presentation data; the containing <see cref="AppCatalogItem"/> supplies canonical identity, aliases and search terms.</remarks>
    public AppItem ToAppItem()
    {
        return new AppItem
        {
            Name = Name,
            Subtitle = Description,
            AppTypeLabel = Resources.packaged_application,
            IconSource = LogoPath,
            JumboIconSource = JumboLogoPath,
            DirectoryPath = PackageLocation,
            AppUserModelId = AppUserModelId,
            IsPackaged = true,
            Commands = GetCommands(),
            PackageFamilyName = PackageFamilyName,
        };
    }

    /// <summary>Gets the released name-based command ID for resolving references to this source representation.</summary>
    /// <remarks>The canonical typed command ID is derived separately from <see cref="AppUserModelId"/>.</remarks>
    /// <returns>The legacy ID derived from <see cref="Name"/> and <see cref="Description"/>, with an empty launch path.</returns>
    public string GetCommandId()
    {
        return AppCommand.GenerateId(Name, Description, string.Empty);
    }

    /// <inheritdoc />
    /// <remarks>Uses <see cref="AppUserModelId"/> to associate shortcut and execution alias representations with this app.</remarks>
    string? IAppCatalogPayload.GetCanonicalIdentityHint()
    {
        return string.IsNullOrWhiteSpace(AppUserModelId) ? null : AppIdentity.ForPackaged(AppUserModelId);
    }

    /// <inheritdoc />
    /// <remarks>Provides only package-local executable candidates. The catalog rejects ambiguous associations; activation still uses the AUMID.</remarks>
    string? IAppCatalogPayload.GetCanonicalTargetPath()
    {
        if (string.IsNullOrWhiteSpace(Executable)
            || string.IsNullOrWhiteSpace(PackageLocation)
            || !Path.IsPathFullyQualified(PackageLocation)
            || Path.IsPathRooted(Executable)
            || Executable.Contains(':')
            || !PathHelpers.IsExecutablePath(Executable))
        {
            return null;
        }

        try
        {
            var packagePath = Path.GetFullPath(PackageLocation);
            var targetPath = Path.GetFullPath(Executable, packagePath);
            return PathHelpers.IsPathInsideDirectory(targetPath, packagePath) ? targetPath : null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>Builds location actions, adding elevation and uninstall actions when the captured metadata allows them.</summary>
    private List<IContextItem> GetCommands()
    {
        List<IContextItem> commands = [];

        if (CanRunElevated)
        {
            commands.Add(new CommandContextItem(
                new RunAsAdminCommand(AppUserModelId, string.Empty, true))
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
                Icon = Icons.OpenPathIcon,
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
                RequestedShortcut = KeyChords.Delete,
                IsCritical = true,
            });
        }

        return commands;
    }
}
