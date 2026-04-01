// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "HotkeyManager.h"
#include <iostream>
#include <sstream>

HotkeyManager::HotkeyManager()
    : m_nextHotkeyId(1) // Start from 1
    , m_hotkeyExRegistered(false)
    , m_hotkeyExId(0)
{
}

HotkeyManager::~HotkeyManager()
{
    UnregisterAll();
}

UINT HotkeyManager::ConvertModifiers(bool win, bool ctrl, bool alt, bool shift) const
{
    UINT modifiers = MOD_NOREPEAT; // Prevent repeat events
    if (win) modifiers |= MOD_WIN;
    if (ctrl) modifiers |= MOD_CONTROL;
    if (alt) modifiers |= MOD_ALT;
    if (shift) modifiers |= MOD_SHIFT;
    return modifiers;
}

bool HotkeyManager::RegisterModuleHotkeys(ModuleLoader& moduleLoader)
{
    if (!moduleLoader.IsLoaded())
    {
        std::wcerr << L"Error: Module not loaded\n";
        return false;
    }

    bool anyRegistered = false;

    // First, try the newer GetHotkeyEx() API
    auto hotkeyEx = moduleLoader.GetHotkeyEx();
    if (hotkeyEx.has_value())
    {
        std::wcout << L"Module has HotkeyEx activation hotkey\n";
        
        UINT modifiers = hotkeyEx->modifiersMask | MOD_NOREPEAT;
        UINT vkCode = hotkeyEx->vkCode;

        if (vkCode != 0)
        {
            int hotkeyId = m_nextHotkeyId++;
            
            std::wcout << L"  Registering HotkeyEx: ";
            std::wcout << ModifiersToString(modifiers) << L"+" << VKeyToString(vkCode);

            if (RegisterHotKey(nullptr, hotkeyId, modifiers, vkCode))
            {
                m_hotkeyExRegistered = true;
                m_hotkeyExId = hotkeyId;
                
                std::wcout << L" - OK (Activation/Toggle)\n";
                anyRegistered = true;
            }
            else
            {
                DWORD error = GetLastError();
                std::wcout << L" - FAILED (Error: " << error << L")\n";
                
                if (error == ERROR_HOTKEY_ALREADY_REGISTERED)
                {
                    std::wcout << L"    (Hotkey is already registered by another application)\n";
                }
            }
        }
    }

    // Also check the legacy get_hotkeys() API
    size_t hotkeyCount = moduleLoader.GetHotkeys(nullptr, 0);
    if (hotkeyCount > 0)
    {
        std::wcout << L"Module reports " << hotkeyCount << L" legacy hotkey(s)\n";

        // Allocate buffer and get the hotkeys
        std::vector<PowertoyModuleIface::Hotkey> hotkeys(hotkeyCount);
        size_t actualCount = moduleLoader.GetHotkeys(hotkeys.data(), hotkeyCount);

        // Register each hotkey
        for (size_t i = 0; i < actualCount; i++)
        {
            const auto& hotkey = hotkeys[i];
            
            UINT modifiers = ConvertModifiers(hotkey.win, hotkey.ctrl, hotkey.alt, hotkey.shift);
            UINT vkCode = hotkey.key;

            if (vkCode == 0)
            {
                std::wcout << L"  Skipping hotkey " << i << L" (no key code)\n";
                continue;
            }

            int hotkeyId = m_nextHotkeyId++;
            
            std::wcout << L"  Registering hotkey " << i << L": ";
            std::wcout << ModifiersToString(modifiers) << L"+" << VKeyToString(vkCode);

            if (RegisterHotKey(nullptr, hotkeyId, modifiers, vkCode))
            {
                HotkeyInfo info;
                info.id = hotkeyId;
                info.moduleHotkeyId = i;
                info.modifiers = modifiers;
                info.vkCode = vkCode;
                info.description = ModifiersToString(modifiers) + L"+" + VKeyToString(vkCode);
                
                m_registeredHotkeys.push_back(info);
                std::wcout << L" - OK\n";
                anyRegistered = true;
            }
            else
            {
                DWORD error = GetLastError();
                std::wcout << L" - FAILED (Error: " << error << L")\n";
                
                if (error == ERROR_HOTKEY_ALREADY_REGISTERED)
                {
                    std::wcout << L"    (Hotkey is already registered by another application)\n";
                }
            }
        }
    }

    if (!anyRegistered && hotkeyCount == 0 && !hotkeyEx.has_value())
    {
        std::wcout << L"Module has no hotkeys\n";
    }

    return anyRegistered;
}

