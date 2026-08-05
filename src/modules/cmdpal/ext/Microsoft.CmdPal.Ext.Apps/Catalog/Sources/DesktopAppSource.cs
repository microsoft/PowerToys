// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

internal sealed class DesktopAppSource : DirectoryWin32ProgramSource
{
    private readonly AllAppsSettings _settings;

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

    public override string Id => "desktop";

    public override int Priority => 20;

    public override bool IsEnabled => _settings.EnableDesktopSource;

    public override bool IncludeNonApps => _settings.IncludeNonAppsOnDesktop;
}
