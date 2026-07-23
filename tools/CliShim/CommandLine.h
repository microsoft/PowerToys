// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <string>
#include <string_view>

namespace CommandLine
{
    // Removes argv[0] using CRT quote rules, trims separating spaces/tabs, and
    // preserves the remaining command-line text verbatim.
    std::wstring StripArgumentZero(std::wstring_view commandLine);
}
