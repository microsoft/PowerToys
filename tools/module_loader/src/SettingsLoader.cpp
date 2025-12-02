// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "SettingsLoader.h"
#include <iostream>
#include <fstream>
#include <sstream>
#include <filesystem>
#include <Shlobj.h>

SettingsLoader::SettingsLoader()
{
}

SettingsLoader::~SettingsLoader()
{
}

std::wstring SettingsLoader::GetPowerToysSettingsRoot() const
{
    // Get %LOCALAPPDATA%
    PWSTR localAppDataPath = nullptr;
    HRESULT hr = SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &localAppDataPath);
    
    if (FAILED(hr) || !localAppDataPath)
    {
        std::wcerr << L"Error: Failed to get LOCALAPPDATA path\n";
        return L"";
    }

    std::wstring result(localAppDataPath);
    CoTaskMemFree(localAppDataPath);

    // Append PowerToys directory
    result += L"\\Microsoft\\PowerToys";
    return result;
}

std::wstring SettingsLoader::GetSettingsPath(const std::wstring& moduleName) const
{
    std::wstring root = GetPowerToysSettingsRoot();
    if (root.empty())
    {
        return L"";
    }

    // Construct path: %LOCALAPPDATA%\Microsoft\PowerToys\<ModuleName>\settings.json
    std::wstring settingsPath = root + L"\\" + moduleName + L"\\settings.json";
    return settingsPath;
}

std::wstring SettingsLoader::ReadFileContents(const std::wstring& filePath) const
{
    std::wifstream file(filePath, std::ios::binary);
    if (!file.is_open())
    {
        std::wcerr << L"Error: Could not open file: " << filePath << L"\n";
        return L"";
    }

    // Read the entire file
    std::wstringstream buffer;
    buffer << file.rdbuf();
    
    return buffer.str();
}

std::wstring SettingsLoader::LoadSettings(const std::wstring& moduleName, const std::wstring& moduleDllPath)
{
    const std::wstring powerToysPrefix = L"PowerToys.";
    
    // Build list of possible module name variations to try
    std::vector<std::wstring> moduleNameVariants;
    
    // Try exact name first
    moduleNameVariants.push_back(moduleName);
    
    // If doesn't start with "PowerToys.", try adding it
    if (moduleName.find(powerToysPrefix) != 0)
    {
        moduleNameVariants.push_back(powerToysPrefix + moduleName);
    }
    // If starts with "PowerToys.", try without it
    else
    {
        moduleNameVariants.push_back(moduleName.substr(powerToysPrefix.length()));
    }

    // FIRST: Try same directory as the module DLL
    if (!moduleDllPath.empty())
    {
        std::filesystem::path dllPath(moduleDllPath);
        std::filesystem::path dllDirectory = dllPath.parent_path();
        
        std::wstring localSettingsPath = (dllDirectory / L"settings.json").wstring();
        std::wcout << L"Trying settings path (module directory): " << localSettingsPath << L"\n";
        
        if (std::filesystem::exists(localSettingsPath))
        {
            std::wstring contents = ReadFileContents(localSettingsPath);
            if (!contents.empty())
            {
                std::wcout << L"Settings file loaded from module directory (" << contents.size() << L" characters)\n";
                return contents;
            }
        }
    }

    // SECOND: Try standard PowerToys settings locations
    for (const auto& variant : moduleNameVariants)
    {
        std::wstring settingsPath = GetSettingsPath(variant);
        
        std::wcout << L"Trying settings path: " << settingsPath << L"\n";

        // Check if file exists (case-sensitive path)
        if (std::filesystem::exists(settingsPath))
        {
            std::wstring contents = ReadFileContents(settingsPath);
            if (!contents.empty())
            {
                std::wcout << L"Settings file loaded (" << contents.size() << L" characters)\n";
                return contents;
            }
        }
        else
        {
            // Try case-insensitive search in the parent directory
            std::wstring root = GetPowerToysSettingsRoot();
            if (!root.empty() && std::filesystem::exists(root))
            {
                try
                {
                    // Search for a directory that matches case-insensitively
                    for (const auto& entry : std::filesystem::directory_iterator(root))
                    {
                        if (entry.is_directory())
                        {
                            std::wstring dirName = entry.path().filename().wstring();
                            
                            // Case-insensitive comparison
                            if (_wcsicmp(dirName.c_str(), variant.c_str()) == 0)
                            {
                                std::wstring actualSettingsPath = entry.path().wstring() + L"\\settings.json";
                                std::wcout << L"Found case-insensitive match: " << actualSettingsPath << L"\n";
                                
                                if (std::filesystem::exists(actualSettingsPath))
                                {
                                    std::wstring contents = ReadFileContents(actualSettingsPath);
                                    if (!contents.empty())
                                    {
                                        std::wcout << L"Settings file loaded (" << contents.size() << L" characters)\n";
                                        return contents;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (const std::filesystem::filesystem_error& e)
                {
                    std::wcerr << L"Error searching directory: " << e.what() << L"\n";
                }
            }
        }
    }

    std::wcerr << L"Error: Settings file not found in any expected location:\n";
    if (!moduleDllPath.empty())
    {
        std::filesystem::path dllPath(moduleDllPath);
        std::filesystem::path dllDirectory = dllPath.parent_path();
        std::wcerr << L"  - " << (dllDirectory / L"settings.json").wstring() << L" (module directory)\n";
    }
    for (const auto& variant : moduleNameVariants)
    {
        std::wcerr << L"  - " << GetSettingsPath(variant) << L"\n";
    }
    
    return L"";
}
