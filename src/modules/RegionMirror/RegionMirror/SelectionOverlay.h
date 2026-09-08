// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#pragma once
#include <windows.h>
#include <optional>

namespace RegionMirror
{
    struct Selection
    {
        RECT region{};
    };

    // Synchronous modal interaction on the UI thread. Escape or focus loss cancels.
    std::optional<Selection> SelectRegion(HWND owner);
    HWND CreateRegionBorder(const RECT& region);
}
