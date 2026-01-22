// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Modules;

namespace PowerToysExtension.Helpers;

/// <summary>
/// Aggregates commands exposed by individual module providers and applies fuzzy filtering.
/// </summary>
internal static class ModuleCommandCatalog
{
    private static readonly ModuleCommandProvider[] Providers =
    [
        new AwakeModuleCommandProvider(),
        new AdvancedPasteModuleCommandProvider(),
        new WorkspacesModuleCommandProvider(),
        new LightSwitchModuleCommandProvider(),
        new PowerToysRunModuleCommandProvider(),
        new ScreenRulerModuleCommandProvider(),
        new ShortcutGuideModuleCommandProvider(),
        new TextExtractorModuleCommandProvider(),
        new ZoomItModuleCommandProvider(),
        new ColorPickerModuleCommandProvider(),
        new AlwaysOnTopModuleCommandProvider(),
        new CropAndLockModuleCommandProvider(),
        new FancyZonesModuleCommandProvider(),
        new KeyboardManagerModuleCommandProvider(),
        new MouseUtilsModuleCommandProvider(),
        new MouseWithoutBordersModuleCommandProvider(),
        new QuickAccentModuleCommandProvider(),
        new FileExplorerAddonsModuleCommandProvider(),
        new FileLocksmithModuleCommandProvider(),
        new ImageResizerModuleCommandProvider(),
        new NewPlusModuleCommandProvider(),
        new PeekModuleCommandProvider(),
        new PowerRenameModuleCommandProvider(),
        new CommandNotFoundModuleCommandProvider(),
        new EnvironmentVariablesModuleCommandProvider(),
        new HostsModuleCommandProvider(),
        new RegistryPreviewModuleCommandProvider(),
    ];

    public static IListItem[] GetAllItems()
    {
        return [.. Providers.SelectMany(provider => provider.BuildCommands())];
    }
}
