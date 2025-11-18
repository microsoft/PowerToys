// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

namespace WorkspacesEditor.Utils
{
    public static class StringUtils
    {
        public static string UpperCamelCaseToDashCase(this string str)
        {
            // If it's a single letter variable, leave it as it is
            return str.Length == 1
                ? str
                : string.Concat(str.Select((x, i) => i > 0 && char.IsUpper(x) ? "-" + x.ToString() : x.ToString())).ToLowerInvariant();
        }
    }
}
