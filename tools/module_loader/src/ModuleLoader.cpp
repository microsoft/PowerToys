// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "ModuleLoader.h"
#include <iostream>
#include <stdexcept>

ModuleLoader::ModuleLoader()
    : m_hModule(nullptr)
    , m_module(nullptr)
{
}

ModuleLoader::~ModuleLoader()
{
    if (m_module)
    {
        try
        {
            m_module->destroy();
        }
        catch (...)
        {
            // Ignore exceptions during cleanup
        }
        m_module = nullptr;
    }

    if (m_hModule)
    {
        FreeLibrary(m_hModule);
        m_hModule = nullptr;
    }
}

bool ModuleLoader::Load(const std::wstring& dllPath)
{
    if (m_hModule || m_module)
    {
        std::wcerr << L"Error: Module already loaded\n";
        return false;
    }

    m_dllPath = dllPath;

    // Load the DLL
    m_hModule = LoadLibraryW(dllPath.c_str());
    if (!m_hModule)
    {
        DWORD error = GetLastError();
        std::wcerr << L"Error: Failed to load DLL. Error code: " << error << L"\n";
        return false;
    }

    // Get the powertoy_create function
    using powertoy_create_func = PowertoyModuleIface* (*)();
    auto create_func = reinterpret_cast<powertoy_create_func>(
        GetProcAddress(m_hModule, "powertoy_create"));

    if (!create_func)
    {
        std::wcerr << L"Error: DLL does not export 'powertoy_create' function\n";
        FreeLibrary(m_hModule);
        m_hModule = nullptr;
        return false;
    }

    // Create the module instance
    m_module = create_func();
    if (!m_module)
    {
        std::wcerr << L"Error: powertoy_create() returned nullptr\n";
        FreeLibrary(m_hModule);
        m_hModule = nullptr;
        return false;
    }

    std::wcout << L"Module instance created successfully\n";
    return true;
}

void ModuleLoader::Enable()
{
    if (!m_module)
    {
        throw std::runtime_error("Module not loaded");
    }

    m_module->enable();
}

void ModuleLoader::Disable()
{
    if (!m_module)
    {
        return;
    }

    m_module->disable();
}

bool ModuleLoader::IsEnabled() const
{
    if (!m_module)
    {
        return false;
    }

    return m_module->is_enabled();
}

void ModuleLoader::SetConfig(const std::wstring& configJson)
{
    if (!m_module)
    {
        throw std::runtime_error("Module not loaded");
    }

    m_module->set_config(configJson.c_str());
}

std::wstring ModuleLoader::GetModuleName() const
{
    if (!m_module)
    {
        return L"<not loaded>";
    }

    const wchar_t* name = m_module->get_name();
    return name ? name : L"<unknown>";
}

std::wstring ModuleLoader::GetModuleKey() const
{
    if (!m_module)
    {
        return L"<not loaded>";
    }

    const wchar_t* key = m_module->get_key();
    return key ? key : L"<unknown>";
}

size_t ModuleLoader::GetHotkeys(PowertoyModuleIface::Hotkey* buffer, size_t bufferSize)
{
    if (!m_module)
    {
        return 0;
    }

    return m_module->get_hotkeys(buffer, bufferSize);
}

bool ModuleLoader::OnHotkey(size_t hotkeyId)
{
    if (!m_module)
    {
        return false;
    }

    return m_module->on_hotkey(hotkeyId);
}

std::optional<PowertoyModuleIface::HotkeyEx> ModuleLoader::GetHotkeyEx()
{
    if (!m_module)
    {
        return std::nullopt;
    }

    return m_module->GetHotkeyEx();
}

void ModuleLoader::OnHotkeyEx()
{
    if (!m_module)
    {
        return;
    }

    m_module->OnHotkeyEx();
}
