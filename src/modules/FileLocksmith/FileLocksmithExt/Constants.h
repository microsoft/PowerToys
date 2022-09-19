#pragma once

#include "pch.h"

// Localizable constants, these should be converted to resources
namespace constants::localizable
{
	// Text shown in the context menu
	constexpr WCHAR CommandTitle[] = L"What's using this file?";
}

// Non-localizable constants
namespace constants::nonlocalizable
{
	// Description of the registry key
	constexpr WCHAR RegistryKeyDescription[] = L"File Locksmith Shell Extension";

	// File name of the DLL
	constexpr WCHAR FileNameDLL[] = L"ContextMenuEntry.dll";

	// File name of the UI executable
	constexpr WCHAR FileNameUIExe[] = L"FileLocksmithGUI\\FileLocksmithGUI.exe";
}

// Macros, non-localizable
 
// Description of the registry key
#define REGISTRY_CONTEXT_MENU_KEY  L"FileLocksmithExt"
