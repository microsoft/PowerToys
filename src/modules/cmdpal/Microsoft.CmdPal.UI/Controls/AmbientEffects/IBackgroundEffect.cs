// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.UI.Composition;

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects;

/// <summary>
/// Defines a GPU-accelerated background visual effect rendered via the Composition API.
/// Each implementation builds its own composition tree inside the provided <see cref="ContainerVisual"/>.
/// </summary>
internal interface IBackgroundEffect : IDisposable
{
    /// <summary>
    /// Called once to set up the composition visual tree.
    /// </summary>
    /// <param name="compositor">The compositor to use for creating visuals and animations.</param>
    /// <param name="rootVisual">The container visual to attach child visuals to.</param>
    /// <param name="size">The initial size of the effect area.</param>
    void Initialize(Compositor compositor, ContainerVisual rootVisual, Vector2 size);

    /// <summary>
    /// Starts or resumes all animations.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops all animations (e.g. when the window is hidden).
    /// </summary>
    void Stop();

    /// <summary>
    /// Called when the host element is resized.
    /// </summary>
    void Resize(Vector2 newSize);
}
