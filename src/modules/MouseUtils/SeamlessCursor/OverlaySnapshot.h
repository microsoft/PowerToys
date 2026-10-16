// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "CalibrationStore.h"
#include "Monitors.h"
#include "SeamlessCursorEngine.h"

#include <windows.h>
#include <wincodec.h>
#include <winrt/base.h>

#include <functional>
#include <vector>

// The calibration overlay drawn into bitmaps instead of windows, for tools that check it on monitor layouts that are hard
// to set up (see OverlaySnapshots). The module itself does not use it.
namespace seamless_cursor
{
    // Moves the pointer over a snapshot, as mouse moves over the overlay windows would, to show the pointer trail.
    class SnapshotPointer
    {
    public:
        virtual ~SnapshotPointer() = default;

        // The engine for the lines as placed, as Try while adjusting remaps with it.
        virtual Engine CurrentEngine() const = 0;

        // `screen` in virtual-screen pixels; `time` in GetTickCount64 milliseconds.
        virtual void Move(POINT screen, ULONGLONG time) = 0;
    };

    struct SnapshotMonitor
    {
        MonitorDetails details;
        float scale = 1.0f; // The Windows display scale.
    };

    struct SnapshotRequest
    {
        std::vector<SnapshotMonitor> monitors;
        std::vector<BorderCalibration> saved;
        OverlayPreferences preferences;
        EngineOptions engineOptions;
        std::function<void(SnapshotPointer&)> movePointer; // Optional.
        // When the snapshot is taken, in GetTickCount64 milliseconds, for the pointer trail's fading; 0 for right after
        // the last pointer move.
        ULONGLONG time = 0;
    };

    // One bitmap per monitor, in the order of `request.monitors`; nothing on failure. The overlay's strings come from the
    // string resources of the binary that calls this, so it must contain this module's resources.
    std::vector<winrt::com_ptr<IWICBitmap>> RenderSnapshot(IWICImagingFactory* wic, const SnapshotRequest& request);
}
