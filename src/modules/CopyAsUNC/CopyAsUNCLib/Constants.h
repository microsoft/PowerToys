#pragma once

#include "pch.h"

// Non-localizable constants
namespace constants::nonlocalizable
{
    // String key used by PowerToys runner
    constexpr WCHAR PowerToyKey[] = L"Copy as UNC";

    // Nonlocalized name of this PowerToy, for logs, etc.
    constexpr WCHAR PowerToyName[] = L"CopyAsUNC";

    // JSON key used to store extended menu enabled
    constexpr WCHAR JsonKeyShowInExtendedContextMenu[] = L"showInExtendedContextMenu";

    // Path of the JSON file used to store settings
    constexpr WCHAR DataFilePath[] = L"\\copy-as-unc-settings.json";

    // Name of the tier 1 context menu package
    constexpr WCHAR ContextMenuPackageName[] = L"CopyAsUNCContextMenu";
}
