// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <string>
#include <string_view>

namespace CommandLine
{
    // Returns the command line with its first token (argv[0]) removed, following the C
    // runtime rule for the program name: leading whitespace is skipped first, quotes group
    // whitespace but are otherwise part of argv[0]'s tokenization, and the token ends at the
    // first unquoted whitespace. Whitespace before the first real argument is then trimmed.
    // The remaining arguments are returned verbatim.
    std::wstring StripArgumentZero(std::wstring_view commandLine);
}
