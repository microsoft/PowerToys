// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Microsoft.PowerToys.Settings.UI.Messages
{
    /// <summary>
    /// Message sent to request navigation to a specific page.
    /// </summary>
    public sealed class NavigateToPageMessage : ValueChangedMessage<System.Type>
    {
        public NavigateToPageMessage(System.Type pageType)
            : base(pageType)
        {
        }
    }
}
