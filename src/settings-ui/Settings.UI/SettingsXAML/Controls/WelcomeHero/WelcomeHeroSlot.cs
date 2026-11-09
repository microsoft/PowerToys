// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    /// <summary>
    /// A single tile position in the Welcome hero constellation.
    /// </summary>
    /// <param name="Rank">Order in which slots are handed out to icons (0 = first).</param>
    /// <param name="Column">Lattice column (0 = the logo column, negative = left).</param>
    /// <param name="Row">Lattice row within the column (0 = lowest).</param>
    /// <param name="Offset">Center of the tile relative to the logo center, in DIPs.</param>
    /// <param name="Distance">Distance from the logo center, in DIPs.</param>
    /// <param name="Opacity">Resting opacity; tiles fade out towards the top edge of the hero.</param>
    public readonly record struct WelcomeHeroSlot(int Rank, int Column, int Row, Vector2 Offset, float Distance, float Opacity);
}
