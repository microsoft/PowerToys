// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Wox.Plugin
{
    public static class AllowedLanguage
    {
        public static string CSharp
        {
            get { return "CSHARP"; }
        }

        public static string Executable
        {
            get { return "EXECUTABLE"; }
        }

        public static bool IsAllowed(string language)
        {
            ArgumentNullException.ThrowIfNull(language);

            // Using InvariantCulture since this is a command line arg
            return string.Equals(language, CSharp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(language, Executable, StringComparison.OrdinalIgnoreCase);
        }
    }
}
