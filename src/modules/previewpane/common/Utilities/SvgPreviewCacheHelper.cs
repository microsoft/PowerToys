// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
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
                cacheKeyBuilder.Append(input ?? string.Empty);
                cacheKeyBuilder.Append('\n');
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKeyBuilder.ToString())));
        }

        internal static string GetCacheFilePath(string cacheRootFolder, string cacheKey)
        {
            Directory.CreateDirectory(cacheRootFolder);
            return Path.Combine(cacheRootFolder, $"{cacheKey}.html");
        }
    }
}