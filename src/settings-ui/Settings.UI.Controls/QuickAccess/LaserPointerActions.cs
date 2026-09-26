// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    /// <summary>
    /// Names for the entries Laser Pointer contributes to Quick Access. It has more than
    /// one, so the action says which was pressed; kept as constants so the view model and
    /// the launcher cannot drift apart.
    /// </summary>
    public static class LaserPointerActions
    {
        public const string ShareWindow = "share";

        public const string StopSharing = "stop";
    }
}
