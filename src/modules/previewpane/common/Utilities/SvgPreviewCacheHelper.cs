// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Common.Utilities
{
    internal static class SvgPreviewCacheHelper
    {
        internal static string BuildCacheKey(params string[] cacheInputs)
        {
            var cacheKeyBuilder = new StringBuilder();

            foreach (var input in cacheInputs)
            {
                string value = input ?? string.Empty;
                cacheKeyBuilder.Append(value.Length).Append(':').Append(value);
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKeyBuilder.ToString())));
        }

        internal static string GetCacheFilePath(string cacheRootFolder, string cacheKey)
        {
            return Path.Combine(cacheRootFolder, $"{cacheKey}.html");
        }

        internal static bool WriteCacheFileAtomic(string cacheFilePath, string content)
        {
            try
            {
                var directory = Path.GetDirectoryName(cacheFilePath);
                Directory.CreateDirectory(directory);
                
                string tempFile = Path.Combine(directory, Path.GetRandomFileName());
                File.WriteAllText(tempFile, content);
                
                File.Move(tempFile, cacheFilePath, overwrite: true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static void ManageCacheSize(string cacheFolder, int maxEntries = 30)
        {
            try
            {
                if (!Directory.Exists(cacheFolder))
                {
                    return;
                }

                var files = new DirectoryInfo(cacheFolder).GetFiles("*.html")
                                                          .OrderByDescending(f => f.LastWriteTimeUtc)
                                                          .ToList();

                if (files.Count > maxEntries)
                {
                    foreach (var file in files.Skip(maxEntries))
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}