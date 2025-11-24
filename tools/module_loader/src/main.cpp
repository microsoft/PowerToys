// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <Windows.h>
#include <Tlhelp32.h>
#include <iostream>
#include <string>
#include <filesystem>
#include "ModuleLoader.h"
#include "SettingsLoader.h"
#include "HotkeyManager.h"
#include "ConsoleHost.h"

namespace
{
    void PrintUsage()
    {
        std::wcout << L"PowerToys Module Loader - Standalone utility for loading and testing PowerToy modules\n\n";
        std::wcout << L"Usage: ModuleLoader.exe <module_dll_path>\n\n";
        std::wcout << L"Arguments:\n";
        std::wcout << L"  module_dll_path   Path to the PowerToy module DLL (e.g., CursorWrap.dll)\n\n";
        std::wcout << L"Behavior:\n";
        std::wcout << L"  - Automatically discovers settings from %%LOCALAPPDATA%%\\Microsoft\\PowerToys\\<ModuleName>\\settings.json\n";
        std::wcout << L"  - Loads and enables the module\n";
        std::wcout << L"  - Registers module hotkeys\n";
        std::wcout << L"  - Runs until Ctrl+C is pressed\n\n";
        std::wcout << L"Examples:\n";
        std::wcout << L"  ModuleLoader.exe x64\\Debug\\modules\\CursorWrap.dll\n";
        std::wcout << L"  ModuleLoader.exe \"C:\\Program Files\\PowerToys\\modules\\MouseHighlighter.dll\"\n\n";
        std::wcout << L"Notes:\n";
        std::wcout << L"  - Only non-UI modules are supported\n";
        std::wcout << L"  - Module must have a valid settings.json file\n";
        std::wcout << L"  - Debug output is written to module's log directory\n";
    }

    std::wstring ExtractModuleName(const std::wstring& dllPath)
    {
        std::filesystem::path path(dllPath);
        std::wstring filename = path.stem().wstring();
        
        // Remove "PowerToys." prefix if present (case-insensitive)
        const std::wstring powerToysPrefix = L"PowerToys.";
        if (filename.length() >= powerToysPrefix.length())
        {
            // Check if filename starts with "PowerToys." (case-insensitive)
            if (_wcsnicmp(filename.c_str(), powerToysPrefix.c_str(), powerToysPrefix.length()) == 0)
            {
                filename = filename.substr(powerToysPrefix.length());
            }
        }
        
        // Common PowerToys module naming patterns
        // Remove common suffixes if present
        const std::wstring suffixes[] = { L"Module", L"ModuleInterface", L"Interface" };
        for (const auto& suffix : suffixes)
        {
            if (filename.size() > suffix.size())
            {
                size_t pos = filename.rfind(suffix);
                if (pos != std::wstring::npos && pos + suffix.size() == filename.size())
                {
                    filename = filename.substr(0, pos);
                    break;
                }
            }
        }
        
        return filename;
    }
}

