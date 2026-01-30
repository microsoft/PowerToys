// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <Windows.h>
#include <string>

/// <summary>
/// Utility class for discovering and loading PowerToy module settings
/// </summary>
class SettingsLoader
{
public:
    SettingsLoader();
    ~SettingsLoader();

    /// <summary>
    /// Load settings for a PowerToy module
    /// </summary>
    /// <param name="moduleName">Name of the module (e.g., "CursorWrap")</param>
    /// <param name="moduleDllPath">Full path to the module DLL (for checking local settings.json)</param>
    /// <returns>JSON settings string, or empty string if not found</returns>
    std::wstring LoadSettings(const std::wstring& moduleName, const std::wstring& moduleDllPath);

    /// <summary>
    /// Get the settings file path for a module
    /// </summary>
    /// <param name="moduleName">Name of the module</param>
    /// <returns>Full path to the settings.json file</returns>
    std::wstring GetSettingsPath(const std::wstring& moduleName) const;

private:
    /// <summary>
    /// Get the PowerToys root settings directory
    /// </summary>
    /// <returns>Path to %LOCALAPPDATA%\Microsoft\PowerToys</returns>
    std::wstring GetPowerToysSettingsRoot() const;

    /// <summary>
    /// Read a text file into a string
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>File contents as a string</returns>
    std::wstring ReadFileContents(const std::wstring& filePath) const;
};
