// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "CursorBrakeEdges.h"

#include <string>
#include <vector>

namespace cursorbrake
{
    // One active display path. `source` is the GDI device the path shows ("\\.\DISPLAY1"), `id`
    // the monitor ID in MonitorId's form, `name` the friendly name such as "Dell U2724D", `builtIn`
    // whether the output is a panel built into the device, and `target` the connector it ends at.
    // Any text field can be empty when Windows does not report it.
    struct DisplayPath
    {
        std::wstring source;
        std::wstring id;
        std::wstring name;
        bool builtIn = false;
        DisplayTarget target;
    };

    // The display configuration as CursorBrake reads it: the active paths, in QueryDisplayConfig's
    // order, and every available target, active or not, which Windows' display numbers count.
    struct DisplayConfiguration
    {
        std::vector<DisplayPath> active;
        std::vector<DisplayTarget> available;
    };

    // Reads the display configuration. DisplayUtils::GetDisplays carries no name or number, so they
    // come from here. Empty when the configuration cannot be read.
    DisplayConfiguration GetDisplayConfiguration();
}
