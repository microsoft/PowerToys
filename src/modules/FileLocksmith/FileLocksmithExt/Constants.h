#pragma once

#include "pch.h"

// Localizable constants, these should be converted to resources
namespace constants::localizable
{
	// Text shown in the context menu
	constexpr WCHAR CommandTitle[] = L"What's using this file?";

	// Localized name of this PowerToy
    constexpr WCHAR PowerToyName[] = L"File Locksmith";
}

// Non-localizable constants
namespace constants::nonlocalizable
{
	// Description of the registry key
	constexpr WCHAR RegistryKeyDescription[] = L"File Locksmith Shell Extension";

	// File name of the UI executable
	constexpr WCHAR FileNameUIExe[] = L"PowerToys.FileLocksmithGUI.exe";

	// String key used by PowerToys
    constexpr WCHAR PowerToyKey[] = L"File Locksmith";

	// Nonlocalized name of this PowerToy, for logs, etc
    constexpr WCHAR PowerToyName[] = L"File Locksmith";

	// JSON key used to store whether the module is enabled
    constexpr WCHAR JsonKeyEnabled[] = L"Enabled";

	// Path of the JSON file used to store settings
    constexpr WCHAR DataFilePath[] = L"\\file-locksmith-settings.json";
}

// Macros, non-localizable
 
// Description of the registry key
#define REGISTRY_CONTEXT_MENU_KEY  L"FileLocksmithExt"
