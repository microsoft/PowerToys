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
#include <common/utils/gpo.h>
#include "ModuleConstants.h"
#include <common/utils/ProcessWaiter.h>
#include "trace.h"

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

const std::wstring instanceMutexName = L"Local\\PowerToys_CropAndLock_InstanceMutex";
bool m_running = true;

int WINAPI wWinMain(_In_ HINSTANCE, _In_opt_ HINSTANCE, _In_ PWSTR lpCmdLine, _In_ int)
{
    // Initialize COM
    winrt::init_apartment(winrt::apartment_type::single_threaded);

    // Initialize logger automatic logging of exceptions.
    LoggerHelpers::init_logger(NonLocalizable::ModuleKey, L"", LogSettings::cropAndLockLoggerName);
    InitUnhandledExceptionHandler();

    if (powertoys_gpo::getConfiguredCropAndLockEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        Logger::warn(L"Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
        return 0;
    }

    // Before we do anything, check to see if we're already running. If we are,
    // the hotkey won't register and we'll fail. Instead, we should tell the user
    // to kill the other instance and exit this one.
    auto mutex = CreateMutex(nullptr, true, instanceMutexName.c_str());
    if (mutex == nullptr)
    {
        Logger::error(L"Failed to create mutex. {}", get_last_error_or_default(GetLastError()));
    }

    if (GetLastError() == ERROR_ALREADY_EXISTS)
    {
        // CropAndLock is already open.
        return 1;
    }

    std::wstring pid = std::wstring(lpCmdLine);
    if (pid.empty())
    {
        Logger::warn(L"Tried to run Crop And Lock as a standalone.");
        MessageBoxW(nullptr, L"CropAndLock can't run as a standalone. Start it from PowerToys.", L"CropAndLock", MB_ICONERROR);
        return 1;
    }

    auto mainThreadId = GetCurrentThreadId();
    ProcessWaiter::OnProcessTerminate(pid, [mainThreadId](int err) {
        if (err != ERROR_SUCCESS)
        {
            Logger::error(L"Failed to wait for parent process exit. {}", get_last_error_or_default(err));
        }
        else
        {
            Logger::trace(L"PowerToys runner exited.");
        }

        Logger::trace(L"Exiting CropAndLock");
        PostThreadMessage(mainThreadId, WM_QUIT, 0, 0);
    });

    // NOTE: reparenting a window with a different DPI context has consequences.
    //       See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent#remarks
    //       for more info.
    winrt::check_bool(SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2));

    // Create the DispatcherQueue that the compositor needs to run
    auto controller = util::CreateDispatcherQueueControllerForCurrentThread();

    // Setup Composition
    auto compositor = winrt::Compositor();

    // Create our overlay window
    std::unique_ptr<OverlayWindow> overlayWindow;

    // Keep a list of our cropped windows
    std::vector<std::shared_ptr<CropAndLockWindow>> croppedWindows;

    // Handles and thread for the events sent from runner
    HANDLE m_reparent_event_handle;
    HANDLE m_thumbnail_event_handle;
    HANDLE m_exit_event_handle;
    std::thread m_event_triggers_thread;

    std::function<void(HWND)> removeWindowCallback = [&](HWND windowHandle)
    {
        if (!m_running)
        {
            // If we're not running, the reference to croppedWindows might no longer be valid and cause a crash at exit time, due to being called by destructors after wWinMain returns.
            return;
        }

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
                Logger::trace(L"Creating a reparent window");
                Trace::CropAndLock::CreateReparentWindow();
                break;
            case CropAndLockType::Thumbnail:
                croppedWindow = std::make_shared<ThumbnailCropAndLockWindow>(title, 800, 600);
                Logger::trace(L"Creating a thumbnail window");
                Trace::CropAndLock::CreateThumbnailWindow();
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
    m_exit_event_handle = CreateEventW(nullptr, false, false, CommonSharedConstants::CROP_AND_LOCK_EXIT_EVENT);
    if (!m_reparent_event_handle || !m_thumbnail_event_handle || !m_exit_event_handle)
    {
        Logger::warn(L"Failed to create events. {}", get_last_error_or_default(GetLastError()));
        return 1;
    }

    m_event_triggers_thread = std::thread([&]() {
        MSG msg;
        HANDLE event_handles[3] = {m_reparent_event_handle, m_thumbnail_event_handle, m_exit_event_handle};
        while (m_running)
        {
            DWORD dwEvt = MsgWaitForMultipleObjects(3, event_handles, false, INFINITE, QS_ALLINPUT);
            if (!m_running)
            {
                break;
            }
            switch (dwEvt)
            {
            case WAIT_OBJECT_0:
            {
                // Reparent Event
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
                // Thumbnail Event
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
            {
                // Exit Event
                Logger::trace(L"Received an exit event.");
                PostThreadMessage(mainThreadId, WM_QUIT, 0, 0);
                break;
            }
            case WAIT_OBJECT_0 + 3:
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
    CloseHandle(m_thumbnail_event_handle);
    CloseHandle(m_exit_event_handle);
    m_event_triggers_thread.join();

    return util::ShutdownDispatcherQueueControllerAndWait(controller, static_cast<int>(msg.wParam));
}
