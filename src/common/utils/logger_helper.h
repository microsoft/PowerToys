#pragma once

#include <filesystem>
#include <string>
#include <common/version/version.h>
#include <common/SettingsAPI/settings_helpers.h>

namespace LoggerHelpers
{
    std::filesystem::path get_log_folder_path(std::wstring_view appPath);

    bool delete_old_log_folder(const std::filesystem::path& logFolderPath);

    bool dir_exists(std::filesystem::path dir);

    bool delete_other_versions_log_folders(std::wstring_view appPath, const std::filesystem::path& currentVersionLogFolder);

    void init_logger(std::wstring moduleName, std::wstring internalPath, std::string loggerName);
}
