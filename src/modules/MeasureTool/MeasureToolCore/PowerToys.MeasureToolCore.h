#pragma once

#include "Core.g.h"
#include "ToolState.h"
#include "OverlayUI.h"
#include "Settings.h"
#include "GuideOverlayUI.h"

#include <common/Telemetry/EtwTrace/EtwTrace.h>
#include <common/utils/serialized.h>
#include "ScreenCapturing.h"

#include <mutex>

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

        void InitResources();
        void StartBoundsTool();
        void StartMeasureTool(const bool horizontal, const bool vertical);
        void BeginGuidePlacement(GuideOrientation orientation);
        void ClearGuides();
        bool HasGuides();
        void SetGuidePresenceChangedEvent(GuidePresenceChanged presenceChangedTrigger);
        void SetGuideEditMode(bool enabled);
        void SetToolCompletionEvent(ToolSessionCompleted sessionCompletedTrigger);
        void SetToolbarWindowHandle(uint64_t windowHandle);
        void SetToolbarBoundingBox(const int32_t fromX, const int32_t fromY, const int32_t toX, const int32_t toY);
        void ResetState();
        float GetDPIScaleForWindow(uint64_t windowHandle);
        void MouseCaptureThread();
        void LoadSettings();
        void UpdateGuideCaptureWindows();
        void NotifyGuidePresenceChanged(bool hasGuides);

        DxgiAPI dxgiAPI;

        wil::shared_event _stopMouseCaptureThreadSignal;
        std::thread _mouseCaptureThread;
        std::vector<std::thread> _screenCaptureThreads;

        std::vector<std::unique_ptr<OverlayUIState>> _overlayUIStates;
        Serialized<MeasureToolState> _measureToolState;
        BoundsToolState _boundsToolState;
        CommonState _commonState;
        Settings _settings;
        std::unique_ptr<GuideOverlayManager> _guideOverlayManager;
        std::mutex _guidePresenceChangedMutex;
        GuidePresenceChanged _guidePresenceChanged{ nullptr };
        bool _closed = false;
        Shared::Trace::ETWTrace trace{};
    };
}

namespace winrt::PowerToys::MeasureToolCore::factory_implementation
{
    struct Core : CoreT<Core, implementation::Core>
    {
    };
}
