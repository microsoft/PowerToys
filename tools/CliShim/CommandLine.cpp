// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "CommandLine.h"

namespace CommandLine
{
    std::wstring StripArgumentZero(std::wstring_view commandLine)
    {
        size_t index = 0;

        // Skip leading whitespace before argv[0]. The OS loader never produces this, but a
        // non-shell parent that calls CreateProcessW with a padded lpCommandLine can, and
        // without this the unquoted scan below would stall at index 0 and leak the program
        // name into the forwarded arguments.
        while (index < commandLine.size() && (commandLine[index] == L' ' || commandLine[index] == L'\t'))
        {
            ++index;
        }

        if (index < commandLine.size() && commandLine[index] == L'"')
        {
            ++index;
            while (index < commandLine.size() && commandLine[index] != L'"')
            {
                ++index;
            }

            if (index < commandLine.size())
            {
                ++index; // Consume the closing quote.
            }
        }
        else
        {
            while (index < commandLine.size() && commandLine[index] != L' ' && commandLine[index] != L'\t')
            {
                ++index;
            }
        }

        while (index < commandLine.size() && (commandLine[index] == L' ' || commandLine[index] == L'\t'))
        {
            ++index;
        }

        return std::wstring{ commandLine.substr(index) };
    }
}
