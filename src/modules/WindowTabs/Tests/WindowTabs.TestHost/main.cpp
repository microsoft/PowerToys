// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <roapi.h>
#include <commctrl.h>
#include <filesystem>
#include <stdexcept>
#include <string_view>
#include <interface/powertoy_module_interface.h>

namespace
{
    PowertoyModuleIface* module = nullptr;

    LRESULT CALLBACK SampleProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam)
    {
        if (message == WM_CREATE)
        {
            CreateWindowW(L"STATIC", L"Window Tabs sample window\r\n\r\nThis is a disposable test window. No user documents are loaded.\r\n\r\nHold Shift while dragging its title bar onto the other sample window to group them.", WS_CHILD | WS_VISIBLE, 28, 28, 550, 150, window, nullptr, reinterpret_cast<LPCREATESTRUCTW>(lparam)->hInstance, nullptr);
            return 0;
        }
        if (message == WM_DESTROY)
        {
            PostQuitMessage(0);
            return 0;
        }
        return DefWindowProcW(window, message, wparam, lparam);
    }

    int RunSample(HINSTANCE instance, bool notes, int show)
    {
        WNDCLASSW type{};
        type.lpfnWndProc = SampleProc;
        type.hInstance = instance;
        type.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        type.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
        type.lpszClassName = L"PowerToys.WindowTabs.Sample";
        RegisterClassW(&type);
        const auto window = CreateWindowW(type.lpszClassName, notes ? L"Window Tabs sample - Notes" : L"Window Tabs sample - Research", WS_OVERLAPPEDWINDOW, notes ? 80 : 780, 180, 660, 440, nullptr, nullptr, instance, nullptr);
        if (!window)
            return 1;
        ShowWindow(window, show);
        MSG message{};
        while (GetMessageW(&message, nullptr, 0, 0) > 0)
        {
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }
        return 0;
    }

    LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam)
    {
        if (message == WM_CREATE)
        {
            const auto instance = reinterpret_cast<LPCREATESTRUCTW>(lparam)->hInstance;
            CreateWindowW(L"STATIC",
                          L"Window Tabs is running.\r\n\r\n"
                          L"Open two resizable application windows. Hold Shift when starting a window drag, "
                          L"then drop its title bar on the other window's title bar.\r\n\r\n"
                          L"Click tabs to switch. Drag tabs to reorder, or drag out of the strip to detach. "
                          L"Right-click a tab for detach, close and ungroup actions.\r\n\r\n"
                          L"Next / previous tab: Win+Ctrl+PageDown / PageUp.\r\n\r\n"
                          L"Close this launcher to stop Window Tabs and restore the application windows.",
                          WS_CHILD | WS_VISIBLE,
                          20,
                          20,
                          500,
                          235,
                          window,
                          nullptr,
                          instance,
                          nullptr);
            CreateWindowW(L"BUTTON", L"Stop Window Tabs", WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_DEFPUSHBUTTON, 20, 270, 180, 34, window, reinterpret_cast<HMENU>(1), instance, nullptr);
            RegisterHotKey(window, 0, MOD_WIN | MOD_CONTROL | MOD_NOREPEAT, VK_NEXT);
            RegisterHotKey(window, 1, MOD_WIN | MOD_CONTROL | MOD_NOREPEAT, VK_PRIOR);
            return 0;
        }
        if (message == WM_HOTKEY && module)
        {
            module->on_hotkey(static_cast<size_t>(wparam));
            return 0;
        }
        if (message == WM_COMMAND && LOWORD(wparam) == 1)
        {
            PostMessageW(window, WM_CLOSE, 0, 0);
            return 0;
        }
        if (message == WM_DESTROY)
        {
            UnregisterHotKey(window, 0);
            UnregisterHotKey(window, 1);
            PostQuitMessage(0);
            return 0;
        }
        return DefWindowProcW(window, message, wparam, lparam);
    }
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR commandLine, int show)
{
    if (std::wstring_view(commandLine) == L"--sample-notes")
        return RunSample(instance, true, show);
    if (std::wstring_view(commandLine) == L"--sample-research")
        return RunSample(instance, false, show);
    const auto initialized = RoInitialize(RO_INIT_SINGLETHREADED);
    if (FAILED(initialized))
        return 1;
    HMODULE library = nullptr;
    int result = 1;
    try
    {
        wchar_t executable[32768]{};
        GetModuleFileNameW(nullptr, executable, ARRAYSIZE(executable));
        const auto path = std::filesystem::path(executable).parent_path().parent_path().parent_path() / L"PowerToys.WindowTabs.dll";
        library = LoadLibraryExW(path.c_str(), nullptr, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        if (!library)
            throw std::runtime_error("Cannot load PowerToys.WindowTabs.dll");
        using CreateModule = PowertoyModuleIface*(__cdecl*)();
        const auto create = reinterpret_cast<CreateModule>(GetProcAddress(library, "powertoy_create"));
        if (!create || !(module = create()))
            throw std::runtime_error("Cannot create Window Tabs module");
        module->enable();
        if (!module->is_enabled())
            throw std::runtime_error("Window Tabs service did not start; inspect its local logs");
        if (std::wstring_view(commandLine) == L"--smoke-test")
        {
            result = 0;
        }
        else
        {
            WNDCLASSW type{};
            type.lpfnWndProc = WindowProc;
            type.hInstance = instance;
            type.hCursor = LoadCursorW(nullptr, IDC_ARROW);
            type.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
            type.lpszClassName = L"PowerToys.WindowTabs.TestHost";
            RegisterClassW(&type);
            const auto window = CreateWindowW(type.lpszClassName, L"PowerToys Window Tabs — local test", WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX, CW_USEDEFAULT, CW_USEDEFAULT, 570, 370, nullptr, nullptr, instance, nullptr);
            if (!window)
                throw std::runtime_error("Cannot create the local test launcher");
            ShowWindow(window, show);
            MSG message{};
            while (GetMessageW(&message, nullptr, 0, 0) > 0)
            {
                TranslateMessage(&message);
                DispatchMessageW(&message);
            }
            result = 0;
        }
    }
    catch (const std::exception& error)
    {
        if (std::wstring_view(commandLine) != L"--smoke-test")
            MessageBoxA(nullptr, error.what(), "Window Tabs local test", MB_OK | MB_ICONERROR);
    }
    if (module)
    {
        module->destroy();
        module = nullptr;
    }
    if (library)
        FreeLibrary(library);
    RoUninitialize();
    return result;
}
