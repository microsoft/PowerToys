// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public static class PasteAIUsageExtensions
    {
        public static string ToConfigString(this PasteAIUsage usage)
        {
            return usage switch
            {
                PasteAIUsage.ChatCompletion => "ChatCompletion",
                PasteAIUsage.TextToImage => "TextToImage",
                _ => "ChatCompletion",
            };
        }

        public static PasteAIUsage FromConfigString(string usage)
        {
            return usage switch
            {
                "TextToImage" => PasteAIUsage.TextToImage,
                _ => PasteAIUsage.ChatCompletion,
            };
        }
    }
}
