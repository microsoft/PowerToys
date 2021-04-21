#pragma once

#include "pch.h"

#include <ErrorTypes.h>

namespace KeyboardManagerEditorStrings
{
    // String constant for the default app name in Remap shortcuts
    inline const std::wstring DefaultAppName = GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_ALLAPPS);
        
    // Function to return the error message
    winrt::hstring GetErrorMessage(KeyboardManagerHelper::ErrorType errorType);
}