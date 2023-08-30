// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.FilePreviewCommon
{
    public static class Helper
    {
        public static Task<bool> CleanupTempDirAsync(string folder)
        {
            return Task.Run(() =>
            {
                return CleanupTempDir(folder);
            });
        }

        public static bool CleanupTempDir(string folder)
        {
            try
            {
                var dir = new DirectoryInfo(folder);
                foreach (var file in dir.EnumerateFiles("*.html"))
                {
                    file.Delete();
                }

                return true;
            }
            catch (Exception)
            {
            }

            return false;
        }
    }
}
