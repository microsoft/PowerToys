// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// Base class for tinted backdrops that wrap a controller from
/// <see cref="Microsoft.UI.Composition.SystemBackdrops"/> so they can be applied
/// to a single control via <see cref="Microsoft.UI.Xaml.Controls.SystemBackdropElement"/>.
/// </summary>
/// <remarks>
/// The stock <see cref="MicaBackdrop"/> / <see cref="DesktopAcrylicBackdrop"/> classes
/// don't expose tint color / opacity / luminosity customization. This base type plugs
/// the lower-level controllers into the new <see cref="Microsoft.UI.Xaml.Controls.SystemBackdropElement"/>
/// extensibility surface so we can keep all of CmdPal's theme-driven tinting.
/// </remarks>
internal abstract partial class TintedControllerBackdrop : SystemBackdrop
{
    private SystemBackdropConfiguration? _config;

    public Color TintColor { get; init; }

    public float TintOpacity { get; init; }

    public Color FallbackColor { get; init; }

    public float LuminosityOpacity { get; init; }

    /// <summary>
    /// Gets a value indicating whether tint properties should be applied. Mica without
    /// colorization wants the system defaults; in that case set this to false.
    /// </summary>
    public bool ApplyTint { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the host window is currently activated. The
    /// system uses this to decide between the active and inactive backdrop appearance.
    /// </summary>
    public bool IsInputActive
    {
        get => _config?.IsInputActive ?? true;
        set
        {
            if (_config is not null)
            {
                _config.IsInputActive = value;
            }
        }
    }

    protected SystemBackdropConfiguration? Configuration => _config;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        _config = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = xamlRoot.Content is FrameworkElement fe
                ? ToBackdropTheme(fe.ActualTheme)
                : SystemBackdropTheme.Default,
        };
        AttachController(connectedTarget, xamlRoot);
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        DetachController(disconnectedTarget);
        _config = null;
        base.OnTargetDisconnected(disconnectedTarget);
    }

    protected abstract void AttachController(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot);

    protected abstract void DetachController(ICompositionSupportsSystemBackdrop target);

    private static SystemBackdropTheme ToBackdropTheme(ElementTheme theme) => theme switch
    {
        ElementTheme.Dark => SystemBackdropTheme.Dark,
        ElementTheme.Light => SystemBackdropTheme.Light,
        _ => SystemBackdropTheme.Default,
    };
}
