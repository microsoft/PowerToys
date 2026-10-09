// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.ObjectModel.WinUI3;
using AdaptiveCards.Rendering.WinUI3;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>
/// Draws an element's <c>fallback</c> for an element that can't draw itself, such as an icon
/// without a glyph.
/// </summary>
/// <remarks>
/// The WinUI 3 renderer is meant to do this when an element renderer fails with its fallback
/// error, but <c>XamlBuilder::RenderAsUIElement</c> renders the fallback and then returns an empty
/// result, so the fallback never shows. The element renderer returns the fallback's control as
/// its own instead, and the renderer tags it with the original element.
/// </remarks>
internal static class AdaptiveFallbackRenderer
{
    // E_PERFORM_FALLBACK: an element renderer's request to draw the element's fallback.
    private const int PerformFallbackHResult = unchecked((int)0x8ADA1000);

    /// <summary>Renders the fallback of <paramref name="element"/>, following nested fallbacks.</summary>
    /// <returns>The fallback's control, or null when the element has no fallback or drops.</returns>
    public static UIElement? Render(IAdaptiveCardElement element, AdaptiveRenderContext context, AdaptiveRenderArgs renderArgs)
    {
        var current = element;
        while (current.FallbackType == FallbackType.Content && current.FallbackContent is { } fallback)
        {
            if (context.ElementRenderers.Get(fallback.ElementTypeString) is { } renderer)
            {
                try
                {
                    return renderer.Render(fallback, context, renderArgs);
                }
                catch (Exception ex) when (ex.HResult == PerformFallbackHResult)
                {
                    // The fallback can't render either, so its own fallback is next.
                }
            }

            current = fallback;
        }

        return null;
    }
}
