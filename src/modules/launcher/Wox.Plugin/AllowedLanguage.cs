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
            if (language == null)
            {
                throw new ArgumentNullException(nameof(language));
            }

            // Using InvariantCulture since this is a command line arg
            return language.ToUpper(CultureInfo.InvariantCulture) == CSharp.ToUpper(CultureInfo.InvariantCulture)
                || language.ToUpper(CultureInfo.InvariantCulture) == Executable.ToUpper(CultureInfo.InvariantCulture);
        }
    }
}
