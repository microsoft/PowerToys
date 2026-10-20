// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <cwchar>
#include <filesystem>

namespace BugReportTool
{
    inline void CopySettingsFolder(const std::filesystem::path& source, const std::filesystem::path& destination)
    {
        std::filesystem::create_directory(destination, source);

        for (const auto& entry : std::filesystem::directory_iterator(source))
        {
            const auto filename = entry.path().filename();
            // Runner holds this synchronization file exclusively for its entire lifetime.
            if (_wcsicmp(filename.c_str(), L"settings.json.cli-lock") == 0)
            {
                continue;
            }

            std::filesystem::copy(entry.path(), destination / filename, std::filesystem::copy_options::recursive);
        }
    }
}
