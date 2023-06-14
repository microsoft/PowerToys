// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.Common.Helpers
{
    public static class SizeHelper
    {
        public static string GetHumanSize(long size)
        {
            return size switch
            {
                < 1024 => $"{size} bytes",
                < 1024 * 1024 => $"{size / 1024.0:0.00} KB",
                < 1024 * 1024 * 1024 => $"{size / 1024.0 / 1024.0:0.00} MB",
                < 1024L * 1024 * 1024 * 1024 => $"{size / 1024.0 / 1024.0 / 1024.0:0.00} GB",
                _ => $"{size / 1024.0 / 1024.0 / 1024.0 / 1024.0:0.00} TB",
            };
        }
    }
}
