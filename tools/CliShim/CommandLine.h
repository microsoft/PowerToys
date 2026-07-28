// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <string>
#include <string_view>

namespace CommandLine
{
    // Removes argv[0] the way the CRT, CommandLineToArgvW and CreateProcessW all tokenize it - a
    // quoted name runs to the next quote, an unquoted one to the first whitespace - then trims the
    // separating spaces/tabs and preserves the remaining command-line text verbatim. Leading
    // whitespace is skipped first, which the CRT does not do; see CommandLine.cpp.
    std::wstring StripArgumentZero(std::wstring_view commandLine);
}
