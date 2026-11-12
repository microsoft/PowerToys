// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Common.UI.Controls;

/// <summary>
/// A show or hide transition a surface (e.g. <see cref="TransientSurface"/>)
/// plays when it is shown or hidden. The directional values describe an edge —
/// interpreted as <em>in from</em> that edge on show and <em>out toward</em> it
/// on hide — while <see cref="None"/> and <see cref="Pop"/> are non-directional.
/// </summary>
public enum Transition
{
    /// <summary>No animation; the surface appears and disappears instantly.</summary>
    None,

    /// <summary>Slide from the left edge (in from on show, out toward on hide).</summary>
    Left,

    /// <summary>Slide from the top edge (in from on show, out toward on hide).</summary>
    Top,

    /// <summary>Slide from the right edge (in from on show, out toward on hide).</summary>
    Right,

    /// <summary>Slide from the bottom edge (in from on show, out toward on hide).</summary>
    Bottom,

    /// <summary>
    /// A subtle "pop": a quick fade combined with a small scale between 96% and
    /// 100% from the surface's center. Stays in place — no slide.
    /// </summary>
    Pop,
}
