#pragma once

#include "pch.h"

#include "ShortcutErrorType.h"

namespace KeyboardManagerEditorStrings
{
    // String constant for the default app name in Remap shortcuts
    inline std::wstring DefaultAppName()
    {
        return GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_ALLAPPS);
    }

    inline std::wstring MappingTypeText()
    {
        return GET_RESOURCE_STRING(IDS_MAPPING_TYPE_DROPDOWN_TEXT);
    }

    inline std::wstring MappingTypeShortcut()
    {
        return GET_RESOURCE_STRING(IDS_MAPPING_TYPE_DROPDOWN_SHORTCUT);
    }

    inline std::wstring MappingTypeKey()
    {
        return GET_RESOURCE_STRING(IDS_MAPPING_TYPE_DROPDOWN_KEY);
    }

    // Function to return the error message
    winrt::hstring GetErrorMessage(ShortcutErrorType errorType);
}
