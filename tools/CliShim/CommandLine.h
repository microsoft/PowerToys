// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <string>
#include <string_view>

namespace CommandLine
{
    // Returns the command line with its first token (argv[0]) removed, following the C
    // runtime rule for the program name: leading whitespace is skipped first; then, when
    // argv[0] starts with a quote it ends at the next quote (no backslash-escaping for the
    // program name), otherwise it ends at the first whitespace. Whitespace before the first
    // real argument is then trimmed. The remaining arguments are returned verbatim.
    std::wstring StripArgumentZero(std::wstring_view commandLine);
}
