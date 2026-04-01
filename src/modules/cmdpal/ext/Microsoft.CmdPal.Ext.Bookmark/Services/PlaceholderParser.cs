// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

public class PlaceholderParser : IPlaceholderParser
{
    public bool ParsePlaceholders(string input, out string head, out List<PlaceholderInfo> placeholders)
    {
        ArgumentNullException.ThrowIfNull(input);

        head = string.Empty;
        placeholders = [];

        if (string.IsNullOrEmpty(input))
        {
            head = string.Empty;
            return false;
        }

        var foundPlaceholders = new List<PlaceholderInfo>();
        var searchStart = 0;
        var firstPlaceholderStart = -1;
        var hasValidPlaceholder = false;

        while (searchStart < input.Length)
        {
            var openBrace = input.IndexOf('{', searchStart);
            if (openBrace == -1)
            {
                break;
            }

            var closeBrace = input.IndexOf('}', openBrace + 1);
            if (closeBrace == -1)
            {
                break;
            }

            // Extract potential placeholder name
            var placeholderContent = input.Substring(openBrace + 1, closeBrace - openBrace - 1);

            // Check if it's a valid placeholder
            if (!string.IsNullOrEmpty(placeholderContent) &&
                !IsGuidFormat(placeholderContent) &&
                IsValidPlaceholderName(placeholderContent))
            {
                // Valid placeholder found
                foundPlaceholders.Add(new PlaceholderInfo(placeholderContent, openBrace));
                hasValidPlaceholder = true;

                // Remember the first valid placeholder position
                if (firstPlaceholderStart == -1)
                {
                    firstPlaceholderStart = openBrace;
                }
            }

            // Continue searching after this brace pair
            searchStart = closeBrace + 1;
        }

        // Convert to Placeholder objects
        placeholders = foundPlaceholders;

        if (hasValidPlaceholder)
        {
            head = input[..firstPlaceholderStart];
            return true;
        }
        else
        {
            head = input;
            return false;
        }
    }

    private static bool IsValidPlaceholderName(string name)
    {
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsGuidFormat(string content) => Guid.TryParse(content, out _);
}
