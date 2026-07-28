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
        // unquoted scan stalls at index 0 and leaks argv[0] into the forwarded tail. This is a
        // deliberate departure from the CRT, which would report an empty argv[0] instead.
        while (index < commandLine.size() && isWhitespace(commandLine[index]))
        {
            ++index;
        }

        // argv[0] is tokenized differently from every later argument, and identically by the CRT,
        // CommandLineToArgvW and CreateProcessW's own program-name scan: a quoted name runs to the
        // NEXT quote - backslashes do not escape it and there is no toggling - and an unquoted name
        // runs to the first whitespace. Matching that rule is what makes the shim transparent: the
        // target sees the exact tail it would have seen had the caller invoked it directly.
        // Toggling instead would swallow the argument in `"...\PowerToys.FancyZones.CLI.exe"--help`,
        // where every other program on the system sees `--help`.
        if (index < commandLine.size() && commandLine[index] == L'"')
        {
            ++index;

            while (index < commandLine.size() && commandLine[index] != L'"')
            {
                ++index;
            }

            if (index < commandLine.size())
            {
                ++index;
            }
        }
        else
        {
            while (index < commandLine.size() && !isWhitespace(commandLine[index]))
            {
                ++index;
            }
        }

        while (index < commandLine.size() && isWhitespace(commandLine[index]))
        {
            ++index;
        }

        return std::wstring{ commandLine.substr(index) };
    }
}
