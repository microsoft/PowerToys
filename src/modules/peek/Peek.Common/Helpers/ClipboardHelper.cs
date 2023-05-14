// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Peek.Common.Helpers
{
    public static class ClipboardHelper
    {
        public static void SaveToClipboard(IStorageItem? storageItem)
        {
            if (storageItem == null)
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetStorageItems(new IStorageItem[1] { storageItem }, false);

            if (storageItem is StorageFile storageFile)
            {
                RandomAccessStreamReference imageStreamRef = RandomAccessStreamReference.CreateFromFile(storageFile);
                dataPackage.Properties.Thumbnail = imageStreamRef;
                dataPackage.SetBitmap(imageStreamRef);
            }

            Clipboard.SetContent(dataPackage);
        }
    }
}
