// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills;

/// <summary>The parsed form of an element that Command Palette renders natively.</summary>
internal interface IAdaptiveVisualModel
{
    /// <summary>Gets the canonical element JSON, which identifies everything the control draws.</summary>
    string IncrementalState { get; }

    /// <summary>Gets whether the element draws itself; if not, its <c>fallback</c> is drawn instead.</summary>
    bool RendersItself => true;
}
