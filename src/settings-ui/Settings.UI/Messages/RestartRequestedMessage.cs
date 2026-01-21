// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Messages
{
    /// <summary>
    /// Message sent to request a restart of PowerToys.
    /// </summary>
    public sealed class RestartRequestedMessage
    {
        public bool MaintainElevation { get; }

        public RestartRequestedMessage(bool maintainElevation = true)
        {
            MaintainElevation = maintainElevation;
        }
    }
}
