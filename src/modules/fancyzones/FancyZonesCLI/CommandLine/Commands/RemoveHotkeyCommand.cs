// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;

using FancyZonesEditorCommon.Data;
using FancyZonesEditorCommon.Utils;

namespace FancyZonesCLI.CommandLine.Commands;

internal sealed partial class RemoveHotkeyCommand : FancyZonesBaseCommand
{
    private readonly Argument<int> _key;

    public RemoveHotkeyCommand()
        : base("remove-hotkey", "Remove hotkey assignment")
    {
        AddAlias("rhk");

        _key = new Argument<int>("key", "Hotkey index (0-9)");
        AddArgument(_key);
    }

    protected override string Execute(InvocationContext context)
    {
        // FancyZones running guard is handled by FancyZonesBaseCommand.
        int key = context.ParseResult.GetValueForArgument(_key);

        var hotkeysWrapper = FancyZonesDataIO.ReadLayoutHotkeys();

        if (hotkeysWrapper.LayoutHotkeys == null)
        {
            return "No hotkeys configured.";
        }

        var hotkeysList = hotkeysWrapper.LayoutHotkeys;
        var removed = hotkeysList.RemoveAll(h => h.Key == key);
        if (removed == 0)
        {
            return $"No hotkey assigned to key {key}";
        }

        // Save.
        var newWrapper = new LayoutHotkeys.LayoutHotkeysWrapper { LayoutHotkeys = hotkeysList };
        FancyZonesDataIO.WriteLayoutHotkeys(newWrapper);

        // Notify FancyZones.
        NativeMethods.NotifyFancyZones(NativeMethods.WM_PRIV_LAYOUT_HOTKEYS_FILE_UPDATE);

        return string.Empty;
    }
}
