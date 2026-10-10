// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;

/// <summary>
/// Implemented by a custom element's control when it can take a newer version of its element
/// in place. Register the element type with <see cref="IncrementalPatchableElements"/>.
/// </summary>
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
