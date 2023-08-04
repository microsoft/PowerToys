#include "pch.h"
#include "SettingsWindow.h"
#include "OverlayWindow.h"
#include "CropAndLockWindow.h"
#include "ThumbnailCropAndLockWindow.h"
#include "ReparentCropAndLockWindow.h"
#include <common/interop/shared_constants.h>
#include <common/utils/winapi_error.h>
#include <common/utils/logger_helper.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include "ModuleConstants.h"

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
    // Initialize COM
    winrt::init_apartment(winrt::apartment_type::single_threaded);

    // Initialize logger automatic logging of exceptions.
    LoggerHelpers::init_logger(NonLocalizable::ModuleKey, L"", LogSettings::cropAndLockLoggerName);
    InitUnhandledExceptionHandler();

    // Before we do anything, check to see if we're already running. If we are,
    // the hotkey won't register and we'll fail. Instead, we should tell the user
    // to kill the other instance and exit this one.
    auto processes = GetAllProcesses();
    auto currentPid = GetCurrentProcessId();
    for (auto&& process : processes)
    {
        if (process.Name == L"CropAndLock.exe" && process.Pid != currentPid)
        {
            MessageBoxW(nullptr, L"CropAndLock is already open! Please close it first by going to the icon in the system tray.", L"CropAndLock", MB_ICONERROR);
            return 1;
        }
    }

    // NOTE: Reparenting a window with a different DPI context has consequences.
    //       See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent#remarks
    //       for more info.
    winrt::check_bool(SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2));

    // Create the DispatcherQueue that the compositor needs to run
    auto controller = util::CreateDispatcherQueueControllerForCurrentThread();

    // Create our settings window
    auto settingsWindow = SettingsWindow(L"CropAndLock Settings", 300, 225);

    // Create a tray icon
    auto instance = winrt::check_pointer(GetModuleHandleW(nullptr));
    wil::unique_hicon iconResource(winrt::check_pointer(LoadIconW(instance, MAIN_ICON)));
    auto trayIcon = util::TrayIcon(settingsWindow.m_window, iconResource.get(), SettingsWindow::TrayIconMessage, 0, L"CropAndLock");

    // Setup hot key
    auto hotKey = util::HotKey(MOD_CONTROL | MOD_SHIFT, 0x4C); // L 

    // Setup Composition
    auto compositor = winrt::Compositor();

    // Create our overlay window
    std::unique_ptr<OverlayWindow> overlayWindow;

    // Keep a list of our cropped windows
    std::vector<std::shared_ptr<CropAndLockWindow>> croppedWindows;

    // Handles and thread for the events sent from runner
    HANDLE m_reparent_event_handle;
    HANDLE m_thumbnail_event_handle;
    std::thread m_event_triggers_thread;
    bool m_running = true;

    std::function<void(HWND)> removeWindowCallback = [&](HWND windowHandle)
    {
        auto pos = std::find_if(croppedWindows.begin(), croppedWindows.end(), [windowHandle](auto window) { return window->Handle() == windowHandle; });
        if (pos != croppedWindows.end())
        {
            croppedWindows.erase(pos);
        }
    };

    std::function<void(CropAndLockType)> ProcessCommand = [&](CropAndLockType mode)
    {
        std::function<void(HWND, RECT)> windowCroppedCallback = [&, mode](HWND targetWindow, RECT cropRect) {
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
            switch (mode)
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
    };

    // Start a thread to listen on the events.
    m_reparent_event_handle = CreateEventW(nullptr, false, false, CommonSharedConstants::CROP_AND_LOCK_REPARENT_EVENT);
    m_thumbnail_event_handle = CreateEventW(nullptr, false, false, CommonSharedConstants::CROP_AND_LOCK_THUMBNAIL_EVENT);
    if (!m_reparent_event_handle || !m_reparent_event_handle)
    {
        Logger::warn(L"Failed to create events. {}", get_last_error_or_default(GetLastError()));
        return 1;
    }

    m_event_triggers_thread = std::thread([&]() {
        MSG msg;
        HANDLE event_handles[2] = {m_reparent_event_handle, m_thumbnail_event_handle};
        while (m_running)
        {
            DWORD dwEvt = MsgWaitForMultipleObjects(2, event_handles, false, INFINITE, QS_ALLINPUT);
            if (!m_running)
            {
                break;
            }
            switch (dwEvt)
            {
            case WAIT_OBJECT_0:
            {
                bool enqueueSucceeded = controller.DispatcherQueue().TryEnqueue([&]() {
                    ProcessCommand(CropAndLockType::Reparent);
                });
                if (!enqueueSucceeded)
                {
                    Logger::error("Couldn't enqueue message to reparent a window.");
                }
                break;
            }
            case WAIT_OBJECT_0 + 1:
            {
                bool enqueueSucceeded = controller.DispatcherQueue().TryEnqueue([&]() {
                    ProcessCommand(CropAndLockType::Thumbnail);
                });
                if (!enqueueSucceeded)
                {
                    Logger::error("Couldn't enqueue message to thumbnail a window.");
                }
                break;
            }
            case WAIT_OBJECT_0 + 2:
                if (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE))
                {
                    TranslateMessage(&msg);
                    DispatchMessageW(&msg);
                }
                break;
            default:
                break;
            }
        }
    });

    // Message pump
    MSG msg = {};
    while (GetMessageW(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    m_running = false;
    // Needed to unblock MsgWaitForMultipleObjects one last time
    SetEvent(m_reparent_event_handle);
    CloseHandle(m_reparent_event_handle);
    SetEvent(m_thumbnail_event_handle);
    CloseHandle(m_thumbnail_event_handle);
    m_event_triggers_thread.join();

    return util::ShutdownDispatcherQueueControllerAndWait(controller, static_cast<int>(msg.wParam));
}
