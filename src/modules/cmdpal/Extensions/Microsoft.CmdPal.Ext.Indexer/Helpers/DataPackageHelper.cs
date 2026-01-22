// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using File = System.IO.File;

namespace Microsoft.CmdPal.Ext.Indexer.Helpers;

internal static class DataPackageHelper
{
    public static DataPackage CreateDataPackageForPath(ICommandItem listItem, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText(path);
        _ = dataPackage.TrySetStorageItemsAsync(path);
        dataPackage.Properties.Title = listItem.Title;
        dataPackage.Properties.Description = listItem.Subtitle;
        dataPackage.RequestedOperation = DataPackageOperation.Copy;
        return dataPackage;
    }

    public static async Task<bool> TrySetStorageItemsAsync(this DataPackage dataPackage, string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var file = await StorageFile.GetFileFromPathAsync(filePath);
                dataPackage.SetStorageItems([file]);
                return true;
            }

            if (Directory.Exists(filePath))
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(filePath);
                dataPackage.SetStorageItems([folder]);
                return true;
            }

            // nothing there
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // Access denied – skip or report, but don't crash
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
