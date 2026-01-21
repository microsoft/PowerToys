// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Microsoft.PowerToys.Settings.UI.Messages
{
    /// <summary>
    /// Message sent when backup/restore operations complete.
    /// </summary>
    public sealed class BackupRestoreCompletedMessage : ValueChangedMessage<BackupRestoreCompletedMessage.ResultData>
    {
        public BackupRestoreCompletedMessage(bool success, string message, bool isBackup)
            : base(new ResultData(success, message, isBackup))
        {
        }

        public record ResultData(bool Success, string Message, bool IsBackup);
    }
}
