// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// One direction's value: the mode, and the push distance Custom uses. Off and Wall carry the
    /// distance too, so it comes back when the direction returns to Custom.
    /// </summary>
    public readonly record struct CursorBrakeEdgeValue(CursorBrakeEdgeMode Mode, int PushDistance);
}
