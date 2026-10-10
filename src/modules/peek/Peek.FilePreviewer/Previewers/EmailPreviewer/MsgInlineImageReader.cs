// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Peek.FilePreviewer.Previewers.EmailPreviewer
{
    internal static class MsgInlineImageReader
    {
        public static void Read(MsgStorage storage, EmailMessage message)
        {
            foreach (string storageName in storage.EnumerateStorages("__attach_version1.0_"))
            {
                using MsgStorage attachment = storage.OpenStorage(storageName);
                string contentId = attachment.ReadString("3712").Trim().Trim('<', '>');
                string contentType = attachment.ReadString("370E");
                byte[]? data = attachment.ReadStream("__substg1.0_37010102");
                if (!string.IsNullOrWhiteSpace(contentId) && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && data?.Length > 0)
                {
                    message.InlineImages[contentId] = new EmailInlineImage(contentType, data);
                }
            }
        }
    }
}
