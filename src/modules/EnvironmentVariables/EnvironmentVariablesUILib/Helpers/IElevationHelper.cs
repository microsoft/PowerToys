// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace EnvironmentVariablesUILib.Helpers
{
    public interface IElevationHelper
    {
        bool IsElevated { get; }

        /// <summary>
        /// Gets a value indicating whether the host can restart elevated so system variables can be edited.
        /// </summary>
        bool CanRequestElevation => false;

        /// <summary>
        /// Asks the host to restart elevated. Called only when <see cref="CanRequestElevation"/> is <see langword="true"/>.
        /// </summary>
        void RequestElevation()
        {
        }
    }
}
