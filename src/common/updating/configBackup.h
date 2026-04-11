// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <filesystem>
#include <fstream>
#include <string>

namespace updating
{
    namespace fs = std::filesystem;

    // Check if a JSON file is corrupted (contains null bytes, as seen in #46179)
    inline bool IsJsonFileCorrupted(const fs::path& filePath)
    {
        try
        {
            std::ifstream file(filePath, std::ios::binary);
            if (!file.is_open())
            {
                return false;
            }

            constexpr size_t c_readChunkSize{ 4096 };
            char buffer[c_readChunkSize];
            while (file.read(buffer, c_readChunkSize) || file.gcount() > 0)
            {
                const auto bytesRead = file.gcount();
                for (std::streamsize i = 0; i < bytesRead; ++i)
                {
                    if (buffer[i] == '\0')
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch (...)
        {
            return true;
        }
    }

    // Backup all JSON config files before update to protect against corruption (#46179)
    inline void BackupConfigFiles(const fs::path& rootPath)
    {
        try
        {
            const fs::path backupDir = rootPath / L"ConfigBackup";

            std::error_code ec;
            fs::remove_all(backupDir, ec);
            fs::create_directories(backupDir, ec);
            if (ec)
            {
                return;
            }

            for (const auto& entry : fs::directory_iterator(rootPath, ec))
            {
                if (ec)
                {
                    break;
                }

                if (entry.is_regular_file() && entry.path().extension() == L".json")
                {
                    fs::copy_file(entry.path(), backupDir / entry.path().filename(), fs::copy_options::overwrite_existing, ec);
                }
                else if (entry.is_directory())
                {
                    const auto dirName = entry.path().filename().wstring();
                    if (dirName == L"ConfigBackup" || dirName == L"Updates")
                    {
                        continue;
                    }

                    const auto moduleBackup = backupDir / entry.path().filename();
                    fs::create_directories(moduleBackup, ec);

                    std::error_code moduleEc;
                    for (const auto& moduleEntry : fs::directory_iterator(entry.path(), moduleEc))
                    {
                        if (moduleEc)
                        {
                            break;
                        }

                        if (moduleEntry.is_regular_file() && moduleEntry.path().extension() == L".json")
                        {
                            fs::copy_file(moduleEntry.path(), moduleBackup / moduleEntry.path().filename(), fs::copy_options::overwrite_existing, moduleEc);
                        }
                    }
                }
            }
        }
        catch (...)
        {
        }
    }

    // Restore JSON configs from backup if corruption is detected after update
    inline void RestoreCorruptedConfigs(const fs::path& rootPath)
    {
        try
        {
            const fs::path backupDir = rootPath / L"ConfigBackup";

            if (!fs::exists(backupDir))
            {
                return;
            }

            std::error_code ec;
            for (const auto& backupEntry : fs::directory_iterator(backupDir, ec))
            {
                if (ec)
                {
                    break;
                }

                if (backupEntry.is_regular_file() && backupEntry.path().extension() == L".json")
                {
                    const auto originalPath = rootPath / backupEntry.path().filename();
                    if (fs::exists(originalPath) && IsJsonFileCorrupted(originalPath))
                    {
                        fs::copy_file(backupEntry.path(), originalPath, fs::copy_options::overwrite_existing, ec);
                    }
                }
                else if (backupEntry.is_directory())
                {
                    const auto moduleDir = rootPath / backupEntry.path().filename();

                    std::error_code moduleEc;
                    for (const auto& moduleBackupEntry : fs::directory_iterator(backupEntry.path(), moduleEc))
                    {
                        if (moduleEc)
                        {
                            break;
                        }

                        if (moduleBackupEntry.is_regular_file() && moduleBackupEntry.path().extension() == L".json")
                        {
                            const auto originalModulePath = moduleDir / moduleBackupEntry.path().filename();
                            if (fs::exists(originalModulePath) && IsJsonFileCorrupted(originalModulePath))
                            {
                                fs::copy_file(moduleBackupEntry.path(), originalModulePath, fs::copy_options::overwrite_existing, moduleEc);
                            }
                        }
                    }
                }
            }
        }
        catch (...)
        {
        }
    }
}
