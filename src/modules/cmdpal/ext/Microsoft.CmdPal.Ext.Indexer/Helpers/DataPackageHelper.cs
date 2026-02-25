// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

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
    public static DataPackage? CreateDataPackageForPath(ICommandItem listItem, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        // Capture now; don't rely on listItem still being valid later.
        var title = listItem.Title;
        var description = listItem.Subtitle;
        var capturedPath = path;

        var dataPackage = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy,
            Properties =
            {
                Title = title,
                Description = description,
            },
        };

        // Cheap + immediate.
        dataPackage.SetText(capturedPath);

        // Expensive + only computed if the consumer asks for StorageItems.
        dataPackage.SetDataProvider(StandardDataFormats.StorageItems, async void (request) =>
        {
            var deferral = request.GetDeferral();
            try
            {
                var items = await TryGetStorageItemAsync(capturedPath).ConfigureAwait(false);
                if (items is not null)
                {
                    request.SetData(items);
                }

                // If null: just don't provide StorageItems. Text still works.
            }
            catch
            {
                // Swallow: better to provide partial data (text) than fail the whole package.
            }
            finally
            {
                deferral.Complete();
            }
        });

        return dataPackage;
    }

    private static async Task<IStorageItem[]?> TryGetStorageItemAsync(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var file = await StorageFile.GetFileFromPathAsync(filePath);
                return [file];
            }

            if (Directory.Exists(filePath))
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(filePath);
                return [folder];
            }

            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }
}
