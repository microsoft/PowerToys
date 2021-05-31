#include "pch.h"

#include <common/utils/window.h>
#include <common/utils/UnhandledExceptionHandler_x64.h>

#include <FancyZonesLib/trace.h>
#include <FancyZonesLib/Generated Files/resource.h>

#include <common/utils/logger_helper.h>
#include <common/hooks/LowlevelKeyboardEvent.h>
#include <common/utils/resources.h>

#include <FancyZonesLib/FancyZones.h>
#include <FancyZonesLib/FancyZonesData.h>
#include <FancyZonesLib/FancyZonesWinHookEventIDs.h>

#include <FancyZonesApp.h>

// Non-localizable
const std::wstring moduleName = L"FancyZones";
const std::wstring internalPath = L"";

int WINAPI wWinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ PWSTR lpCmdLine, _In_ int nCmdShow)
{
    winrt::init_apartment();
    InitUnhandledExceptionHandler_x64();

    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::fancyZonesLoggerName);

    Trace::RegisterProvider();

    FancyZonesApp app(GET_RESOURCE_STRING(IDS_FANCYZONES), NonLocalizable::FancyZonesStr);
    app.Run();

    run_message_loop();

    Trace::UnregisterProvider();
    
    return 0;
}
