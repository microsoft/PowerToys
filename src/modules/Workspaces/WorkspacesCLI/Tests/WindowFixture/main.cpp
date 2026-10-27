// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <string_view>
#include <string>

namespace
{
    LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam)
    {
        if (message == WM_TIMER || message == WM_CLOSE)
        {
            DestroyWindow(window);
            return 0;
        }
        if (message == WM_DESTROY)
        {
            PostQuitMessage(0);
            return 0;
        }
        return DefWindowProcW(window, message, wparam, lparam);
    }
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR arguments, int)
{
    const std::wstring_view command(arguments);
    if (command.starts_with(L"--delay-window "))
    {
        const auto name = std::wstring(command.substr(std::wstring_view(L"--delay-window ").size()));
        const auto started = OpenEventW(EVENT_MODIFY_STATE, FALSE, name.c_str());
        if (!started)
            return 1;
        SetEvent(started);
        CloseHandle(started);
        Sleep(3000);
    }
    WNDCLASSW type{};
    type.hInstance = instance;
    type.lpfnWndProc = WindowProc;
    type.lpszClassName = L"WorkspacesCliIsolatedFixture";
    type.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    if (!RegisterClassW(&type))
        return 1;
    const auto window = CreateWindowW(type.lpszClassName, L"Workspaces CLI isolated test window", WS_OVERLAPPEDWINDOW, 500, 350, 640, 400, nullptr, nullptr, instance, nullptr);
    if (!window)
        return 1;
    SetTimer(window, 1, 60000, nullptr);
    ShowWindow(window, SW_SHOWNOACTIVATE);
    MSG message{};
    while (GetMessageW(&message, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }
    return 0;
}
