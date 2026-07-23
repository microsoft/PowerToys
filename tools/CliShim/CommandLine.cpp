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

        // A non-shell CreateProcessW caller can prepend whitespace; without this skip the
        // unquoted scan stalls at index 0 and leaks argv[0] into the forwarded tail.
        while (index < commandLine.size() && isWhitespace(commandLine[index]))
        {
            ++index;
        }

        // CRT argv[0] parsing treats quotes as whitespace toggles and backslashes literally;
        // adjacent quoted and unquoted segments remain one token.
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
