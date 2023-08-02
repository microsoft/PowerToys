#include "pch.h"
#include "SettingsWindow.h"
#include "OverlayWindow.h"
#include "CropAndLockWindow.h"
#include "ThumbnailCropAndLockWindow.h"
#include "ReparentCropAndLockWindow.h"

#pragma comment(linker,"/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")

namespace winrt
{
    using namespace Windows::Foundation;
    using namespace Windows::Foundation::Numerics;
    using namespace Windows::UI;
    using namespace Windows::UI::Composition;
}

namespace util
{
    using namespace robmikh::common::desktop;
}

int __stdcall WinMain(HINSTANCE, HINSTANCE, PSTR, int)
{
    // Before we do anything, check to see if we're already running. If we are,
    // the hotkey won't register and we'll fail. Instead, we should tell the user
    // to kill the other instance and exit this one.
    auto processes = GetAllProcesses();
    auto currentPid = GetCurrentProcessId();
    for (auto&& process : processes)
    {
        if (process.Name == L"CropAndLockDemo.exe" && process.Pid != currentPid)
        {
            MessageBoxW(nullptr, L"CropAndLockDemo is already open! Please close it first by going to the icon in the system tray.", L"CropAndLockDemo", MB_ICONERROR);
            return 1;
        }
    }

    // NOTE: Reparenting a window with a different DPI context has consequences.
    //       See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent#remarks
    //       for more info.
    winrt::check_bool(SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2));

    // Initialize COM
    winrt::init_apartment(winrt::apartment_type::single_threaded);

    // Create the DispatcherQueue that the compositor needs to run
    auto controller = util::CreateDispatcherQueueControllerForCurrentThread();

    // Create our settings window
    auto settingsWindow = SettingsWindow(L"CropAndLockDemo Settings", 300, 225);

    // Create a tray icon
    auto instance = winrt::check_pointer(GetModuleHandleW(nullptr));
    wil::unique_hicon iconResource(winrt::check_pointer(LoadIconW(instance, MAIN_ICON)));
    auto trayIcon = util::TrayIcon(settingsWindow.m_window, iconResource.get(), SettingsWindow::TrayIconMessage, 0, L"CropAndLockDemo");

    // Setup hot key
    auto hotKey = util::HotKey(MOD_CONTROL | MOD_SHIFT, 0x4C); // L 

    // Setup Composition
    auto compositor = winrt::Compositor();

    // Create our overlay window
    std::unique_ptr<OverlayWindow> overlayWindow;

    // Keep a list of our cropped windows
    std::vector<std::shared_ptr<CropAndLockWindow>> croppedWindows;

    std::function<void(HWND)> removeWindowCallback = [&](HWND windowHandle)
    {
        auto pos = std::find_if(croppedWindows.begin(), croppedWindows.end(), [windowHandle](auto window) { return window->Handle() == windowHandle; });
        if (pos != croppedWindows.end())
        {
            croppedWindows.erase(pos);
        }
    };
    std::function<void(HWND, RECT)> windowCroppedCallback = [&](HWND targetWindow, RECT cropRect)
        {
            auto targetInfo = util::WindowInfo(targetWindow);
            // TODO: Fix WindowInfo.h to not contain the null char at the end.
            auto nullCharIndex = std::wstring::npos;
            do
            {
                nullCharIndex = targetInfo.Title.rfind(L'\0');
                if (nullCharIndex != std::wstring::npos)
                {
                    targetInfo.Title.erase(nullCharIndex);
                }
            } while (nullCharIndex != std::wstring::npos);
            
            std::wstringstream titleStream;
            titleStream << targetInfo.Title << L" (Cropped)";
            auto title = titleStream.str();

            std::shared_ptr<CropAndLockWindow> croppedWindow;
            auto type = settingsWindow.GetCropAndLockType();
            switch (type)
            {
            case CropAndLockType::Reparent:
                croppedWindow = std::make_shared<ReparentCropAndLockWindow>(title, 800, 600);
                break;
            case CropAndLockType::Thumbnail:
                croppedWindow = std::make_shared<ThumbnailCropAndLockWindow>(title, 800, 600);
                break;
            default:
                return;
            }
            croppedWindow->CropAndLock(targetWindow, cropRect);
            croppedWindow->OnClosed(removeWindowCallback);
            croppedWindows.push_back(croppedWindow);
        };

    // Message pump
    MSG msg = {};
    while (GetMessageW(&msg, nullptr, 0, 0))
    {
        if (msg.message == WM_HOTKEY)
        {
            overlayWindow.reset();

            // Get the current window with focus
            auto foregroundWindow = GetForegroundWindow();
            if (foregroundWindow != nullptr)
            {
                bool match = false;
                for (auto&& croppedWindow : croppedWindows)
                {
                    if (foregroundWindow == croppedWindow->Handle())
                    {
                        match = true;
                        break;
                    }
                }
                if (!match)
                {
                    overlayWindow = std::make_unique<OverlayWindow>(compositor, foregroundWindow, windowCroppedCallback);
                }
            }
        }
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
    return util::ShutdownDispatcherQueueControllerAndWait(controller, static_cast<int>(msg.wParam));
}
