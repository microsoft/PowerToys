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
/// Mock implementation of IUWPApplication for unit testing
/// </summary>
public class MockUWPApplication : IUWPApplication
{
    public string AppListEntry { get; set; } = string.Empty;

    public string UniqueIdentifier { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string UserModelId { get; set; } = string.Empty;

    public string BackgroundColor { get; set; } = string.Empty;

    public string EntryPoint { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public bool CanRunElevated { get; set; }

    public string LogoPath { get; set; } = string.Empty;

    public LogoType LogoType { get; set; } = LogoType.Colored;

    public UWP Package { get; set; } = null!;

    public string Name => DisplayName;

    public string Location => Package?.Location ?? string.Empty;

    public string LocationLocalized => Package?.LocationLocalized ?? string.Empty;

    public string GetAppIdentifier()
    {
        return UserModelId;
    }

    public List<IContextItem> GetCommands()
    {
        return new List<IContextItem>();
    }

    public void UpdateLogoPath(Theme theme)
    {
        // Mock implementation - no-op for testing
    }

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
