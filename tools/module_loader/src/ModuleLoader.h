// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <Windows.h>
#include <string>
#include <vector>
#include <powertoy_module_interface.h>

/// <summary>
/// Wrapper class for loading and managing a PowerToy module DLL
/// </summary>
class ModuleLoader
{
public:
    ModuleLoader();
    ~ModuleLoader();

    // Prevent copying
    ModuleLoader(const ModuleLoader&) = delete;
    ModuleLoader& operator=(const ModuleLoader&) = delete;

    /// <summary>
    /// Load a PowerToy module DLL
    /// </summary>
    /// <param name="dllPath">Path to the module DLL</param>
    /// <returns>True if successful, false otherwise</returns>
    bool Load(const std::wstring& dllPath);

    /// <summary>
    /// Enable the loaded module
    /// </summary>
    void Enable();

    /// <summary>
    /// Disable the loaded module
    /// </summary>
    void Disable();

    /// <summary>
    /// Check if the module is enabled
    /// </summary>
    /// <returns>True if enabled, false otherwise</returns>
    bool IsEnabled() const;

    /// <summary>
    /// Set configuration for the module
    /// </summary>
    /// <param name="configJson">JSON configuration string</param>
    void SetConfig(const std::wstring& configJson);

    /// <summary>
    /// Get the module's localized name
    /// </summary>
    /// <returns>Module name</returns>
    std::wstring GetModuleName() const;

    /// <summary>
    /// Get the module's non-localized key
    /// </summary>
    /// <returns>Module key</returns>
    std::wstring GetModuleKey() const;

    /// <summary>
    /// Get the module's hotkeys
    /// </summary>
    /// <param name="buffer">Buffer to store hotkeys</param>
    /// <param name="bufferSize">Size of the buffer</param>
    /// <returns>Number of hotkeys returned</returns>
    size_t GetHotkeys(PowertoyModuleIface::Hotkey* buffer, size_t bufferSize);

    /// <summary>
    /// Trigger a hotkey callback on the module
    /// </summary>
    /// <param name="hotkeyId">ID of the hotkey to trigger</param>
    /// <returns>True if the key press should be swallowed</returns>
    bool OnHotkey(size_t hotkeyId);

    /// <summary>
    /// Check if the module is loaded
    /// </summary>
    /// <returns>True if loaded, false otherwise</returns>
    bool IsLoaded() const { return m_module != nullptr; }

    /// <summary>
    /// Get the module's activation hotkey (newer HotkeyEx API)
    /// </summary>
    /// <returns>Optional HotkeyEx struct</returns>
    std::optional<PowertoyModuleIface::HotkeyEx> GetHotkeyEx();

    /// <summary>
    /// Trigger the newer-style hotkey callback on the module
    /// </summary>
    void OnHotkeyEx();

private:
    HMODULE m_hModule;
    PowertoyModuleIface* m_module;
    std::wstring m_dllPath;
};
