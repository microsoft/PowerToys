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

        // A non-shell CreateProcessW caller can prepend whitespace; without this skip the scan
        // below stalls at index 0 and leaks argv[0] into the forwarded tail. This is a deliberate
        // departure from the CRT, which would report an empty argv[0] instead.
        while (index < commandLine.size() && isWhitespace(commandLine[index]))
        {
            ++index;
        }

        // argv[0] is tokenized differently from every later argument, and the rule that matters is
        // the CRT's, because that is what every target ends up parsing: FileLocksmithCLI is a
        // native wmain, and the .NET CLIs receive their string[] from the apphost's wmain. The CRT
        // (ucrt\startup\argv_parsing.cpp, parse_command_line) toggles an in-quotes flag on every
        // quote while scanning argv[0] and ends the name at the first whitespace found outside
        // quotes; a quote never terminates the name and a backslash never escapes one.
        //
        // CommandLineToArgvW is the odd one out - it ends a quoted argv[0] at the closing quote,
        // with no toggling - so following it instead would leak the rest of the program name into
        // the tail of a partially quoted command line such as
        // `"%ProgramFiles%"\PowerToys\bin\PowerToys.FancyZones.CLI.exe arg`, which cmd.exe passes
        // through verbatim. Matching the CRT is what makes the shim transparent: the target sees
        // the exact tail it would have seen had the caller invoked it directly.
        bool inQuotes = false;
        while (index < commandLine.size())
        {
            const wchar_t character = commandLine[index];
            ++index;

            if (character == L'"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && isWhitespace(character))
            {
                break;
            }
        }

        while (index < commandLine.size() && isWhitespace(commandLine[index]))
        {
            ++index;
        }

        return std::wstring{ commandLine.substr(index) };
    }
}
