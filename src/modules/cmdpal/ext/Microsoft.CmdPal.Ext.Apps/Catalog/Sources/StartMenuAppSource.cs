// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

internal sealed class StartMenuAppSource : DirectoryWin32ProgramSource
{
    private readonly AllAppsSettings _settings;

    public StartMenuAppSource(AllAppsSettings settings)
        : base(
            [
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            ],
            settings.ProgramSuffixes)
    {
        _settings = settings;
    }

    public override string Id => "start-menu";

    public override int Priority => 10;

    public override bool IsEnabled => _settings.EnableStartMenuSource;

    public override bool IncludeNonApps => _settings.IncludeNonAppsInStartMenu;
}
