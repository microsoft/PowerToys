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
        
    // Function to return the error message
    winrt::hstring GetErrorMessage(ShortcutErrorType errorType);
}
