// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Common.Search.FuzzSearch;
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

    public static IListItem[] FilteredItems(string query)
    {
        var all = Providers.SelectMany(provider => provider.BuildCommands()).ToList();
        if (string.IsNullOrWhiteSpace(query))
        {
            return [.. all];
        }

        var matched = new List<Tuple<int, ListItem>>();
        foreach (var item in all)
        {
            var result = StringMatcher.FuzzyMatch(query, item.Title);
            if (result.Success)
            {
                matched.Add(new Tuple<int, ListItem>(result.Score, item));
            }
        }

        matched.Sort((x, y) => y.Item1.CompareTo(x.Item1));
        return [.. matched.Select(x => x.Item2)];
    }
}
