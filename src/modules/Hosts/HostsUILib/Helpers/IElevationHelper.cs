// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace HostsUILib.Helpers
{
    public interface IElevationHelper
    {
        bool IsElevated { get; }

        /// <summary>
        /// Gets a value indicating whether the host can restart the editor elevated.
        /// When true and not elevated, the editor offers a "Restart as administrator" action.
        /// </summary>
        bool CanRequestElevation => false;

        void RequestElevation()
        {
        }
    }
}
