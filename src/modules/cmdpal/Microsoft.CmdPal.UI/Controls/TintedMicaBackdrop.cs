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
/// A tinted <see cref="MicaController"/> exposed as a <see cref="SystemBackdrop"/>
/// so it can be hosted by <see cref="Microsoft.UI.Xaml.Controls.SystemBackdropElement"/>.
/// </summary>
internal sealed partial class TintedMicaBackdrop : TintedControllerBackdrop, IDisposable
{
    private MicaController? _controller;

    public MicaKind Kind { get; init; } = MicaKind.Base;

    protected override void AttachController(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        if (!MicaController.IsSupported())
        {
            return;
        }

        _controller = new MicaController { Kind = Kind };

        // Only set tint properties when colorization is active.
        // Otherwise let the system handle light/dark theme defaults automatically.
        if (ApplyTint)
        {
            _controller.TintColor = TintColor;
            _controller.TintOpacity = TintOpacity;
            _controller.FallbackColor = FallbackColor;
            _controller.LuminosityOpacity = LuminosityOpacity;
        }

        _controller.AddSystemBackdropTarget(target);
        _controller.SetSystemBackdropConfiguration(Configuration);
    }

    protected override void DetachController(ICompositionSupportsSystemBackdrop target)
    {
        if (_controller is not null)
        {
            _controller.RemoveSystemBackdropTarget(target);
            _controller.Dispose();
            _controller = null;
        }
    }

    public void Dispose()
    {
        _controller?.Dispose();
        _controller = null;
    }
}
