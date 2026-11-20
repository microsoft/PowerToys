// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <cstddef>
#include <filesystem>
#include <vector>

namespace ReportSanitizer
{
    inline constexpr std::size_t MaxRegexFileSize = 1024 * 1024;

    // If a failed staging file or folder cannot be deleted, throw to stop report creation.
    std::vector<std::filesystem::path> Sanitize(const std::filesystem::path& reportDirectory);
}
