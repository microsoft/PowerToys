// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

/// <summary>
/// Mock implementation of IUWPApplication for unit testing.
/// </summary>
public class MockUWPApplication : IUWPApplication
{
    /// <summary>
    /// Gets or sets the app list entry.
    /// </summary>
    public string AppListEntry { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public string UniqueIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user model ID.
    /// </summary>
    public string UserModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public string BackgroundColor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the entry point.
    /// </summary>
    public string EntryPoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the application is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the application can run elevated.
    /// </summary>
    public bool CanRunElevated { get; set; }

    /// <summary>
    /// Gets or sets the logo path.
    /// </summary>
    public string LogoPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the logo type.
    /// </summary>
    public LogoType LogoType { get; set; } = LogoType.Colored;

    /// <summary>
    /// Gets or sets the UWP package.
    /// </summary>
    public UWP Package { get; set; } = null!;

    /// <summary>
    /// Gets the name of the application.
    /// </summary>
    public string Name => DisplayName;

    /// <summary>
    /// Gets the location of the application.
    /// </summary>
    public string Location => Package?.Location ?? string.Empty;

    /// <summary>
    /// Gets the localized location of the application.
    /// </summary>
    public string LocationLocalized => Package?.LocationLocalized ?? string.Empty;

    /// <summary>
    /// Gets the application identifier.
    /// </summary>
    /// <returns>The user model ID of the application.</returns>
    public string GetAppIdentifier()
    {
        return UserModelId;
    }

    /// <summary>
    /// Gets the commands available for this application.
    /// </summary>
    /// <returns>A list of context items.</returns>
    public List<IContextItem> GetCommands()
    {
        return new List<IContextItem>();
    }

    /// <summary>
    /// Updates the logo path based on the specified theme.
    /// </summary>
    /// <param name="theme">The theme to use for the logo.</param>
    public void UpdateLogoPath(Theme theme)
    {
        // Mock implementation - no-op for testing
    }

    /// <summary>
    /// Converts this UWP application to an AppItem.
    /// </summary>
    /// <returns>An AppItem representation of this UWP application.</returns>
    public AppItem ToAppItem()
    {
        var iconPath = LogoType != LogoType.Error ? LogoPath : string.Empty;
        return new AppItem()
        {
            Name = Name,
            Subtitle = Description,
            Type = "Packaged Application", // Equivalent to UWPApplication.Type()
            IcoPath = iconPath,
            DirPath = Location,
            UserModelId = UserModelId,
            IsPackaged = true,
            Commands = GetCommands(),
            AppIdentifier = GetAppIdentifier(),
        };
    }
}
