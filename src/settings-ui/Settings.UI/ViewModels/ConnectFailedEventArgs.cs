// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public sealed class ConnectFailedEventArgs : EventArgs
    {
        public ConnectFailedEventArgs(ConnectFailureTarget target, string message)
        {
            Target = target;
            Message = message;
        }

        public ConnectFailureTarget Target { get; }

        public string Message { get; }
    }
}
