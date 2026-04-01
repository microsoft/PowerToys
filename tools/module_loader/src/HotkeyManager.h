// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <Windows.h>
#include <string>
#include <vector>
#include <map>
#include "ModuleLoader.h"

/// <summary>
/// Manages hotkey registration using RegisterHotKey API
/// </summary>
class HotkeyManager
{
public:
    HotkeyManager();
    ~HotkeyManager();

    // Prevent copying
    HotkeyManager(const HotkeyManager&) = delete;
    HotkeyManager& operator=(const HotkeyManager&) = delete;

    /// <summary>
    /// Register all hotkeys from a module
    /// </summary>
    /// <param name="moduleLoader">Module to get hotkeys from</param>
    /// <returns>True if at least one hotkey was registered</returns>
    bool RegisterModuleHotkeys(ModuleLoader& moduleLoader);

    /// <summary>
    /// Unregister all hotkeys
    /// </summary>
    void UnregisterAll();

    /// <summary>
    /// Handle a WM_HOTKEY message
    /// </summary>
    /// <param name="hotkeyId">ID from the WM_HOTKEY message</param>
    /// <param name="moduleLoader">Module to trigger the hotkey on</param>
    /// <returns>True if the hotkey was handled</returns>
    bool HandleHotkey(int hotkeyId, ModuleLoader& moduleLoader);

    /// <summary>
    /// Get the number of registered hotkeys
    /// </summary>
    /// <returns>Number of registered hotkeys</returns>
    size_t GetRegisteredCount() const { return m_registeredHotkeys.size() + (m_hotkeyExRegistered ? 1 : 0); }

    /// <summary>
    /// Print registered hotkeys to console
    /// </summary>
    void PrintHotkeys() const;

private:
    struct HotkeyInfo
    {
        int id = 0;
        size_t moduleHotkeyId = 0;
        UINT modifiers = 0;
        UINT vkCode = 0;
        std::wstring description;
    };

    std::vector<HotkeyInfo> m_registeredHotkeys;
    int m_nextHotkeyId;
    bool m_hotkeyExRegistered;
    int m_hotkeyExId;

    /// <summary>
    /// Convert modifier bools to RegisterHotKey modifiers
    /// </summary>
    UINT ConvertModifiers(bool win, bool ctrl, bool alt, bool shift) const;

    /// <summary>
    /// Get a string representation of modifiers
    /// </summary>
    std::wstring ModifiersToString(UINT modifiers) const;

    /// <summary>
    /// Get a string representation of a virtual key code
    /// </summary>
    std::wstring VKeyToString(UINT vkCode) const;
};
