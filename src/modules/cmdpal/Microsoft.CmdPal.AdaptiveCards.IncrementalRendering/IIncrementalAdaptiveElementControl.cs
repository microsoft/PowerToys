// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

/// <summary>
/// Implemented by the control that a custom element renderer returns when that control can
/// absorb a newer version of its element without the card being replaced.
/// </summary>
/// <remarks>
/// The element type must also be registered with <see cref="IncrementalPatchableElements"/>.
/// The updater only patches the properties that registration reports as patchable; every
/// other property change still replaces the complete card.
/// </remarks>
public interface IIncrementalAdaptiveElementControl
{
    /// <summary>
    /// Gets a deterministic snapshot of the patchable state. Two controls that report the same
    /// state must render the same content.
    /// </summary>
    string IncrementalState { get; }

    /// <summary>
    /// Returns whether the state of <paramref name="candidate"/> can be applied to this control.
    /// The updater checks every change before it applies any of them.
    /// </summary>
    bool CanApplyIncrementalState(IIncrementalAdaptiveElementControl candidate);

    /// <summary>Applies the state of <paramref name="candidate"/> to this control.</summary>
    void ApplyIncrementalState(IIncrementalAdaptiveElementControl candidate);
}
