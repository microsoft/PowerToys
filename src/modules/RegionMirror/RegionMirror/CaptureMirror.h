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
#include <vector>

namespace RegionMirror
{
    struct CaptureSource
    {
        HMONITOR monitor{};
        RECT bounds{};
    };

    // Start/Stop and destruction belong to the window's owning thread. Keep both
    // windows alive until Stop returns. The target must remain outside sourceRect.
    class CaptureMirror final
    {
    public:
        CaptureMirror();
        ~CaptureMirror();

        CaptureMirror(const CaptureMirror&) = delete;
        CaptureMirror& operator=(const CaptureMirror&) = delete;

        // sourceRect and each source's bounds use physical virtual-screen pixels.
        // Uncovered parts of the rectangle stay black. Startup is asynchronous:
        // worker failures set Error and post failureMessage to notifyWindow.
        void Start(HWND targetWindow, const std::vector<CaptureSource>& sources, RECT sourceRect, HWND notifyWindow, UINT failureMessage);
        void Stop() noexcept;

        // The final count/error remain available after Stop, until the next Start.
        uint64_t FramesPresented() const noexcept;
        // Frames copied into the composite, in the same order as Start's sources.
        std::vector<uint64_t> SourceFrames() const;
        std::wstring Error() const;

    private:
        struct State;
        std::unique_ptr<State> m_state;
        bool m_stopping = false;
    };
}
