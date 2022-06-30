// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Community.PowerToys.Run.Plugin.Translate.Utils
{
    /// <summary>
    /// Basic language object
    /// </summary>
    public class Language
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Code { get; set; }

        public Language(string name, string displayName, string code)
        {
            Name = name;
            DisplayName = displayName;
            Code = code;
        }
    }
}
