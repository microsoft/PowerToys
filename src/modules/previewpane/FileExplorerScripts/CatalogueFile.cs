// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FileExplorerScripts;

internal static class CatalogueFile
{
    internal static async Task PublishAsync(string temporary, string destination, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (File.Exists(destination))
                {
                    // Replacement keeps open, delete-sharing readers on their original snapshot.
                    File.Replace(temporary, destination, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(temporary, destination);
                }

                return;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException &&
                (exception.HResult & 0xffff) is 5 or 32 or 33 &&
                attempt < 9)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
