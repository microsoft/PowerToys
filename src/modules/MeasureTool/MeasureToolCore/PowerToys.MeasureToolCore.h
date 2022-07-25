#pragma once

#include "Core.g.h"
#include "MeasureToolState.h"
#include "Settings.h"

namespace winrt::PowerToys::MeasureToolCore::implementation
{
    struct Core : CoreT<Core>
    {
        Core();
        void StartBoundsTool();
        void StartMeasureTool(const bool horizontal, const bool vertical);

        void ResetState();
        winrt::PowerToys::MeasureToolCore::Point GetCursorPosition();

        HWND _overlayUIWindowHandle = {};
        HWND _nativeWindowHandle = {};
        HMONITOR _targetMonitor = nullptr;
        float _targetMonitorScaleRatio = 1.f;
        MeasureToolState _measureToolState;
        BoundsToolState _boundsToolState;
        Settings _settings;
    };
}

namespace winrt::PowerToys::MeasureToolCore::factory_implementation
{
    struct Core : CoreT<Core, implementation::Core>
    {
    };
}
