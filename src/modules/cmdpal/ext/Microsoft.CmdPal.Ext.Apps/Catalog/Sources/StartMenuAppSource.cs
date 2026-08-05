// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

internal sealed class StartMenuAppSource : DirectoryWin32ProgramSource
{
    private readonly AllAppsSettings _settings;

    public override string Id => "start-menu";

    public override int Priority => 10;

    public override bool IsEnabled => _settings.EnableStartMenuSource;

    public override Win32ProgramSourceProfile Profile
        => Win32ProgramSourceProfile.RecurseSubdirectories
            | (_settings.IncludeNonAppsInStartMenu ? Win32ProgramSourceProfile.IncludeNonApplications : 0);

    /// <summary>Initializes a new instance of the <see cref="StartMenuAppSource"/> class. Creates discovery over the user and shared Start Menu folders while excluding Startup folders.</summary>
    public StartMenuAppSource(AllAppsSettings settings)
        : this(
            settings,
            [
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            ],
            [
                Environment.GetFolderPath(Environment.SpecialFolder.Startup, Environment.SpecialFolderOption.DoNotVerify),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup, Environment.SpecialFolderOption.DoNotVerify),
            ])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StartMenuAppSource"/> class. Creates Start Menu discovery with explicit roots and Startup-folder exclusions.</summary>
    internal StartMenuAppSource(
        AllAppsSettings settings,
        IReadOnlyList<string> startMenuDirectories,
        IReadOnlyList<string> startupDirectories)
        : base(startMenuDirectories, settings.ProgramSuffixes, startupDirectories)
    {
        _settings = settings;
    }
}
