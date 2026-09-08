// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma once

#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>

#include <cstdint>
#include <memory>
#include <string>

namespace RegionMirror
{
    // Start/Stop and destruction belong to the window's owning thread. Keep both
    // windows alive until Stop returns. The target must remain outside sourceRect.
    class CaptureMirror final
    {
    public:
        CaptureMirror();
        ~CaptureMirror();

        CaptureMirror(const CaptureMirror&) = delete;
        CaptureMirror& operator=(const CaptureMirror&) = delete;

        // sourceRect uses physical virtual-screen coordinates and must be fully
        // inside sourceMonitor. Startup is asynchronous: worker failures set Error
        // and post failureMessage to notifyWindow. No capture consent is bypassed.
        void Start(HWND targetWindow, HMONITOR sourceMonitor, RECT sourceRect, HWND notifyWindow, UINT failureMessage);
        void Stop() noexcept;

        // The final count/error remain available after Stop, until the next Start.
        uint64_t FramesPresented() const noexcept;
        std::wstring Error() const;

    private:
        struct State;
        std::unique_ptr<State> m_state;
        bool m_stopping = false;
    };
}
