#include "pch.h"
#include "KeyboardInput.h"

#include <hidUsage.h>

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

std::optional<KeyboardInput::Key> KeyboardInput::OnKeyboardInput(HRAWINPUT hInput)
{
    RAWINPUT input;
    UINT size = sizeof(input);
    auto result = GetRawInputData(hInput, RID_INPUT, &input, &size, sizeof(RAWINPUTHEADER));
    if (result < sizeof(RAWINPUTHEADER))
    {
        return std::nullopt;
    }

    if (input.header.dwType == RIM_TYPEKEYBOARD)
    {
        bool pressed = (input.data.keyboard.Flags & RI_KEY_BREAK) == 0;
        return KeyboardInput::Key{ input.data.keyboard.VKey, pressed };
    }

    return std::nullopt;
}