// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "CursorBrakeEdges.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Data.Json.h>

#include <string>
#include <vector>

// detected-edges.json, which the module writes into its settings folder for Settings to read: the
// monitors with their IDs and names, the edges between them, and when each pair of monitors was
// last seen next to each other. Settings has no adjacency code of its own, so the edges here are
// the engine's.
namespace cursorbrake
{
    // A file version Settings checks before reading the rest.
    constexpr int DETECTED_EDGES_VERSION = 1;

    // A monitor as published. `id` is empty for a monitor without a usable ID, `rect` is in
    // physical pixels, half-open like the engine's, `number` is Windows' display number
    // (DisplayNumbers), and `builtIn` marks a panel built into the device.
    struct DetectedMonitor
    {
        std::wstring id;
        std::wstring name;
        RectL rect;
        unsigned int dpi = BASE_DPI;
        bool primary = false;
        int number = 0;
        bool builtIn = false;
    };

    // The file's content: `version`, `identified` (every monitor has an ID), `monitors`, `edges`
    // (InnerEdges over the monitors, as indices into `monitors`, with "vertical" for a side-by-side
    // edge along x = at and "horizontal" for a stacked one along y = at), `seen` and `expired`
    // (SeenRecord, each pair by its two IDs). Settings reads `seen` and ignores `expired`.
    winrt::Windows::Data::Json::JsonObject BuildDetectedEdges(const std::vector<DetectedMonitor>& monitors, const std::vector<SeenPair>& seen, const std::vector<std::pair<std::wstring, std::wstring>>& expired = {});

    // The `seen` entries of a file, in order, skipping any entry without two string IDs of at most
    // MAX_MONITOR_ID_LENGTH characters and a time in FormatUtc's form before SEEN_TIME_LIMIT, and
    // reading at most MAX_SEEN_ENTRIES elements. A time ahead of the clock is read as it stands,
    // since the clock may be the one that is wrong.
    std::vector<SeenPair> ReadSeen(const winrt::Windows::Data::Json::JsonObject& file);

    // The `expired` pairs of a file, in order, skipping any entry without two string IDs of at most
    // MAX_MONITOR_ID_LENGTH characters, and reading at most MAX_SEEN_ENTRIES elements.
    std::vector<std::pair<std::wstring, std::wstring>> ReadExpired(const winrt::Windows::Data::Json::JsonObject& file);
}
