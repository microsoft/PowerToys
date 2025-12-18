// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Linq;

using FancyZonesEditorCommon.Data;
using FancyZonesEditorCommon.Utils;

namespace FancyZonesCLI.CommandLine.Commands;

internal sealed partial class GetHotkeysCommand : FancyZonesBaseCommand
{
    public GetHotkeysCommand()
        : base("get-hotkeys", "List all layout hotkeys")
    {
        AddAlias("hk");
    }

    protected override string Execute(InvocationContext context)
    {
        var hotkeys = FancyZonesDataIO.ReadLayoutHotkeys();

        if (hotkeys.LayoutHotkeys == null || hotkeys.LayoutHotkeys.Count == 0)
        {
            return "No hotkeys configured.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Layout Hotkeys ===\n");
        sb.AppendLine("Press Win + Ctrl + Alt + <number> to switch layouts:\n");

        foreach (var hotkey in hotkeys.LayoutHotkeys.OrderBy(h => h.Key))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  [{hotkey.Key}] => {hotkey.LayoutId}");
        }

        return sb.ToString().TrimEnd();
    }
}
