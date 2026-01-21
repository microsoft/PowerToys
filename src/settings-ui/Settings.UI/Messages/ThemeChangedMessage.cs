// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Microsoft.PowerToys.Settings.UI.Messages
{
    /// <summary>
    /// Message sent when the application theme changes.
    /// </summary>
    public sealed class ThemeChangedMessage : ValueChangedMessage<string>
    {
        public ThemeChangedMessage(string themeName)
            : base(themeName)
        {
        }
    }
}
