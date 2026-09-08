// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma once

#include <windows.h>
#include <swdevice.h>

#include <atomic>
#include <string>

namespace RegionMirror
{
    // Create and Close must be serialized. Cancel and join the creating worker before closing.
    class VirtualDisplay
    {
    public:
        VirtualDisplay() = default;
        ~VirtualDisplay();
        VirtualDisplay(const VirtualDisplay&) = delete;
        VirtualDisplay& operator=(const VirtualDisplay&) = delete;

        void Create(int requestedWidth, int requestedHeight, const std::atomic_bool& cancel);
        void Close() noexcept;
        HMONITOR Monitor() const noexcept;
        RECT Bounds() const noexcept;
        std::wstring DeviceName() const;
        std::wstring InstanceId() const;

    private:
        HSWDEVICE m_device = nullptr;
        HMONITOR m_monitor = nullptr;
        RECT m_bounds{};
        std::wstring m_deviceName;
        std::wstring m_instanceId;
        std::wstring m_retiredInstanceId;
    };
}
