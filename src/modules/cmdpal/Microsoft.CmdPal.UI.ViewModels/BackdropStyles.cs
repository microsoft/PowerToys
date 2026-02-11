// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Central registry of backdrop style configurations.
/// </summary>
public static class BackdropStyles
{
    private static readonly Dictionary<BackdropStyle, BackdropStyleConfig> Configs = new()
    {
        [BackdropStyle.Acrylic] = new()
        {
            ControllerKind = BackdropControllerKind.Acrylic,
            BaseTintOpacity = 0.5f,
            BaseLuminosityOpacity = 0.9f,
            PreviewBrush = PreviewBrushKind.Acrylic,
        },
        [BackdropStyle.AcrylicThin] = new()
        {
            ControllerKind = BackdropControllerKind.AcrylicThin,
            BaseTintOpacity = 0.0f,
            BaseLuminosityOpacity = 0.85f,
            PreviewBrush = PreviewBrushKind.Acrylic,
        },
        [BackdropStyle.Mica] = new()
        {
            ControllerKind = BackdropControllerKind.Mica,
            BaseTintOpacity = 0.0f,
            BaseLuminosityOpacity = 1.0f,
            PreviewBrush = PreviewBrushKind.Solid,
            FixedOpacity = 0.96f,
            SupportsOpacity = false,
        },
        [BackdropStyle.MicaAlt] = new()
        {
            ControllerKind = BackdropControllerKind.MicaAlt,
            BaseTintOpacity = 0.0f,
            BaseLuminosityOpacity = 1.0f,
            PreviewBrush = PreviewBrushKind.Solid,
            FixedOpacity = 0.98f,
            SupportsOpacity = false,
        },
        [BackdropStyle.Clear] = new()
        {
            ControllerKind = BackdropControllerKind.Solid,
            BaseTintOpacity = 1.0f,
            BaseLuminosityOpacity = 1.0f,
            PreviewBrush = PreviewBrushKind.Solid,
        },
    };

    /// <summary>
    /// Gets the configuration for the specified backdrop style.
    /// </summary>
    public static BackdropStyleConfig Get(BackdropStyle style) =>
        Configs.TryGetValue(style, out var config) ? config : Configs[BackdropStyle.Acrylic];

    /// <summary>
    /// Gets all registered backdrop styles.
    /// </summary>
    public static IEnumerable<BackdropStyle> All => Configs.Keys;
}
