// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;

using FancyZonesEditorCommon.Data;
using FancyZonesEditorCommon.Utils;

namespace FancyZonesCLI.CommandLine.Commands;

internal sealed partial class SetHotkeyCommand : FancyZonesBaseCommand
{
    private readonly Argument<int> _key;
    private readonly Argument<string> _layout;

    public SetHotkeyCommand()
        : base("set-hotkey", "Assign hotkey (0-9) to a custom layout")
    {
        AddAlias("shk");

        _key = new Argument<int>("key", "Hotkey index (0-9)");
        _layout = new Argument<string>("layout", "Custom layout UUID");

        AddArgument(_key);
        AddArgument(_layout);
    }

    protected override string Execute(InvocationContext context)
    {
        // FancyZones running guard is handled by FancyZonesBaseCommand.
        int key = context.ParseResult.GetValueForArgument(_key);
        string layout = context.ParseResult.GetValueForArgument(_layout);

        if (key < 0 || key > 9)
        {
            throw new InvalidOperationException("Key must be between 0 and 9.");
        }

        // Editor only allows assigning hotkeys to existing custom layouts.
        var customLayouts = FancyZonesDataIO.ReadCustomLayouts();

        CustomLayouts.CustomLayoutWrapper? matchedLayout = null;
        if (customLayouts.CustomLayouts != null)
        {
            foreach (var candidate in customLayouts.CustomLayouts)
            {
                if (candidate.Uuid.Equals(layout, StringComparison.OrdinalIgnoreCase))
                {
                    matchedLayout = candidate;
                    break;
                }
            }
        }

        if (!matchedLayout.HasValue)
        {
            throw new InvalidOperationException($"Layout '{layout}' is not a custom layout UUID.");
        }

        string layoutName = matchedLayout.Value.Name;

        var hotkeysWrapper = FancyZonesDataIO.ReadLayoutHotkeys();

        var hotkeysList = hotkeysWrapper.LayoutHotkeys ?? new List<LayoutHotkeys.LayoutHotkeyWrapper>();

        // Match editor behavior:
        // - One key maps to one layout
        // - One layout maps to at most one key
        hotkeysList.RemoveAll(h => h.Key == key);
        hotkeysList.RemoveAll(h => string.Equals(h.LayoutId, layout, StringComparison.OrdinalIgnoreCase));

        // Add new hotkey.
        hotkeysList.Add(new LayoutHotkeys.LayoutHotkeyWrapper { Key = key, LayoutId = layout });

        // Save.
        var newWrapper = new LayoutHotkeys.LayoutHotkeysWrapper { LayoutHotkeys = hotkeysList };
        FancyZonesDataIO.WriteLayoutHotkeys(newWrapper);

        // Notify FancyZones.
        NativeMethods.NotifyFancyZones(NativeMethods.WM_PRIV_LAYOUT_HOTKEYS_FILE_UPDATE);

        return string.Empty;
    }
}
