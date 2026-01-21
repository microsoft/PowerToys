// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.Messages
{
    /// <summary>
    /// Message sent when general settings are updated.
    /// </summary>
    public sealed class GeneralSettingsUpdatedMessage : ValueChangedMessage<GeneralSettings>
    {
        public GeneralSettingsUpdatedMessage(GeneralSettings settings)
            : base(settings)
        {
        }
    }
}
