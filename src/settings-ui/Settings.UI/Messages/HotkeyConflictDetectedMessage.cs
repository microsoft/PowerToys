// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Microsoft.PowerToys.Settings.UI.Messages
{
    /// <summary>
    /// Message sent when hotkey conflicts are detected.
    /// </summary>
    public sealed class HotkeyConflictDetectedMessage : ValueChangedMessage<HotkeyConflictDetectedMessage.ConflictData>
    {
        public HotkeyConflictDetectedMessage(string moduleName, string hotkeyDescription, bool isSystemConflict)
            : base(new ConflictData(moduleName, hotkeyDescription, isSystemConflict))
        {
        }

        public record ConflictData(string ModuleName, string HotkeyDescription, bool IsSystemConflict);
    }
}
