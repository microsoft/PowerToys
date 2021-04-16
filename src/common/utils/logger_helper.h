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

    inline bool create_dir_if_does_not_exist(std::filesystem::path dir)
    {
        std::error_code err;
        auto entry = std::filesystem::directory_entry(dir, err);
        if (err.value())
        {
            Logger::error("Failed to create directory entry. {}", err.message());
            return false;
        }

        if (!entry.exists())
        {
            Logger::warn("Directory {} does not exist", dir.string());
            std::error_code err;
            if (!std::filesystem::create_directory(dir, err))
            {
                Logger::error("Failed to create directory {}. {}", dir.string(), err.message());
                return false;
            }
        }

        return true;
    }

    inline bool delete_other_versions_log_folders(std::wstring_view appPath, const std::filesystem::path& currentVersionLogFolder)
    {
        bool result = true;
        std::filesystem::path logFolderPath(appPath);
        logFolderPath.append(LogSettings::logPath);

        if (!create_dir_if_does_not_exist(logFolderPath))
        {
            return false;
        }

        std::error_code err;
        auto folders = std::filesystem::directory_iterator(logFolderPath, err);
        if (err.value())
        {
            Logger::error("Failed to create directory iterator for {}. {}", logFolderPath.string(), err.message());
            return false;
        }

        for (const auto& dir : folders)
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
