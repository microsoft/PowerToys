// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Wox.Core.Resource
{
    public class Language
    {
        public Language(string code, string display)
        {
            LanguageCode = code;
            Display = display;
        }

        /// <summary>
        /// E.g. En or Zh-CN
        /// </summary>
        public string LanguageCode { get; set; }

        public string Display { get; set; }
    }
}
