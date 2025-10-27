// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

public class DockSettings
{
    // public bool ShowAppTitles { get; set; }

    // public bool ShowSearchButton { get; set; }
    public DockSide Side { get; set; } = DockSide.Top;

    public DockSize DockSize { get; set; } = DockSize.Small;

    public DockBackdrop Backdrop { get; set; } = DockBackdrop.Acrylic;
}

public enum DockSide
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3,
}

public enum DockSize
{
    Small,
    Medium,
    Large,
}

public enum DockBackdrop
{
    Mica,
    Transparent,
    Acrylic,
}
