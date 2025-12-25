// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Linq;

using FancyZonesEditorCommon.Data;
using FancyZonesEditorCommon.Utils;

namespace FancyZonesCLI.CommandLine.Commands;

internal sealed partial class SetHotkeyCommand : FancyZonesBaseCommand
{
    private readonly Argument<int> _key;
    private readonly Argument<string> _layout;

    public SetHotkeyCommand()
        : base("set-hotkey", Properties.Resources.cmd_set_hotkey)
    {
        AddAlias("shk");

        _key = new Argument<int>("key", Properties.Resources.set_hotkey_arg_key);
        _layout = new Argument<string>("layout", Properties.Resources.set_hotkey_arg_layout);

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
            throw new InvalidOperationException(Properties.Resources.set_hotkey_error_invalid_key);
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
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Properties.Resources.set_hotkey_error_not_custom, layout));
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
