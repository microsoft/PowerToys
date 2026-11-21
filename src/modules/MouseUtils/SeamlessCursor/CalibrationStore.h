// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "SeamlessCursorEngine.h"

#include <optional>
#include <vector>

// calibration.json is owned by the native module only. Settings UI never writes it, so it can never
// overwrite the calibration with a stale copy.
namespace seamless_cursor
{
    // Nothing while the file cannot be read (another program holds it), which usually passes; waits about a second for
    // it first. An empty list when there is no file, or when it cannot be parsed, which does not pass by itself.
    // Malformed entries are skipped.
    std::optional<std::vector<BorderCalibration>> LoadCalibrations();

    // As LoadCalibrations, but without waiting for the file, for a caller that tries again later.
    std::optional<std::vector<BorderCalibration>> TryLoadCalibrations();

    // The stored calibrations to merge a save into. A file that cannot be parsed is copied to calibration.json.bak first,
    // and then counts as empty. Nothing while the file cannot be read, or when that copy fails: then the save must not
    // replace the file.
    std::optional<std::vector<BorderCalibration>> LoadCalibrationsForSave();

    bool SaveCalibrations(const std::vector<BorderCalibration>& calibrations);
    bool DeleteCalibrations();

    // Overlay preferences live in their own file, so resetting the calibration keeps them.
    struct OverlayPreferences
    {
        bool tryWhileAdjusting = true;
        bool showEdgeMap = false;
        bool showTrail = false;
    };

    OverlayPreferences LoadOverlayPreferences();
    void SaveOverlayPreferences(const OverlayPreferences& preferences);
}
