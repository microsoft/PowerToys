#include "pch.h"

#include <common/utils/logger_helper.h>
#include <common/utils/UnhandledExceptionHandler_x64.h>

#include <lib/trace.h>

// Non-locaizable
const std::wstring moduleName = L"FancyZones";
const std::wstring internalPath = L"Engine";

int WINAPI wWinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ PWSTR lpCmdLine, _In_ int nCmdShow)
{
    winrt::init_apartment();
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::fancyZonesLoggerName);
    
    Trace::RegisterProvider();

    Trace::UnregisterProvider();
    
    return 0;
}
