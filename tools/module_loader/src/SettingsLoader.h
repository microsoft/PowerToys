// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <Windows.h>
#include <string>
#include <vector>
#include <utility>

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

    /// <summary>
    /// Display settings information for a module
    /// </summary>
    /// <param name="moduleName">Name of the module</param>
    /// <param name="moduleDllPath">Path to the module DLL</param>
    void DisplaySettingsInfo(const std::wstring& moduleName, const std::wstring& moduleDllPath);

    /// <summary>
    /// Get a specific setting value
    /// </summary>
    /// <param name="moduleName">Name of the module</param>
    /// <param name="moduleDllPath">Path to the module DLL</param>
    /// <param name="key">Setting key to retrieve</param>
    /// <returns>Value as string, or empty if not found</returns>
    std::wstring GetSettingValue(const std::wstring& moduleName, const std::wstring& moduleDllPath, const std::wstring& key);

    /// <summary>
    /// Set a specific setting value
    /// </summary>
    /// <param name="moduleName">Name of the module</param>
    /// <param name="moduleDllPath">Path to the module DLL</param>
    /// <param name="key">Setting key to set</param>
    /// <param name="value">Value to set</param>
    /// <returns>True if successful</returns>
    bool SetSettingValue(const std::wstring& moduleName, const std::wstring& moduleDllPath, const std::wstring& key, const std::wstring& value);

    /// <summary>
    /// Find the actual settings file path (handles case-insensitivity)
    /// </summary>
    /// <param name="moduleName">Name of the module</param>
    /// <param name="moduleDllPath">Path to the module DLL</param>
    /// <returns>Actual path to settings.json, or empty if not found</returns>
    std::wstring FindSettingsFilePath(const std::wstring& moduleName, const std::wstring& moduleDllPath);

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

    /// <summary>
    /// Write a string to a text file
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="contents">Contents to write</param>
    /// <returns>True if successful</returns>
    bool WriteFileContents(const std::wstring& filePath, const std::wstring& contents) const;

    /// <summary>
    /// Parse settings properties from JSON and display them
    /// </summary>
    /// <param name="settingsJson">JSON string containing settings</param>
    /// <param name="indent">Indentation level</param>
    void DisplayJsonProperties(const std::wstring& settingsJson, int indent = 0);

    /// <summary>
    /// Parse a hotkey object from JSON and format it as a string (e.g., "Win+Alt+U")
    /// </summary>
    /// <param name="json">JSON string</param>
    /// <param name="objStart">Start position of the hotkey object</param>
    /// <param name="objEnd">Output: end position of the hotkey object</param>
    /// <returns>Formatted hotkey string, or empty if not a valid hotkey</returns>
    std::string ParseHotkeyObject(const std::string& json, size_t objStart, size_t& objEnd);

    /// <summary>
    /// Check if a JSON object appears to be a hotkey settings object
    /// </summary>
    /// <param name="json">JSON string</param>
    /// <param name="objStart">Start position of the object</param>
    /// <returns>True if this looks like a hotkey object</returns>
    bool IsHotkeyObject(const std::string& json, size_t objStart);

    /// <summary>
    /// Prompt user for yes/no confirmation
    /// </summary>
    /// <param name="prompt">The question to ask</param>
    /// <returns>True if user answered yes</returns>
    bool PromptYesNo(const std::wstring& prompt);

    /// <summary>
    /// Add a new property to the JSON settings file
    /// </summary>
    /// <param name="json">The JSON string to modify</param>
    /// <param name="key">The property key to add</param>
    /// <param name="value">The value to set</param>
    /// <returns>Modified JSON string, or empty if failed</returns>
    std::string AddNewProperty(const std::string& json, const std::string& key, const std::string& value);
};
