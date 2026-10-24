// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "Monitors.h"

#include <utility>

namespace seamless_cursor
{
    namespace
    {
        struct TargetName
        {
            std::wstring gdiDevice; // \\.\DISPLAYn
            std::wstring devicePath;
            std::wstring friendlyName;
        };

        std::vector<TargetName> QueryTargetNames()
        {
            std::vector<TargetName> names;
            std::vector<DISPLAYCONFIG_PATH_INFO> paths;
            std::vector<DISPLAYCONFIG_MODE_INFO> modes;
            UINT32 pathCount = 0;
            UINT32 modeCount = 0;
            LONG result = ERROR_INSUFFICIENT_BUFFER;

            // The configuration can change between the two calls, so retry as the documentation suggests.
            while (result == ERROR_INSUFFICIENT_BUFFER)
            {
                if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, &pathCount, &modeCount) != ERROR_SUCCESS)
                {
                    return names;
                }

                paths.resize(pathCount);
                modes.resize(modeCount);
                result = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, &pathCount, paths.data(), &modeCount, modes.data(), nullptr);
            }

            if (result != ERROR_SUCCESS)
            {
                return names;
            }

            paths.resize(pathCount);
            for (const auto& path : paths)
            {
                DISPLAYCONFIG_SOURCE_DEVICE_NAME source{};
                source.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                source.header.size = sizeof(source);
                source.header.adapterId = path.sourceInfo.adapterId;
                source.header.id = path.sourceInfo.id;

                DISPLAYCONFIG_TARGET_DEVICE_NAME target{};
                target.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                target.header.size = sizeof(target);
                target.header.adapterId = path.targetInfo.adapterId;
                target.header.id = path.targetInfo.id;

                if (DisplayConfigGetDeviceInfo(&source.header) == ERROR_SUCCESS &&
                    DisplayConfigGetDeviceInfo(&target.header) == ERROR_SUCCESS)
                {
                    names.push_back({ source.viewGdiDeviceName, target.monitorDevicePath, target.monitorFriendlyDeviceName });
                }
            }

            return names;
        }

        double PhysicalPitch(const wchar_t* device, const RECT& rect)
        {
            const HDC dc = CreateDCW(L"DISPLAY", device, nullptr, nullptr);
            if (!dc)
            {
                return 0.0;
            }

            const int widthMm = GetDeviceCaps(dc, HORZSIZE);
            const int heightMm = GetDeviceCaps(dc, VERTSIZE);
            DeleteDC(dc);

            // TVs, projectors and some panels report no size or a wrong one.
            const int width = rect.right - rect.left;
            const int height = rect.bottom - rect.top;
            if (widthMm < 100 || heightMm < 50 || width <= 0 || height <= 0)
            {
                return 0.0;
            }

            const double sizeAspect = static_cast<double>(widthMm) / heightMm;
            const double pixelAspect = static_cast<double>(width) / height;
            if (std::abs(sizeAspect / pixelAspect - 1.0) > 0.05)
            {
                return 0.0;
            }

            return static_cast<double>(widthMm) / width;
        }

        BOOL CALLBACK AddMonitor(HMONITOR monitor, HDC, LPRECT, LPARAM data)
        {
            MONITORINFOEXW info{};
            info.cbSize = sizeof(info);
            if (GetMonitorInfoW(monitor, &info))
            {
                MonitorDetails details;
                details.monitor.id = info.szDevice;
                details.monitor.rect = info.rcMonitor;
                details.name = info.szDevice;
                details.primary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0;
                reinterpret_cast<std::vector<MonitorDetails>*>(data)->push_back(std::move(details));
            }

            return TRUE;
        }
    }

    std::vector<MonitorDetails> EnumerateMonitors(bool withPhysicalSize)
    {
        std::vector<MonitorDetails> monitors;
        EnumDisplayMonitors(nullptr, nullptr, AddMonitor, reinterpret_cast<LPARAM>(&monitors));
        if (withPhysicalSize)
        {
            for (auto& details : monitors)
            {
                // The id still holds the GDI device name here.
                details.mmPerPixel = PhysicalPitch(details.monitor.id.c_str(), details.monitor.rect);
            }
        }

        // The GDI device name changes when monitors are reconnected; the device path identifies the
        // monitor and the connector it is plugged into.
        const auto names = QueryTargetNames();
        for (auto& details : monitors)
        {
            const auto name = std::find_if(names.begin(), names.end(), [&details](const TargetName& n) {
                return n.gdiDevice == details.monitor.id;
            });
            if (name != names.end())
            {
                details.monitor.id = name->devicePath;
                details.identified = true;
                if (!name->friendlyName.empty())
                {
                    details.name = name->friendlyName;
                }
            }
        }

        return monitors;
    }

    std::vector<Monitor> ToEngineMonitors(const std::vector<MonitorDetails>& monitors)
    {
        std::vector<Monitor> result;
        result.reserve(monitors.size());
        for (const auto& details : monitors)
        {
            result.push_back(details.monitor);
        }

        return result;
    }
}
