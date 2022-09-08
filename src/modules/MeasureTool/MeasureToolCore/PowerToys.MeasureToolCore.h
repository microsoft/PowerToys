#pragma once

#include "Core.g.h"
#include "ToolState.h"
#include "OverlayUI.h"
#include "Settings.h"

#include <common/utils/serialized.h>
#include "ScreenCapturing.h"

struct PowerToysMisc
{
    PowerToysMisc()
    {
        Trace::RegisterProvider();
        LoggerHelpers::init_logger(L"Measure Tool", L"Core", "Measure Tool");
        InitUnhandledExceptionHandler();
    }

    ~PowerToysMisc()
    {
        Trace::UnregisterProvider();
    }
};

namespace winrt::PowerToys::MeasureToolCore::implementation
{
    struct Core : PowerToysMisc, CoreT<Core>
    {
        Core();
        ~Core();
        void Close();

        void StartBoundsTool();
        void StartMeasureTool(const bool horizontal, const bool vertical);
        void SetToolCompletionEvent(ToolSessionCompleted sessionCompletedTrigger);
        void SetToolbarBoundingBox(const uint32_t fromX, const uint32_t fromY, const uint32_t toX, const uint32_t toY);
        void ResetState();
        float GetDPIScaleForWindow(uint64_t windowHandle);
        void MouseCaptureThread();

        DxgiAPI dxgiAPI;

        wil::shared_event _stopMouseCaptureThreadSignal;
        std::thread _mouseCaptureThread;
        std::vector<std::thread> _screenCaptureThreads;
        
        std::vector<std::unique_ptr<OverlayUIState>> _overlayUIStates;
        Serialized<MeasureToolState> _measureToolState;
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
