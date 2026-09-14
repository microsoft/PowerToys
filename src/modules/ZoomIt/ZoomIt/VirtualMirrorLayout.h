// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma once

#include <windows.h>

#include <cwchar>
#include <string>

namespace zoomit_mirror
{
    struct MonitorSnapshot
    {
        HMONITOR monitor = nullptr;
        RECT bounds{};
        std::wstring deviceName;
        // Sorted PnP target interface paths, rather than a renumberable GDI name.
        std::wstring identity;
    };

    inline bool IsNonEmptyRegion(const RECT& region) noexcept
    {
        return region.left < region.right && region.top < region.bottom;
    }

    inline bool AreSameBounds(const RECT& first, const RECT& second) noexcept
    {
        return first.left == second.left && first.top == second.top &&
               first.right == second.right && first.bottom == second.bottom;
    }

    inline bool IsRegionWithinMonitor(const RECT& region, const RECT& monitor) noexcept
    {
        return IsNonEmptyRegion(region) && IsNonEmptyRegion(monitor) &&
               region.left >= monitor.left && region.top >= monitor.top &&
               region.right <= monitor.right && region.bottom <= monitor.bottom;
    }

    inline bool IsSameMonitorIdentity(const MonitorSnapshot& first, const MonitorSnapshot& second) noexcept
    {
        return !first.identity.empty() && !second.identity.empty() &&
               _wcsicmp(first.identity.c_str(), second.identity.c_str()) == 0;
    }

    inline bool IsSameMonitorPlacement(const MonitorSnapshot& expected, const MonitorSnapshot& current) noexcept
    {
        return current.monitor != nullptr && IsSameMonitorIdentity(expected, current) &&
               AreSameBounds(expected.bounds, current.bounds);
    }

    inline bool IsSameMonitorSnapshot(const MonitorSnapshot& expected, const MonitorSnapshot& current) noexcept
    {
        return IsSameMonitorPlacement(expected, current) && expected.monitor == current.monitor &&
               _wcsicmp(expected.deviceName.c_str(), current.deviceName.c_str()) == 0;
    }

    inline bool IsMirrorLayoutValid(const MonitorSnapshot& originalSource, const MonitorSnapshot& source,
                                    const MonitorSnapshot& target, const RECT& region) noexcept
    {
        const bool monitorsOverlap = source.bounds.left < target.bounds.right &&
                                     target.bounds.left < source.bounds.right &&
                                     source.bounds.top < target.bounds.bottom &&
                                     target.bounds.top < source.bounds.bottom;
        return IsSameMonitorPlacement(originalSource, source) &&
               IsRegionWithinMonitor(region, source.bounds) &&
               target.monitor != nullptr && source.monitor != target.monitor &&
               IsNonEmptyRegion(target.bounds) && !target.identity.empty() &&
               !IsSameMonitorIdentity(source, target) && !monitorsOverlap;
    }
}
