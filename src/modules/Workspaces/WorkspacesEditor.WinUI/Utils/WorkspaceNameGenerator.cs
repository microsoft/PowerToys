// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace WorkspacesEditor.Utils
{
    internal static class WorkspaceNameGenerator
    {
        internal static string Create(string defaultNamePrefix, IEnumerable<string> existingNames)
        {
            ArgumentNullException.ThrowIfNull(defaultNamePrefix);
            ArgumentNullException.ThrowIfNull(existingNames);

            string numberedPrefix = defaultNamePrefix + " ";
            BigInteger greatestIndex = BigInteger.Zero;
            foreach (string name in existingNames)
            {
                if (name != null && name.Length > numberedPrefix.Length &&
                    name.StartsWith(numberedPrefix, StringComparison.CurrentCulture) &&
                    BigInteger.TryParse(name.AsSpan(numberedPrefix.Length), NumberStyles.Integer, CultureInfo.CurrentCulture, out BigInteger index) &&
                    index > greatestIndex)
                {
                    greatestIndex = index;
                }
            }

            return numberedPrefix + (greatestIndex + 1).ToString(CultureInfo.CurrentCulture);
        }
    }
}
