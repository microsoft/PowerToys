// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// What one direction across an edge does. Custom, Off and Wall are the values the <c>mode</c>
    /// of an <c>edges</c> entry stores, numbered as the module's EdgeMode. Default is the absence of
    /// an entry and has no value in the file.
    /// </summary>
    public enum CursorBrakeEdgeMode
    {
        Default = -1,
        Custom = 0,
        Off = 1,
        Wall = 2,
    }
}
