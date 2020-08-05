// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            return language.ToUpper() == CSharp.ToUpper()
                || language.ToUpper() == Executable.ToUpper();
        }
    }
}
