// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <array>
#include <string>

namespace
{
    std::wstring message;

    std::wstring ElevationDescription()
    {
        HANDLE token = nullptr;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token))
        {
            return L"Unavailable (Windows error " + std::to_wstring(GetLastError()) + L")";
        }

        TOKEN_ELEVATION elevation{};
        DWORD size = 0;
        const BOOL success = GetTokenInformation(token, TokenElevation, &elevation, sizeof(elevation), &size);
        const DWORD error = success ? ERROR_SUCCESS : GetLastError();
        CloseHandle(token);
        if (!success)
        {
            return L"Unavailable (Windows error " + std::to_wstring(error) + L")";
        }
        return elevation.TokenIsElevated ? L"Administrator" : L"Not elevated";
    }

    LRESULT CALLBACK WindowProcedure(HWND window, UINT event, WPARAM wparam, LPARAM lparam)
    {
        switch (event)
        {
        case WM_PAINT:
        {
            PAINTSTRUCT paint{};
            const HDC context = BeginPaint(window, &paint);
            RECT bounds{};
            GetClientRect(window, &bounds);
            InflateRect(&bounds, -20, -20);
            const HGDIOBJ oldFont = SelectObject(context, GetStockObject(DEFAULT_GUI_FONT));
            SetBkMode(context, TRANSPARENT);
            SetTextColor(context, GetSysColor(COLOR_WINDOWTEXT));
            DrawTextW(context, message.data(), static_cast<int>(message.size()), &bounds, DT_LEFT | DT_WORDBREAK | DT_NOPREFIX);
            SelectObject(context, oldFont);
            EndPaint(window, &paint);
            return 0;
        }
        case WM_DESTROY:
            PostQuitMessage(0);
            return 0;
        default:
            return DefWindowProcW(window, event, wparam, lparam);
        }
    }
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int showCommand)
{
    std::array<wchar_t, 32768> path{};
    const DWORD length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
    if (length == 0 || length >= path.size())
    {
        MessageBoxW(nullptr, L"Unable to read the fixture's executable name.", L"Signature fixture error", MB_OK | MB_ICONERROR);
        return 1;
    }

    const std::wstring fullPath(path.data(), length);
    const std::wstring fileName = fullPath.substr(fullPath.find_last_of(L'\\') + 1);
    const std::wstring title = fileName + L" - harmless signature test";
    message = L"Harmless PowerToys signature-test application.\r\n\r\n"
              L"File: " + fileName + L"\r\n"
              L"Elevation: " + ElevationDescription() + L"\r\n"
              L"Process ID: " + std::to_wstring(GetCurrentProcessId()) + L"\r\n\r\n"
              L"This app only displays this window. It does not write files or registry settings, "
              L"use the network, or start other processes.\r\n\r\n"
              L"Signature inspection does not require running this app or granting administrator privileges.\r\n\r\n"
              L"Close this window when finished.";

    WNDCLASSEXW windowClass{};
    windowClass.cbSize = sizeof(windowClass);
    windowClass.style = CS_HREDRAW | CS_VREDRAW;
    windowClass.lpfnWndProc = WindowProcedure;
    windowClass.hInstance = instance;
    windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    windowClass.hbrBackground = reinterpret_cast<HBRUSH>(static_cast<INT_PTR>(COLOR_WINDOW + 1));
    windowClass.lpszClassName = L"PowerToysHarmlessSignatureFixture";
    if (!RegisterClassExW(&windowClass))
    {
        MessageBoxW(nullptr, L"Unable to register the test window.", L"Signature fixture error", MB_OK | MB_ICONERROR);
        return 1;
    }

    const HWND window = CreateWindowExW(0, windowClass.lpszClassName, title.c_str(), WS_OVERLAPPEDWINDOW,
                                       CW_USEDEFAULT, CW_USEDEFAULT, 640, 380, nullptr, nullptr, instance, nullptr);
    if (!window)
    {
        MessageBoxW(nullptr, L"Unable to create the test window.", L"Signature fixture error", MB_OK | MB_ICONERROR);
        return 1;
    }
    ShowWindow(window, showCommand);
    UpdateWindow(window);

    MSG event{};
    for (;;)
    {
        const BOOL result = GetMessageW(&event, nullptr, 0, 0);
        if (result == -1)
        {
            MessageBoxW(window, L"Unable to read a window message.", L"Signature fixture error", MB_OK | MB_ICONERROR);
            return 1;
        }
        if (result == 0)
        {
            return static_cast<int>(event.wParam);
        }
        TranslateMessage(&event);
        DispatchMessageW(&event);
    }
}
