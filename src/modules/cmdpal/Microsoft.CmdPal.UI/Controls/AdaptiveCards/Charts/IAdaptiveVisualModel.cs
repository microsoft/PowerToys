// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>The parsed form of an element that Command Palette renders natively.</summary>
internal interface IAdaptiveVisualModel
{
    /// <summary>
    /// Gets the canonical element JSON. It identifies everything the control renders, so the
    /// incremental updater can tell when a control needs new state.
    /// </summary>
    string IncrementalState { get; }

    /// <summary>
    /// Gets whether the element draws itself. When it doesn't, as for an icon name without a
    /// glyph, the renderer draws the element's <c>fallback</c> instead.
    /// </summary>
    bool RendersItself => true;
}
