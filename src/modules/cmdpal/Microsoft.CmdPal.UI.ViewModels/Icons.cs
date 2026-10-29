// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public static class Icons
{
    public static IconInfo PinIcon => new(Glyphs.Pin);

    public static IconInfo UnpinIcon => new(Glyphs.Unpin);

    public static IconInfo MoveUpIcon => new(Glyphs.MoveUp);

    public static IconInfo MoveDownIcon => new(Glyphs.MoveDown);

    public static IconInfo MoveToTopIcon => new(Glyphs.MoveToTop);

    public static IconInfo SettingsIcon => new(Glyphs.Settings);

    public static IconInfo EditIcon => new(Glyphs.Edit);

    public static IconInfo DeleteIcon => new(Glyphs.Delete);
}
