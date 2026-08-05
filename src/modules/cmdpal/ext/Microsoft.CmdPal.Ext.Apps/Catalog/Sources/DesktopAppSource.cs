// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

internal sealed class DesktopAppSource : DirectoryWin32ProgramSource
{
    private readonly AllAppsSettings _settings;

    public override string Id => "desktop";

    public override int Priority => 20;

    public override bool IsEnabled => _settings.EnableDesktopSource;

    public override Win32ProgramSourceProfile Profile
        => Win32ProgramSourceProfile.RecurseSubdirectories
            | (_settings.IncludeNonAppsOnDesktop ? Win32ProgramSourceProfile.IncludeNonApplications : 0);

    /// <summary>Initializes a new instance of the <see cref="DesktopAppSource"/> class. Creates discovery over the current user and shared Desktop folders.</summary>
    public DesktopAppSource(AllAppsSettings settings)
        : base(
            [
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            ],
            settings.ProgramSuffixes)
    {
        _settings = settings;
    }
}
