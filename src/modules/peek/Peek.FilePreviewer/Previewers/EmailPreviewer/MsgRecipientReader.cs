// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Previewers.EmailPreviewer
{
    internal static class MsgRecipientReader
    {
        public static void Read(MsgStorage storage, EmailMessage message)
        {
            foreach (string storageName in storage.EnumerateStorages("__recip_version1.0_"))
            {
                using MsgStorage recipient = storage.OpenStorage(storageName);
                string name = recipient.ReadString("3001");
                string address = MsgReader.FirstNonEmpty(recipient.ReadString("39FE"), recipient.ReadString("3003"));
                string display = MsgReader.FormatAddress(name, address);
                int type = recipient.ReadInt32("0C15");
                (type == 2 ? message.Cc : type == 3 ? message.Bcc : message.To).Add(display);
            }
        }
    }
}
