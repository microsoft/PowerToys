#pragma once
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.System.h>
#include <DispatcherQueue.h>

// This file is added as a workaround for: https://github.com/microsoft/cppwinrt/issues/1391
// robmikh.common needs to be updated to support newer versions of C++/WinRT https://github.com/robmikh/robmikh.common/issues/2
// Applying workaround from https://github.com/robmikh/Win32CaptureSample/commit/fc758e343ca886795b05af5003d9a3bb85ff4da2

namespace robmikh::common::desktop
{
    namespace impl
    {
        inline void ShutdownAndThenPostQuitMessage(winrt::Windows::System::DispatcherQueueController const& controller, int exitCode)
        {
            auto action = controller.ShutdownQueueAsync();
            action.Completed([exitCode](auto&&, auto&&)
                {
                    PostQuitMessage(exitCode);
                });
        }
    }

    inline auto CreateDispatcherQueueControllerForCurrentThread()
    {
        namespace abi = ABI::Windows::System;

        DispatcherQueueOptions options
        {
            sizeof(DispatcherQueueOptions),
            DQTYPE_THREAD_CURRENT,
            DQTAT_COM_NONE
        };

        winrt::Windows::System::DispatcherQueueController controller{ nullptr };
        winrt::check_hresult(CreateDispatcherQueueController(options, reinterpret_cast<abi::IDispatcherQueueController**>(winrt::put_abi(controller))));
        return controller;
    }

    inline int ShutdownDispatcherQueueControllerAndWait(winrt::Windows::System::DispatcherQueueController const& controller, int exitCode)
    {
        impl::ShutdownAndThenPostQuitMessage(controller, exitCode);
        MSG msg = {};
        while (GetMessageW(&msg, nullptr, 0, 0))
        {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        return static_cast<int>(msg.wParam);
    }
}
