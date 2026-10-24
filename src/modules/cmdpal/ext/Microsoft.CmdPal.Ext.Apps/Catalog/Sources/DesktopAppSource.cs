// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

internal sealed class DesktopAppSource : DirectoryWin32ProgramSource
{
    private readonly AllAppsSettings _settings;

    public override string Id => "desktop";

    /// <summary>Gets the source priority that prefers Desktop over Start Menu at equal shortcut ownership and depth.</summary>
    public override int Priority => 9;

    public override bool IsEnabled => _settings.EnableDesktopSource;

    public override Win32ProgramSourceProfile Profile
        => Win32ProgramSourceProfile.RecurseSubdirectories
            | (_settings.IncludeNonAppsOnDesktop ? Win32ProgramSourceProfile.IncludeNonApplications : 0);

    /// <inheritdoc />
    public override int MaximumDepth { get; }

    /// <summary>Initializes a new instance of the <see cref="DesktopAppSource"/> class. Creates discovery over the current user and shared Desktop folders.</summary>
    public DesktopAppSource(AllAppsSettings settings)
        : this(
            settings,
            [
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            ])
    {
    }

    /// <summary>Creates Desktop discovery with explicit personal and shared roots.</summary>
    internal DesktopAppSource(AllAppsSettings settings, IReadOnlyList<string> desktopDirectories)
        : base(desktopDirectories, settings.ProgramSuffixes)
    {
        _settings = settings;
        MaximumDepth = settings.DesktopMaximumDepth;
    }

    /// <inheritdoc />
    public override AppShortcutPreference? GetShortcutPreference(string path)
    {
        return GetStandardShortcutPreference(path, isStartMenu: false);
    }
}
