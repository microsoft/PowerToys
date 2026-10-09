// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public record PowerAccentLanguageModel(string LanguageCode, string LanguageResourceID, string GroupResourceID)
    {
        public string Language { get; set; }
    }
}
