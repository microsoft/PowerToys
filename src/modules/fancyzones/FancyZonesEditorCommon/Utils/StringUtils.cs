// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

namespace FancyZonesEditorCommon.Utils
{
    public static class StringUtils
    {
        public static string UpperCamelCaseToDashCase(this string str)
        {
            // If it's single letter variable, leave it as it is
            if (str.Length == 1)
            {
                return str;
            }

            return string.Concat(str.Select((x, i) => i > 0 && char.IsUpper(x) ? "-" + x.ToString() : x.ToString())).ToLowerInvariant();
        }
    }
}