int wmain(int argc, wchar_t* argv[])
{
    std::wcout << L"PowerToys Module Loader v1.0\n";
    std::wcout << L"=============================\n\n";

    // Check if PowerToys.exe is running
    HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (hSnapshot != INVALID_HANDLE_VALUE)
    {
        PROCESSENTRY32W pe32;
        pe32.dwSize = sizeof(PROCESSENTRY32W);
        
        bool powerToysRunning = false;
        if (Process32FirstW(hSnapshot, &pe32))
        {
            do
            {
                if (_wcsicmp(pe32.szExeFile, L"PowerToys.exe") == 0)
                {
                    powerToysRunning = true;
                    break;
                }
            } while (Process32NextW(hSnapshot, &pe32));
        }
        CloseHandle(hSnapshot);

        if (powerToysRunning)
        {
            // Display warning with VT100 colors
            // Yellow background (43m), black text (30m), bold (1m)
            std::wcout << L"\033[1;43;30m WARNING \033[0m PowerToys.exe is currently running!\n\n";
            
            // Red text for important message
            std::wcout << L"\033[1;31m";
            std::wcout << L"Running ModuleLoader while PowerToys is active may cause conflicts:\n";
            std::wcout << L"  - Duplicate hotkey registrations\n";
            std::wcout << L"  - Conflicting module instances\n";
            std::wcout << L"  - Unexpected behavior\n";
            std::wcout << L"\033[0m\n"; // Reset color
            
            // Cyan text for recommendation
            std::wcout << L"\033[1;36m";
            std::wcout << L"RECOMMENDATION: Exit PowerToys before continuing.\n";
            std::wcout << L"\033[0m\n"; // Reset color
            
            // Yellow text for prompt
            std::wcout << L"\033[1;33m";
            std::wcout << L"Do you want to continue anyway? (y/N): ";
            std::wcout << L"\033[0m"; // Reset color
            
            wchar_t response = L'\0';
            std::wcin >> response;
            
            if (response != L'y' && response != L'Y')
            {
                std::wcout << L"\nExiting. Please close PowerToys and try again.\n";
                return 1;
            }
            
            std::wcout << L"\n";
        }
    }

    // Parse command-line arguments
    if (argc < 2)
    {
        std::wcerr << L"Error: Missing required argument <module_dll_path>\n\n";
        PrintUsage();
        return 1;
    }

    const std::wstring dllPath = argv[1];

    // Validate DLL exists
    if (!std::filesystem::exists(dllPath))
    {
        std::wcerr << L"Error: Module DLL not found: " << dllPath << L"\n";
        return 1;
    }

    std::wcout << L"Loading module: " << dllPath << L"\n";

    // Extract module name from DLL path
    std::wstring moduleName = ExtractModuleName(dllPath);
    std::wcout << L"Detected module name: " << moduleName << L"\n\n";

    try
    {
        // Load settings for the module
        std::wcout << L"Loading settings...\n";
        SettingsLoader settingsLoader;
        std::wstring settingsJson = settingsLoader.LoadSettings(moduleName, dllPath);
        
        if (settingsJson.empty())
        {
            std::wcerr << L"Error: Could not load settings for module '" << moduleName << L"'\n";
            std::wcerr << L"Expected location: %LOCALAPPDATA%\\Microsoft\\PowerToys\\" << moduleName << L"\\settings.json\n";
            return 1;
        }
        
        std::wcout << L"Settings loaded successfully.\n\n";

        // Load the module DLL
        std::wcout << L"Loading module DLL...\n";
        ModuleLoader moduleLoader;
        if (!moduleLoader.Load(dllPath))
        {
            std::wcerr << L"Error: Failed to load module DLL\n";
            return 1;
        }
        
        std::wcout << L"Module DLL loaded successfully.\n";
        std::wcout << L"Module key: " << moduleLoader.GetModuleKey() << L"\n";
        std::wcout << L"Module name: " << moduleLoader.GetModuleName() << L"\n\n";

        // Apply settings to the module
        std::wcout << L"Applying settings to module...\n";
        moduleLoader.SetConfig(settingsJson);
        std::wcout << L"Settings applied.\n\n";

        // Register hotkeys
        std::wcout << L"Registering module hotkeys...\n";
        HotkeyManager hotkeyManager;
        if (!hotkeyManager.RegisterModuleHotkeys(moduleLoader))
        {
            std::wcerr << L"Warning: Failed to register some hotkeys\n";
        }
        std::wcout << L"Hotkeys registered: " << hotkeyManager.GetRegisteredCount() << L"\n\n";

        // Enable the module
        std::wcout << L"Enabling module...\n";
        moduleLoader.Enable();
        std::wcout << L"Module enabled.\n\n";

        // Display status
        std::wcout << L"=============================\n";
        std::wcout << L"Module is now running!\n";
        std::wcout << L"=============================\n\n";
        std::wcout << L"Module Status:\n";
        std::wcout << L"  - Name: " << moduleLoader.GetModuleName() << L"\n";
        std::wcout << L"  - Key: " << moduleLoader.GetModuleKey() << L"\n";
        std::wcout << L"  - Enabled: " << (moduleLoader.IsEnabled() ? L"Yes" : L"No") << L"\n";
        std::wcout << L"  - Hotkeys: " << hotkeyManager.GetRegisteredCount() << L" registered\n\n";
        
        if (hotkeyManager.GetRegisteredCount() > 0)
        {
            std::wcout << L"Registered Hotkeys:\n";
            hotkeyManager.PrintHotkeys();
            std::wcout << L"\n";
        }

        std::wcout << L"Press Ctrl+C to exit.\n";
        std::wcout << L"You can press the module's hotkey to toggle its functionality.\n\n";

        // Run the message loop
        ConsoleHost consoleHost(moduleLoader, hotkeyManager);
        consoleHost.Run();

        // Cleanup
        std::wcout << L"\nShutting down...\n";
        moduleLoader.Disable();
        hotkeyManager.UnregisterAll();
        
        std::wcout << L"Module unloaded successfully.\n";
        return 0;
    }
    catch (const std::exception& ex)
    {
        std::wcerr << L"Fatal error: " << ex.what() << L"\n";
        return 1;
    }
}
