// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Globalization;

namespace PowerOCR.Helpers
{
    internal static class LanguageHelper
    {
        public static bool IsLanguageSpaceJoining(Language selectedLanguage)
        {
            if (selectedLanguage.LanguageTag.StartsWith("zh", StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }
            else if (selectedLanguage.LanguageTag.Equals("ja", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
    }
}
