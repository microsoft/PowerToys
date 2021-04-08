#pragma once

#include <filesystem>
#include <common/version/version.h>

namespace LoggerHelpers
{
    inline std::filesystem::path get_log_folder_path(std::wstring_view appPath)
    {
        std::filesystem::path logFolderPath(appPath);
        logFolderPath.append(LogSettings::logPath);
        logFolderPath.append(get_product_version());
        return logFolderPath;
    }

    inline bool delete_old_log_folder(const std::filesystem::path& logFolderPath)
    {
        try
        {
            std::filesystem::remove_all(logFolderPath);
            return true;
        }
        catch (std::filesystem::filesystem_error& e)
        {
            Logger::error("Failed to delete old log folder: {}", e.what());
        }

        return false;
    }

    inline bool delete_other_versions_log_folders(std::wstring_view appPath, const std::filesystem::path& currentVersionLogFolder)
    {
        bool result = true;
        std::filesystem::path logFolderPath(appPath);
        logFolderPath.append(LogSettings::logPath);

        for (const auto& dir : std::filesystem::directory_iterator(logFolderPath))
        {
            if (dir != currentVersionLogFolder)
            {
                try
                {
                    std::filesystem::remove_all(dir);
                }
                catch (std::filesystem::filesystem_error& e)
                {
                    Logger::error("Failed to delete previous version log folder: {}", e.what());
                    result = false;
                }                
            }
        }

        return result;
    }
}
