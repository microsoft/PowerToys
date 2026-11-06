// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "DisplayNames.h"

#include <common/logger/logger.h>

namespace
{
    // QueryDisplayConfig fails with ERROR_INSUFFICIENT_BUFFER when the paths change between the
    // size query and the read, so a display change mid-call gets a few more tries.
    constexpr int QUERY_ATTEMPTS = 3;
}

namespace cursorbrake
{
    DisplayConfiguration GetDisplayConfiguration()
    {
        std::vector<DISPLAYCONFIG_PATH_INFO> paths;
        std::vector<DISPLAYCONFIG_MODE_INFO> modes;
        LONG result = ERROR_INSUFFICIENT_BUFFER;
        for (int attempt = 0; attempt < QUERY_ATTEMPTS && result == ERROR_INSUFFICIENT_BUFFER; ++attempt)
        {
            UINT32 pathCount = 0;
            UINT32 modeCount = 0;
            result = GetDisplayConfigBufferSizes(QDC_ALL_PATHS, &pathCount, &modeCount);
            if (result != ERROR_SUCCESS)
            {
                break;
            }
            paths.resize(pathCount);
            modes.resize(modeCount);
            result = QueryDisplayConfig(QDC_ALL_PATHS, &pathCount, paths.data(), &modeCount, modes.data(), nullptr);
            paths.resize(pathCount);
        }
        if (result != ERROR_SUCCESS)
        {
            Logger::warn(L"CursorBrake could not read the display configuration for monitor names and numbers, error: {}.", result);
            return {};
        }

        // Adapters by the order they first appear, so a target's key does not depend on LUID values.
        std::vector<LUID> adapters;
        const auto adapterOrder = [&](const LUID& adapter) {
            for (size_t i = 0; i < adapters.size(); ++i)
            {
                if (adapters[i].LowPart == adapter.LowPart && adapters[i].HighPart == adapter.HighPart)
                {
                    return i;
                }
            }
            adapters.push_back(adapter);
            return adapters.size() - 1;
        };

        DisplayConfiguration configuration;
        for (const DISPLAYCONFIG_PATH_INFO& path : paths)
        {
            const DisplayTarget target{ adapterOrder(path.targetInfo.adapterId), path.targetInfo.id };
            if (path.targetInfo.targetAvailable)
            {
                configuration.available.push_back(target);
            }
            if ((path.flags & DISPLAYCONFIG_PATH_ACTIVE) == 0)
            {
                continue;
            }

            DisplayPath entry;
            entry.target = target;
            entry.builtIn = IsBuiltInOutput(static_cast<long>(path.targetInfo.outputTechnology));

            DISPLAYCONFIG_SOURCE_DEVICE_NAME source{};
            source.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            source.header.size = sizeof(source);
            source.header.adapterId = path.sourceInfo.adapterId;
            source.header.id = path.sourceInfo.id;
            if (DisplayConfigGetDeviceInfo(&source.header) == ERROR_SUCCESS)
            {
                entry.source = source.viewGdiDeviceName;
            }

            DISPLAYCONFIG_TARGET_DEVICE_NAME name{};
            name.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
            name.header.size = sizeof(name);
            name.header.adapterId = path.targetInfo.adapterId;
            name.header.id = path.targetInfo.id;
            if (DisplayConfigGetDeviceInfo(&name.header) == ERROR_SUCCESS)
            {
                entry.id = MonitorIdFromDevicePath(name.monitorDevicePath);
                entry.name = name.monitorFriendlyDeviceName;
            }
            configuration.active.push_back(std::move(entry));
        }
        return configuration;
    }
}
