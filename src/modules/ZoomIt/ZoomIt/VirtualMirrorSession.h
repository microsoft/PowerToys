// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma once

#include "VirtualDisplay.h"
#include "VirtualMirrorLayout.h"

#include <atomic>
#include <string>
#include <thread>

namespace zoomit_mirror
{
    constexpr UINT WM_USER_MIRROR_DEVICE_READY = WM_USER + 114;

    // All public methods run on the UI thread. The worker exclusively owns the
    // VirtualDisplay and its result until Complete or Close joins it. Stop the
    // MirrorWindow before Close removes the display on which it is rendering.
    class VirtualMirrorSession
    {
    public:
        VirtualMirrorSession() = default;
        ~VirtualMirrorSession();
        VirtualMirrorSession(const VirtualMirrorSession&) = delete;
        VirtualMirrorSession& operator=(const VirtualMirrorSession&) = delete;

        bool Begin(HWND notify, HMONITOR source, RECT screenRegion, std::wstring& error);
        bool Complete(std::wstring& error);
        void Close() noexcept;

        bool IsPending() const noexcept;
        bool IsActive() const noexcept;
        bool IsLayoutCurrent() const noexcept;
        HMONITOR SourceMonitor() const noexcept;
        HMONITOR TargetMonitor() const noexcept;
        RECT Region() const noexcept;

    private:
        void CreateDisplay() noexcept;

        VirtualDisplay m_display;
        std::thread m_worker;
        std::atomic_bool m_cancel = false;

        // UI state: never written by the creating worker.
        HWND m_notify = nullptr;
        bool m_pending = false;
        bool m_active = false;
        bool m_closing = false;
        RECT m_region{};
        MonitorSnapshot m_originalSource;
        MonitorSnapshot m_source;
        MonitorSnapshot m_target;

        // Worker result: the UI reads these only after joining m_worker.
        bool m_created = false;
        MonitorSnapshot m_createdTarget;
        std::wstring m_creationError;
    };
}
