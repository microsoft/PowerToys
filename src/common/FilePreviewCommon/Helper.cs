// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.PowerToys.FilePreviewCommon
{
    public static class Helper
    {
        public static void CleanupTempDir(string folder)
        {
            try
            {
                var dir = new DirectoryInfo(folder);
                foreach (var file in dir.EnumerateFiles("*.html"))
                {
                    file.Delete();
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
