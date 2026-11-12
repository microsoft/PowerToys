// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <chrono>
#include <filesystem>
#include <optional>
#include <string>
#include <thread>

#include <common/logger/logger.h>
#include <common/utils/json.h>

namespace general_settings
{
    inline std::optional<json::JsonObject> load_from_file_checked(const std::wstring& settings_path)
    {
        constexpr int max_retries = 3;
        int delay_ms = 25;

        for (int retry = 0; retry <= max_retries; retry++)
        {
            auto loaded = json::from_file(settings_path);
            if (loaded.has_value())
            {
                return loaded;
            }

            std::error_code ec;
            const bool exists = std::filesystem::exists(settings_path, ec);
            if (ec)
            {
                Logger::warn("load_general_settings_checked: Failed to check if settings.json exists: {}", ec.message());
                return std::nullopt;
            }

            if (!exists)
            {
                return json::JsonObject{};
            }

            const auto size = std::filesystem::file_size(settings_path, ec);
            if (ec)
            {
                Logger::warn("load_general_settings_checked: Failed to get file size for settings.json: {}", ec.message());
                return std::nullopt;
            }

            // An existing empty file can be the truncation stage of another write.
            // Retry it like any other failed parse instead of allowing a save.
            if (retry == max_retries)
            {
                Logger::warn(L"load_general_settings_checked: Failed to read/parse existing settings.json ({} bytes) after 3 retries. Transient error suspected.", size);
                break;
            }

            std::this_thread::sleep_for(std::chrono::milliseconds(delay_ms));
            delay_ms *= 2;
        }

        return std::nullopt;
    }
}
