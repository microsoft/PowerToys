// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Microsoft.PowerToys.Settings.UI.Messages
{
    /// <summary>
    /// Message sent when a module's enabled state changes.
    /// </summary>
    public sealed class ModuleEnabledChangedMessage : ValueChangedMessage<ModuleEnabledChangedMessage.ModuleStateData>
    {
        public ModuleEnabledChangedMessage(string moduleName, bool isEnabled)
            : base(new ModuleStateData(moduleName, isEnabled))
        {
        }

        public record ModuleStateData(string ModuleName, bool IsEnabled);
    }
}