void HotkeyManager::UnregisterAll()
{
    for (const auto& hotkey : m_registeredHotkeys)
    {
        UnregisterHotKey(nullptr, hotkey.id);
    }
    m_registeredHotkeys.clear();

    if (m_hotkeyExRegistered)
    {
        UnregisterHotKey(nullptr, m_hotkeyExId);
        m_hotkeyExRegistered = false;
        m_hotkeyExId = 0;
    }
}

bool HotkeyManager::HandleHotkey(int hotkeyId, ModuleLoader& moduleLoader)
{
    // Check if it's the HotkeyEx activation hotkey
    if (m_hotkeyExRegistered && hotkeyId == m_hotkeyExId)
    {
        std::wcout << L"\nActivation hotkey triggered (HotkeyEx)\n";
        
        moduleLoader.OnHotkeyEx();
        
        std::wcout << L"Module toggled via activation hotkey\n";
        std::wcout << L"Module enabled: " << (moduleLoader.IsEnabled() ? L"Yes" : L"No") << L"\n\n";
        
        return true;
    }

    // Check legacy hotkeys
    for (const auto& hotkey : m_registeredHotkeys)
    {
        if (hotkey.id == hotkeyId)
        {
            std::wcout << L"\nHotkey triggered: " << hotkey.description << L"\n";
            
            bool result = moduleLoader.OnHotkey(hotkey.moduleHotkeyId);
            
            std::wcout << L"Module handled hotkey: " << (result ? L"Swallowed" : L"Not swallowed") << L"\n";
            std::wcout << L"Module enabled: " << (moduleLoader.IsEnabled() ? L"Yes" : L"No") << L"\n\n";
            
            return true;
        }
    }
    
    return false;
}

void HotkeyManager::PrintHotkeys() const
{
    for (const auto& hotkey : m_registeredHotkeys)
    {
        std::wcout << L"  " << hotkey.description << L"\n";
    }
}

std::wstring HotkeyManager::ModifiersToString(UINT modifiers) const
{
    std::wstringstream ss;
    bool first = true;

    if (modifiers & MOD_WIN)
    {
        if (!first) ss << L"+";
        ss << L"Win";
        first = false;
    }
    if (modifiers & MOD_CONTROL)
    {
        if (!first) ss << L"+";
        ss << L"Ctrl";
        first = false;
    }
    if (modifiers & MOD_ALT)
    {
        if (!first) ss << L"+";
        ss << L"Alt";
        first = false;
    }
    if (modifiers & MOD_SHIFT)
    {
        if (!first) ss << L"+";
        ss << L"Shift";
        first = false;
    }

    return ss.str();
}

std::wstring HotkeyManager::VKeyToString(UINT vkCode) const
{
    // Handle special keys
    switch (vkCode)
    {
    case VK_SPACE: return L"Space";
    case VK_RETURN: return L"Enter";
    case VK_ESCAPE: return L"Esc";
    case VK_TAB: return L"Tab";
    case VK_BACK: return L"Backspace";
    case VK_DELETE: return L"Del";
    case VK_INSERT: return L"Ins";
    case VK_HOME: return L"Home";
    case VK_END: return L"End";
    case VK_PRIOR: return L"PgUp";
    case VK_NEXT: return L"PgDn";
    case VK_LEFT: return L"Left";
    case VK_RIGHT: return L"Right";
    case VK_UP: return L"Up";
    case VK_DOWN: return L"Down";
    case VK_F1: return L"F1";
    case VK_F2: return L"F2";
    case VK_F3: return L"F3";
    case VK_F4: return L"F4";
    case VK_F5: return L"F5";
    case VK_F6: return L"F6";
    case VK_F7: return L"F7";
    case VK_F8: return L"F8";
    case VK_F9: return L"F9";
    case VK_F10: return L"F10";
    case VK_F11: return L"F11";
    case VK_F12: return L"F12";
    }

    // For alphanumeric keys, use MapVirtualKey
    wchar_t keyName[256];
    UINT scanCode = MapVirtualKeyW(vkCode, MAPVK_VK_TO_VSC);
    
    if (GetKeyNameTextW(scanCode << 16, keyName, 256) > 0)
    {
        return keyName;
    }

    // Fallback to hex code
    std::wstringstream ss;
    ss << L"0x" << std::hex << vkCode;
    return ss.str();
}
