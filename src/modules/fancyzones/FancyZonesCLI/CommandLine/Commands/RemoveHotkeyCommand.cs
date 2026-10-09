// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;

using FancyZonesEditorCommon.Data;
using FancyZonesEditorCommon.Utils;

namespace FancyZonesCLI.CommandLine.Commands;

internal sealed partial class RemoveHotkeyCommand : FancyZonesBaseCommand
{
    private readonly Argument<int> _key;

    public RemoveHotkeyCommand()
        : base("remove-hotkey", Properties.Resources.cmd_remove_hotkey)
    {
        AddAlias("rhk");

        _key = new Argument<int>("key", Properties.Resources.remove_hotkey_arg_key);
        AddArgument(_key);
    }

    protected override string Execute(InvocationContext context)
    {
        // FancyZones running guard is handled by FancyZonesBaseCommand.
        int key = context.ParseResult.GetValueForArgument(_key);

        var hotkeysWrapper = FancyZonesDataIO.ReadLayoutHotkeys();

        if (hotkeysWrapper.LayoutHotkeys == null)
        {
            return Properties.Resources.remove_hotkey_no_hotkeys;
        }

        var hotkeysList = hotkeysWrapper.LayoutHotkeys;
        var removed = hotkeysList.RemoveAll(h => h.Key == key);
        if (removed == 0)
        {
            return string.Format(CultureInfo.InvariantCulture, Properties.Resources.remove_hotkey_not_found, key);
        }

        // Save.
        var newWrapper = new LayoutHotkeys.LayoutHotkeysWrapper { LayoutHotkeys = hotkeysList };
        FancyZonesDataIO.WriteLayoutHotkeys(newWrapper);

        // Notify FancyZones.
        NativeMethods.NotifyFancyZones(NativeMethods.WM_PRIV_LAYOUT_HOTKEYS_FILE_UPDATE);

        return string.Empty;
    }
}
