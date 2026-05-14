// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Wox.Plugin
{
    public static class PathNormalization
    {
        private const string UncPrefix = @"\\?\UNC\";
        private const string ExtendedPrefix = @"\\?\";

        public static string NormalizePath(string path)
        {
            if (path.StartsWith(UncPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return @"\\" + path.Substring(UncPrefix.Length);
            }

            if (path.StartsWith(ExtendedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return path.Substring(ExtendedPrefix.Length);
            }

            return path;
        }
    }
}
