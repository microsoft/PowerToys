#include "pch.h"
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <spdlog/sinks/null_sink.h>

std::shared_ptr<spdlog::logger> Logger::logger = spdlog::null_logger_mt("Common.Utils.UnitTests");

namespace PTSettingsHelper
{
    std::wstring get_root_save_folder_location()
    {
        return L"";
    }
}
