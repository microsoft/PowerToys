// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "CommandLine.h"

namespace CommandLine
{
    std::wstring StripArgumentZero(std::wstring_view commandLine)
    {
        const auto isWhitespace = [](const wchar_t character) {
            return character == L' ' || character == L'\t';
        };

        size_t index = 0;

        // Skip leading whitespace before argv[0]. The OS loader never produces this, but a
        // non-shell parent that calls CreateProcessW with a padded lpCommandLine can, and
        // without this the unquoted scan below would stall at index 0 and leak the program
        // name into the forwarded arguments.
        while (index < commandLine.size() && isWhitespace(commandLine[index]))
        {
            ++index;
        }

        // argv[0] follows special CRT rules: quotes may occur anywhere in the token and
        // toggle whether whitespace is significant. Backslashes have no special meaning.
        // Continue after a closing quote because a valid path can mix quoted and unquoted
        // segments, for example: "C:\Program Files"\PowerToys\cli.exe.
        bool inQuotes = false;
        while (index < commandLine.size())
        {
            if (commandLine[index] == L'"')
            {
                inQuotes = !inQuotes;
                ++index;
                continue;
            }

            if (!inQuotes && isWhitespace(commandLine[index]))
            {
                break;
            }

            ++index;
        }

        while (index < commandLine.size() && isWhitespace(commandLine[index]))
        {
            ++index;
        }

        return std::wstring{ commandLine.substr(index) };
    }
}
