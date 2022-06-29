// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Run.Plugin.Translate.Utils
{
    public static class LanguageHelper
    {
        private static IEnumerable<Language> ApplyFilter(this IEnumerable<Language> targetList, string filterText)
        {
            return targetList.Where(x =>
            {
                if (x.Code.Equals(filterText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (x.DisplayName.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (x.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            }).OrderByDescending(c => c.Code.Equals(filterText, StringComparison.OrdinalIgnoreCase));
        }

        public static Language[] FindLanguages(string searchText = "")
        {
            var languages = LanguageDictionary.GetLanguagesList().AsEnumerable();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                languages = languages.ApplyFilter(searchText);
            }

            return languages.ToArray();
        }
    }
}
