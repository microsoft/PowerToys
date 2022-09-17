#include "pch.h"

#include <common/display/dpi_aware.h>
#include <common/display/monitors.h>
#include <common/utils/logger_helper.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/logger/logger.h>

#include "../MeasureToolModuleInterface/trace.h"
#include "constants.h"
#include "PowerToys.MeasureToolCore.h"
#include "Core.g.cpp"
#include "OverlayUI.h"
#include "ScreenCapturing.h"

//#define DEBUG_PRIMARY_MONITOR_ONLY

namespace winrt::PowerToys::MeasureToolCore::implementation
{
    void Core::MouseCaptureThread()
    {
        while (!_stopMouseCaptureThreadSignal.is_signaled())
        {
            static_assert(sizeof(_commonState.cursorPosSystemSpace) == sizeof(LONG64));
            POINT cursorPos = {};
            GetCursorPos(&cursorPos);
            InterlockedExchange64(reinterpret_cast<LONG64*>(&_commonState.cursorPosSystemSpace), std::bit_cast<LONG64>(cursorPos));
            std::this_thread::sleep_for(consts::TARGET_FRAME_DURATION);
        }
    }

    Core::Core() :
        _stopMouseCaptureThreadSignal{ wil::EventOptions::ManualReset },
        _mouseCaptureThread{ [this] { MouseCaptureThread(); } }
    {
    }

    Core::~Core()
    {
        Close();
    }

    void Core::Close()
    {
        ResetState();

        // avoid triggering d2d debug layer leak on shutdown
        dxgiAPI = DxgiAPI{ DxgiAPI::Uninitialized{} };

#if 0
        winrt::com_ptr<IDXGIDebug> dxgiDebug;
        winrt::check_hresult(DXGIGetDebugInterface1({},
                                                    winrt::guid_of<IDXGIDebug>(),
                                                    dxgiDebug.put_void()));
        dxgiDebug->ReportLiveObjects(DXGI_DEBUG_ALL, DXGI_DEBUG_RLO_ALL);
#endif

        if (!_stopMouseCaptureThreadSignal.is_signaled())
            _stopMouseCaptureThreadSignal.SetEvent();

        if (_mouseCaptureThread.joinable())
            _mouseCaptureThread.join();
    }

    void Core::ResetState()
    {
        _commonState.closeOnOtherMonitors = true;
        _overlayUIStates.clear();
        _boundsToolState = { .commonState = &_commonState };
        for (auto& thread : _screenCaptureThreads)
        {
            if (thread.joinable())
            {
                thread.join();
            }
        }
        _screenCaptureThreads.clear();
        _measureToolState.Reset();
        _measureToolState.Access([&](MeasureToolState& s) {
            s.commonState = &_commonState;
        });

        _settings = Settings::LoadFromFile();

        _commonState.units = _settings.units;
        _commonState.lineColor.r = _settings.lineColor[0] / 255.f;
        _commonState.lineColor.g = _settings.lineColor[1] / 255.f;
        _commonState.lineColor.b = _settings.lineColor[2] / 255.f;
        _commonState.closeOnOtherMonitors = false;
    }

    void Core::StartBoundsTool()
    {
        ResetState();

#if defined(DEBUG_PRIMARY_MONITOR_ONLY)
        std::vector<MonitorInfo> monitors = { MonitorInfo::GetPrimaryMonitor() };
        const auto& monitorInfo = monitors[0];
#else
        const auto monitors = MonitorInfo::GetMonitors(true);
        for (const auto& monitorInfo : monitors)
#endif
        {
            auto overlayUI = OverlayUIState::Create(&dxgiAPI,
                                                    _boundsToolState,
                                                    _commonState,
                                                    monitorInfo);
#if !defined(DEBUG_PRIMARY_MONITOR_ONLY)
            if (!overlayUI)
                continue;
#endif
            _overlayUIStates.push_back(std::move(overlayUI));
        }
        Trace::BoundsToolActivated();
    }

    void Core::StartMeasureTool(const bool horizontal, const bool vertical)
    {
        ResetState();

        _measureToolState.Access([horizontal, vertical, this](MeasureToolState& state) {
            if (horizontal)
                state.global.mode = vertical ? MeasureToolState::Mode::Cross : MeasureToolState::Mode::Horizontal;
            else
                state.global.mode = MeasureToolState::Mode::Vertical;

            state.global.continuousCapture = _settings.continuousCapture;
            state.global.drawFeetOnCross = _settings.drawFeetOnCross;
            state.global.pixelTolerance = _settings.pixelTolerance;
            state.global.perColorChannelEdgeDetection = _settings.perColorChannelEdgeDetection;
        });

#if defined(DEBUG_PRIMARY_MONITOR_ONLY)
        std::vector<MonitorInfo> monitors = { MonitorInfo::GetPrimaryMonitor() };
        const auto& monitorInfo = monitors[0];
#else
        const auto monitors = MonitorInfo::GetMonitors(true);
        for (const auto& monitorInfo : monitors)
#endif
        {
            auto overlayUI = OverlayUIState::Create(&dxgiAPI,
                                                    _measureToolState,
                                                    _commonState,
                                                    monitorInfo);
#if !defined(DEBUG_PRIMARY_MONITOR_ONLY)
            if (!overlayUI)
                return;
#endif
            _overlayUIStates.push_back(std::move(overlayUI));
        }

        for (size_t i = 0; i < monitors.size(); ++i)
        {
            auto thread = StartCapturingThread(
                &dxgiAPI,
                _commonState,
                _measureToolState,
                _overlayUIStates[i]->overlayWindowHandle(),
                monitors[i]);
            _screenCaptureThreads.emplace_back(std::move(thread));
        }

        Trace::MeasureToolActivated();
    }

    void MeasureToolCore::implementation::Core::SetToolCompletionEvent(ToolSessionCompleted sessionCompletedTrigger)
    {
        _commonState.sessionCompletedCallback = [trigger = std::move(sessionCompletedTrigger)] {
            trigger();
        };
    }

    void MeasureToolCore::implementation::Core::SetToolbarBoundingBox(const uint32_t fromX,
                                                                      const uint32_t fromY,
                                                                      const uint32_t toX,
                                                                      const uint32_t toY)
    {
        _commonState.toolbarBoundingBox = Box{ RECT{ .left = static_cast<long>(fromX),
                                                     .top = static_cast<long>(fromY),
                                                     .right = static_cast<long>(toX),
                                                     .bottom = static_cast<long>(toY) } };
    }

    float MeasureToolCore::implementation::Core::GetDPIScaleForWindow(uint64_t windowHandle)
    {
        UINT dpi = DPIAware::DEFAULT_DPI;
        DPIAware::GetScreenDPIForWindow(std::bit_cast<HWND>(windowHandle), dpi);
        return static_cast<float>(dpi) / DPIAware::DEFAULT_DPI;
    }
}
