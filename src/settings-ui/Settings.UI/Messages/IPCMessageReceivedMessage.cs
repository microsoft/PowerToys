// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Microsoft.PowerToys.Settings.UI.Messages
{
    /// <summary>
    /// Message sent when an IPC message is received from the PowerToys runner.
    /// </summary>
    public sealed class IPCMessageReceivedMessage : ValueChangedMessage<string>
    {
        public IPCMessageReceivedMessage(string message)
            : base(message)
        {
        }
    }
}
