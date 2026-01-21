// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Microsoft.PowerToys.Settings.UI.Messages
{
    /// <summary>
    /// Message sent when settings are saved.
    /// </summary>
    public sealed class SettingsSavedMessage : ValueChangedMessage<SettingsSavedMessage.SettingsSaveData>
    {
        public SettingsSavedMessage(string moduleName)
            : base(new SettingsSaveData(moduleName))
        {
        }

        public record SettingsSaveData(string ModuleName);
    }
}
