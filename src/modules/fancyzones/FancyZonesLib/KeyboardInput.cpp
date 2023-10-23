#include "pch.h"
#include "KeyboardInput.h"

#include <hidusage.h>

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

bool KeyboardInput::Initialize(HWND window)
{
    RAWINPUTDEVICE inputDevice{};
    inputDevice.usUsagePage = HID_USAGE_PAGE_GENERIC;
    inputDevice.usUsage = HID_USAGE_GENERIC_KEYBOARD;
    inputDevice.dwFlags = RIDEV_INPUTSINK;
    inputDevice.hwndTarget = window;

    bool res = RegisterRawInputDevices(&inputDevice, 1, sizeof(inputDevice));
    if (!res)
    {
        Logger::error(L"RegisterRawInputDevices error: {}", get_last_error_or_default(GetLastError()));
    }

    return res;
}
