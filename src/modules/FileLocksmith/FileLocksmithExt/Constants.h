#pragma once

#include "pch.h"

// Non-localizable constants
namespace constants::nonlocalizable
{
    // Description of the registry key
    constexpr WCHAR RegistryKeyDescription[] = L"File Locksmith Shell Extension";

    // File name of the UI executable
    constexpr WCHAR FileNameUIExe[] = L"PowerToys.FileLocksmithUI.exe";

    // String key used by PowerToys
    constexpr WCHAR PowerToyKey[] = L"File Locksmith";

    // Nonlocalized name of this PowerToy, for logs, etc
    constexpr WCHAR PowerToyName[] = L"File Locksmith";

    // JSON key used to store whether the module is enabled
    constexpr WCHAR JsonKeyEnabled[] = L"Enabled";

    // Path of the JSON file used to store settings
    constexpr WCHAR DataFilePath[] = L"\\file-locksmith-settings.json";

    // Name of the file where the list of files to checked will be stored
    constexpr WCHAR LastRunPath[] = L"\\last-run.log";
}

// Macros, non-localizable
 
// Description of the registry key
#define REGISTRY_CONTEXT_MENU_KEY  L"FileLocksmithExt"
