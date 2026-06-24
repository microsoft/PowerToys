// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <Windows.h>
#include <Tlhelp32.h>
#include <iostream>
#include <string>
#include <filesystem>
#include <vector>
#include <utility>
#include "ModuleLoader.h"
#include "SettingsLoader.h"
#include "HotkeyManager.h"
#include "ConsoleHost.h"

namespace
{
    void PrintUsage()
    {
        std::wcout << L"PowerToys Module Loader - Standalone utility for loading and testing PowerToy modules\n\n";
        std::wcout << L"Usage: ModuleLoader.exe <module_dll_path> [options]\n\n";
        std::wcout << L"Arguments:\n";
        std::wcout << L"  module_dll_path   Path to the PowerToy module DLL (e.g., CursorWrap.dll)\n\n";
        std::wcout << L"Options:\n";
        std::wcout << L"  --info            Display current module settings and exit\n";
        std::wcout << L"  --get <key>       Get a specific setting value and exit\n";
        std::wcout << L"  --set <key>=<val> Set a setting value (can be used multiple times)\n";
        std::wcout << L"  --no-run          Apply settings changes without running the module\n";
        std::wcout << L"  --help            Show this help message\n\n";
        std::wcout << L"Behavior:\n";
        std::wcout << L"  - Automatically discovers settings from %%LOCALAPPDATA%%\\Microsoft\\PowerToys\\<ModuleName>\\settings.json\n";
        std::wcout << L"  - Loads and enables the module\n";
        std::wcout << L"  - Registers module hotkeys\n";
        std::wcout << L"  - Runs until Ctrl+C is pressed\n\n";
        std::wcout << L"Examples:\n";
        std::wcout << L"  ModuleLoader.exe x64\\Debug\\modules\\CursorWrap.dll\n";
        std::wcout << L"  ModuleLoader.exe CursorWrap.dll --info\n";
        std::wcout << L"  ModuleLoader.exe CursorWrap.dll --get wrap_mode\n";
        std::wcout << L"  ModuleLoader.exe CursorWrap.dll --set wrap_mode=1\n";
        std::wcout << L"  ModuleLoader.exe CursorWrap.dll --set auto_activate=true --no-run\n\n";
        std::wcout << L"Notes:\n";
        std::wcout << L"  - Only non-UI modules are supported\n";
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

    struct CommandLineOptions
    {
        std::wstring dllPath;
        bool showInfo = false;
        bool showHelp = false;
        bool noRun = false;
        std::wstring getKey;
        std::vector<std::pair<std::wstring, std::wstring>> setValues;
    };

    CommandLineOptions ParseCommandLine(int argc, wchar_t* argv[])
    {
        CommandLineOptions options;
        
        for (int i = 1; i < argc; i++)
        {
            std::wstring arg = argv[i];
            
            if (arg == L"--help" || arg == L"-h" || arg == L"/?")
            {
                options.showHelp = true;
            }
            else if (arg == L"--info")
            {
                options.showInfo = true;
            }
            else if (arg == L"--no-run")
            {
                options.noRun = true;
            }
            else if (arg == L"--get" && i + 1 < argc)
            {
                options.getKey = argv[++i];
            }
            else if (arg == L"--set" && i + 1 < argc)
            {
                std::wstring setValue = argv[++i];
                size_t eqPos = setValue.find(L'=');
                if (eqPos != std::wstring::npos)
                {
                    std::wstring key = setValue.substr(0, eqPos);
                    std::wstring value = setValue.substr(eqPos + 1);
                    options.setValues.push_back({key, value});
                }
                else
                {
                    std::wcerr << L"Warning: Invalid --set format. Use --set key=value\n";
                }
            }
            else if (arg[0] != L'-' && options.dllPath.empty())
            {
                options.dllPath = arg;
            }
        }
        
        return options;
    }
}

int wmain(int argc, wchar_t* argv[])
{
    // Enable UTF-8 console output for box-drawing characters
    SetConsoleOutputCP(CP_UTF8);
    
    // Enable virtual terminal processing for ANSI escape codes (colors)
    HANDLE hOut = GetStdHandle(STD_OUTPUT_HANDLE);
    DWORD dwMode = 0;
    if (GetConsoleMode(hOut, &dwMode))
    {
        SetConsoleMode(hOut, dwMode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
    }

    std::wcout << L"PowerToys Module Loader v1.1\n";
    std::wcout << L"=============================\n\n";

    // Parse command-line arguments
    auto options = ParseCommandLine(argc, argv);

    if (options.showHelp)
    {
        PrintUsage();
        return 0;
    }

    if (options.dllPath.empty())
    {
        std::wcerr << L"Error: Missing required argument <module_dll_path>\n\n";
        PrintUsage();
        return 1;
    }

    // Validate DLL exists
    if (!std::filesystem::exists(options.dllPath))
    {
        std::wcerr << L"Error: Module DLL not found: " << options.dllPath << L"\n";
        return 1;
    }

    // Extract module name from DLL path
    std::wstring moduleName = ExtractModuleName(options.dllPath);
    
    // Create settings loader
    SettingsLoader settingsLoader;

    // Handle --info option
    if (options.showInfo)
    {
        settingsLoader.DisplaySettingsInfo(moduleName, options.dllPath);
        return 0;
    }

    // Handle --get option
    if (!options.getKey.empty())
    {
        std::wstring value = settingsLoader.GetSettingValue(moduleName, options.dllPath, options.getKey);
        if (value.empty())
        {
            std::wcerr << L"Setting '" << options.getKey << L"' not found.\n";
            return 1;
        }
        std::wcout << options.getKey << L"=" << value << L"\n";
        return 0;
    }

    // Handle --set options
    if (!options.setValues.empty())
    {
        bool allSuccess = true;
        for (const auto& [key, value] : options.setValues)
        {
            if (!settingsLoader.SetSettingValue(moduleName, options.dllPath, key, value))
            {
                allSuccess = false;
            }
        }
        
        if (options.noRun)
        {
            return allSuccess ? 0 : 1;
        }
        
        std::wcout << L"\n";
    }

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
            std::wcout << L"\033[1;43;30m WARNING \033[0m PowerToys.exe is currently running!\n\n";
            
            std::wcout << L"\033[1;31m";
            std::wcout << L"Running ModuleLoader while PowerToys is active may cause conflicts:\n";
            std::wcout << L"  - Duplicate hotkey registrations\n";
            std::wcout << L"  - Conflicting module instances\n";
            std::wcout << L"  - Unexpected behavior\n";
            std::wcout << L"\033[0m\n";
            
            std::wcout << L"\033[1;36m";
            std::wcout << L"RECOMMENDATION: Exit PowerToys before continuing.\n";
            std::wcout << L"\033[0m\n";
            
            std::wcout << L"\033[1;33m";
            std::wcout << L"Do you want to continue anyway? (y/N): ";
            std::wcout << L"\033[0m";
            
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

    std::wcout << L"Loading module: " << options.dllPath << L"\n";
    std::wcout << L"Detected module name: " << moduleName << L"\n\n";

    try
    {
        // Load settings for the module
        std::wcout << L"Loading settings...\n";
        std::wstring settingsJson = settingsLoader.LoadSettings(moduleName, options.dllPath);
        
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
        if (!moduleLoader.Load(options.dllPath))
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
