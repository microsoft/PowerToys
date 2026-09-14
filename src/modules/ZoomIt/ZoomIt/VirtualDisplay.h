// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma once

#include <windows.h>
#include <swdevice.h>

#include <atomic>
#include <string>

namespace zoomit_mirror
{
    // Call Create on a worker: PnP enumeration can take up to 30 seconds. All access
    // must be serialized; cancel and join the creating worker before reading or closing.
    class VirtualDisplay
    {
    public:
        VirtualDisplay() = default;
        ~VirtualDisplay();
        VirtualDisplay(const VirtualDisplay&) = delete;
        VirtualDisplay& operator=(const VirtualDisplay&) = delete;

        // Failure (including cancellation) closes the owned display and sets error.
        bool Create(int requestedWidth, int requestedHeight, std::wstring& error, const std::atomic_bool& cancel);
        void Close() noexcept;
        HMONITOR Monitor() const noexcept;
        RECT Bounds() const noexcept;
        std::wstring DeviceName() const;

    private:
        void CreateInternal(int requestedWidth, int requestedHeight, const std::atomic_bool& cancel);

        HSWDEVICE m_device = nullptr;
        HMONITOR m_monitor = nullptr;
        RECT m_bounds{};
        std::wstring m_deviceName;
        std::wstring m_instanceId;
        std::wstring m_retiredInstanceId;
    };
}
