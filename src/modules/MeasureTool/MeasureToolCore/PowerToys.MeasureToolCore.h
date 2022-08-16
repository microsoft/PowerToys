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
        void SetToolCompletionEvent(ToolSessionCompleted sessionCompletedTrigger);
        void SetToolbarBoundingBox(const uint32_t fromX, const uint32_t fromY, const uint32_t toX, const uint32_t toY);
        void ResetState();
        Point GetCursorPosition();
        float GetDPIScaleForWindow(uint64_t windowHandle);

        HWND _overlayUIWindowHandle = {};
        HWND _nativeWindowHandle = {};
        MeasureToolState _measureToolState;
        BoundsToolState _boundsToolState;
        CommonState _commonState;
        Settings _settings;
    };
}

namespace winrt::PowerToys::MeasureToolCore::factory_implementation
{
    struct Core : CoreT<Core, implementation::Core>
    {
    };
}
