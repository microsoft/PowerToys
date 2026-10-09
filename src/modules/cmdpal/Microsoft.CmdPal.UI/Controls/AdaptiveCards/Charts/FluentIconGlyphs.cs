// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>
/// Maps Adaptive Cards icon names, from the Fluent System Icons catalog, to the Segoe Fluent Icons
/// glyphs that draw the same symbols. Names without a matching glyph aren't listed.
/// </summary>
internal static class FluentIconGlyphs
{
    // The regular glyph, and the filled one where Segoe Fluent Icons has it.
    private static readonly FrozenDictionary<string, Glyph> Glyphs = new Dictionary<string, Glyph>(StringComparer.OrdinalIgnoreCase)
    {
        ["AccessTime"] = new('\uE917'),
        ["Add"] = new('\uE710'),
        ["Alert"] = new('\uEA8F'),
        ["ArrowClockwise"] = new('\uE72C'),
        ["ArrowDown"] = new('\uE74B'),
        ["ArrowDownload"] = new('\uE896'),
        ["ArrowLeft"] = new('\uE72B'),
        ["ArrowRedo"] = new('\uE7A6'),
        ["ArrowRight"] = new('\uE72A'),
        ["ArrowSync"] = new('\uE895'),
        ["ArrowUndo"] = new('\uE7A7'),
        ["ArrowUp"] = new('\uE74A'),
        ["ArrowUpload"] = new('\uE898'),
        ["Attach"] = new('\uE723'),
        ["Battery0"] = new('\uE850'),
        ["Battery1"] = new('\uE851'),
        ["Battery10"] = new('\uE83F'),
        ["Battery2"] = new('\uE852'),
        ["Battery3"] = new('\uE853'),
        ["Battery4"] = new('\uE854'),
        ["Battery5"] = new('\uE855'),
        ["Battery6"] = new('\uE856'),
        ["Battery7"] = new('\uE857'),
        ["Battery8"] = new('\uE858'),
        ["Battery9"] = new('\uE859'),
        ["BatteryCharge"] = new('\uEA93'),
        ["BatterySaver"] = new('\uEA95'),
        ["Bluetooth"] = new('\uE702'),
        ["Bug"] = new('\uEBE8'),
        ["Calculator"] = new('\uE8EF'),
        ["Calendar"] = new('\uE787', '\uEA89'),
        ["Camera"] = new('\uE722'),
        ["CellularData1"] = new('\uE870'),
        ["CellularData2"] = new('\uE86F'),
        ["CellularData3"] = new('\uE86E'),
        ["CellularData4"] = new('\uE86D'),
        ["CellularData5"] = new('\uE86C'),
        ["Chat"] = new('\uE8BD'),
        ["Checkmark"] = new('\uE73E'),
        ["CheckmarkCircle"] = new('\uE930', '\uEC61'),
        ["ChevronDown"] = new('\uE70D'),
        ["ChevronLeft"] = new('\uE76B'),
        ["ChevronRight"] = new('\uE76C'),
        ["ChevronUp"] = new('\uE70E'),
        ["ClipboardPaste"] = new('\uE77F'),
        ["Clock"] = new('\uE917'),
        ["Cloud"] = new('\uE753'),
        ["Code"] = new('\uE943'),
        ["Comment"] = new('\uE90A'),
        ["Copy"] = new('\uE8C8'),
        ["Cut"] = new('\uE8C6'),
        ["DataArea"] = new('\uE9D2'),
        ["DataPie"] = new('\uEB05'),
        ["Delete"] = new('\uE74D'),
        ["Desktop"] = new('\uE7F4'),
        ["DeveloperBoard"] = new('\uE950'),
        ["Dismiss"] = new('\uE711'),
        ["DismissCircle"] = new('\uEA39', '\uEB90'),
        ["Document"] = new('\uE8A5'),
        ["Edit"] = new('\uE70F'),
        ["Emoji"] = new('\uE76E'),
        ["ErrorCircle"] = new('\uE783'),
        ["Eye"] = new('\uE890'),
        ["EyeOff"] = new('\uED1A'),
        ["Filter"] = new('\uE71C'),
        ["Flag"] = new('\uE7C1'),
        ["Flash"] = new('\uE945'),
        ["Folder"] = new('\uE8B7', '\uE8D5'),
        ["FullScreenMaximize"] = new('\uE740'),
        ["Games"] = new('\uE7FC'),
        ["Gauge"] = new('\uF42F'),
        ["Globe"] = new('\uE774'),
        ["HardDrive"] = new('\uEDA2'),
        ["Heart"] = new('\uEB51', '\uEB52'),
        ["HeartPulse"] = new('\uE95E'),
        ["Home"] = new('\uE80F', '\uEA8A'),
        ["Image"] = new('\uE8B9'),
        ["Info"] = new('\uE946', '\uF167'),
        ["Laptop"] = new('\uE7F8'),
        ["Lightbulb"] = new('\uEA80'),
        ["Link"] = new('\uE71B'),
        ["Location"] = new('\uECAF'),
        ["LockClosed"] = new('\uE72E'),
        ["LockOpen"] = new('\uE785'),
        ["Mail"] = new('\uE715', '\uE8A8'),
        ["Memory"] = new('\uEEA0'),
        ["Mic"] = new('\uE720'),
        ["MoreHorizontal"] = new('\uE712'),
        ["NetworkAdapter"] = new('\uE839'),
        ["Open"] = new('\uE8A7'),
        ["Pause"] = new('\uE769'),
        ["People"] = new('\uE716'),
        ["Person"] = new('\uE77B'),
        ["Phone"] = new('\uE8EA'),
        ["Pin"] = new('\uE718', '\uE841'),
        ["Play"] = new('\uE768', '\uF5B0'),
        ["Power"] = new('\uE7E8'),
        ["Print"] = new('\uE749'),
        ["PulseSquare"] = new('\uE9D9'),
        ["QuestionCircle"] = new('\uE9CE'),
        ["Ram"] = new('\uEEA0'),
        ["Ruler"] = new('\uED5E'),
        ["Save"] = new('\uE74E'),
        ["Search"] = new('\uE721'),
        ["Send"] = new('\uE724'),
        ["Settings"] = new('\uE713'),
        ["Share"] = new('\uE72D'),
        ["Shield"] = new('\uEA18'),
        ["Speaker2"] = new('\uE767'),
        ["Star"] = new('\uE734', '\uE735'),
        ["Stop"] = new('\uE71A'),
        ["Storage"] = new('\uEDA2'),
        ["Subtract"] = new('\uE738'),
        ["Tablet"] = new('\uE70A'),
        ["Tag"] = new('\uE8EC'),
        ["ThumbDislike"] = new('\uE8E0', '\uF3C0'),
        ["ThumbLike"] = new('\uE8E1', '\uF3BF'),
        ["Timer"] = new('\uE916'),
        ["TopSpeed"] = new('\uEC4A'),
        ["Tv"] = new('\uE7F4'),
        ["UsbStick"] = new('\uE88E'),
        ["Video"] = new('\uE714', '\uEA0C'),
        ["Warning"] = new('\uE7BA', '\uF736'),
        ["Wifi1"] = new('\uE701'),
        ["Wifi2"] = new('\uE874'),
        ["Wifi3"] = new('\uE873'),
        ["Wifi4"] = new('\uE872'),
        ["Wrench"] = new('\uE90F'),
        ["ZoomIn"] = new('\uE8A3'),
        ["ZoomOut"] = new('\uE71F'),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets every icon name that has a glyph.</summary>
    public static IEnumerable<string> Names => Glyphs.Keys;

    /// <summary>Finds the glyph for an icon name, ignoring case, using the filled glyph when asked for and available.</summary>
    public static bool TryGetGlyph(string? name, bool filled, [NotNullWhen(true)] out string? glyph)
    {
        if (name is not null && Glyphs.TryGetValue(name.Trim(), out var entry))
        {
            glyph = (filled && entry.Filled is char solid ? solid : entry.Regular).ToString();
            return true;
        }

        glyph = null;
        return false;
    }

    /// <summary>Reads a Badge <c>icon</c> reference, <c>name[,regular|filled]</c>; regular is the default.</summary>
    public static (string Name, bool Filled) ParseReference(string reference)
    {
        var comma = reference.IndexOf(',');
        return comma < 0
            ? (reference.Trim(), false)
            : (reference[..comma].Trim(), reference[(comma + 1)..].Trim().Equals("filled", StringComparison.OrdinalIgnoreCase));
    }

    private readonly record struct Glyph(char Regular, char? Filled = null);
}
