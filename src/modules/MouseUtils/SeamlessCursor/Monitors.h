// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "SeamlessCursorEngine.h"

#include <string>
#include <vector>

namespace seamless_cursor
{
    struct MonitorDetails
    {
        Monitor monitor;
        std::wstring name;
        bool primary = false;
        double mmPerPixel = 0.0; // 0 when the monitor does not report a plausible physical size.

        // The id is the monitor's device path. While Windows cannot tell it (for example in a locked session), the id
        // is the GDI device name, which no calibration uses.
        bool identified = false;
    };

    // Active monitors in physical pixels. The caller's thread must be Per-Monitor V2 DPI aware.
    // The physical size needs a device context per monitor, so callers that do not use it can skip it.
    std::vector<MonitorDetails> EnumerateMonitors(bool withPhysicalSize = true);

    std::vector<Monitor> ToEngineMonitors(const std::vector<MonitorDetails>& monitors);
}
