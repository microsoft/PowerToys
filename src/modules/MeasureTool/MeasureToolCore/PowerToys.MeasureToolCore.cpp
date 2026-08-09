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
#include "GuideOverlayUI.h"
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
        _stopMouseCaptureThreadSignal{ wil::EventOptions::ManualReset }
    {
        LoadSettings();
        _guideOverlayManager = std::make_unique<GuideOverlayManager>(
            &dxgiAPI,
            _commonState.lineColor,
            _settings.pixelTolerance,
            _settings.perColorChannelEdgeDetection,
            [this](bool hasGuides) {
                NotifyGuidePresenceChanged(hasGuides);
            });
        _mouseCaptureThread = std::thread{ [this] {
            MouseCaptureThread();
        } };
    }

    Core::~Core()
    {
        Close();
    }

    void Core::Close()
    {
        if (_closed)
        {
            return;
        }
        _closed = true;

        ResetState();
        _guideOverlayManager.reset();

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

    void Core::InitResources()
    {
        Measurement::InitResources();
    }

    void Core::ResetState()
    {
        if (_guideOverlayManager)
        {
            _guideOverlayManager->CancelInteraction();
        }
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
        if (_guideOverlayManager)
        {
            _guideOverlayManager->SetCaptureExclusionWindows({});
        }
        _measureToolState.Reset();
        _measureToolState.Access([&](MeasureToolState& s) {
            s.commonState = &_commonState;
        });

        LoadSettings();
        _commonState.closeOnOtherMonitors = false;
    }

    void Core::LoadSettings()
    {
        _settings = Settings::LoadFromFile();
        _commonState.units = _settings.units;
        _commonState.lineColor.r = _settings.lineColor[0] / 255.f;
        _commonState.lineColor.g = _settings.lineColor[1] / 255.f;
        _commonState.lineColor.b = _settings.lineColor[2] / 255.f;

        if (_guideOverlayManager)
        {
            _guideOverlayManager->UpdateSettings(
                _commonState.lineColor,
                _settings.pixelTolerance,
                _settings.perColorChannelEdgeDetection);
        }
    }

    void Core::UpdateGuideCaptureWindows()
    {
        std::vector<HWND> windows;
        windows.reserve(_overlayUIStates.size());
        for (const auto& overlay : _overlayUIStates)
        {
            windows.push_back(overlay->overlayWindowHandle());
        }
        _guideOverlayManager->SetCaptureExclusionWindows(std::move(windows));
    }

    void Core::StartBoundsTool()
    {
        ResetState();
        _boundsToolState.global.pixelTolerance = _settings.pixelTolerance;
        _boundsToolState.global.perColorChannelEdgeDetection = _settings.perColorChannelEdgeDetection;

#if defined(DEBUG_PRIMARY_MONITOR_ONLY)
        const std::vector<MonitorInfo> monitors = { MonitorInfo::GetPrimaryMonitor() };
#else
        const auto monitors = MonitorInfo::GetMonitors(true);
#endif
        std::vector<std::pair<HWND, MonitorInfo>> captureTargets;
        captureTargets.reserve(monitors.size());
        _boundsToolState.perScreen.reserve(monitors.size());
        for (const auto& monitorInfo : monitors)
        {
            auto overlayUI = OverlayUIState::Create(&dxgiAPI,
                                                    _boundsToolState,
                                                    _commonState,
                                                    monitorInfo);
#if !defined(DEBUG_PRIMARY_MONITOR_ONLY)
            if (!overlayUI)
                continue;
#endif
            const auto window = overlayUI->overlayWindowHandle();
            _boundsToolState.perScreen.try_emplace(window);
            captureTargets.emplace_back(window, monitorInfo);
            _overlayUIStates.push_back(std::move(overlayUI));
        }

        for (const auto& [window, monitor] : captureTargets)
        {
            auto capture = StartBoundsCapturingThread(
                &dxgiAPI,
                _commonState,
                window,
                monitor);
            _boundsToolState.perScreen.at(window).requestSnapFrame = std::move(capture.requestFrame);
            _screenCaptureThreads.emplace_back(std::move(capture.thread));
        }

        for (const auto& overlay : _overlayUIStates)
        {
            overlay->StartUILoop();
        }
        UpdateGuideCaptureWindows();
        _guideOverlayManager->BringToFront();

        trace.UpdateState(true);
        Trace::BoundsToolActivated();
        trace.Flush();
        trace.UpdateState(false);
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
        UpdateGuideCaptureWindows();
        _guideOverlayManager->BringToFront();

        trace.UpdateState(true);
        Trace::MeasureToolActivated();
        trace.Flush();
        trace.UpdateState(false);
    }

    void Core::BeginGuidePlacement(GuideOrientation orientation)
    {
        std::vector<HWND> captureExclusionWindows;
        captureExclusionWindows.reserve(_overlayUIStates.size());
        for (const auto& overlay : _overlayUIStates)
        {
            captureExclusionWindows.push_back(overlay->overlayWindowHandle());
        }

        _guideOverlayManager->BeginPlacement(
            orientation == GuideOrientation::Horizontal ?
                GuideModel::Orientation::Horizontal :
                GuideModel::Orientation::Vertical,
            std::move(captureExclusionWindows));
    }

    void Core::ClearGuides()
    {
        _guideOverlayManager->ClearGuides();
    }

    bool Core::HasGuides()
    {
        return _guideOverlayManager->HasGuides();
    }

    void Core::SetGuidePresenceChangedEvent(GuidePresenceChanged presenceChangedTrigger)
    {
        {
            std::scoped_lock lock{ _guidePresenceChangedMutex };
            _guidePresenceChanged = std::move(presenceChangedTrigger);
        }
        NotifyGuidePresenceChanged(HasGuides());
    }

    void Core::NotifyGuidePresenceChanged(bool hasGuides)
    {
        GuidePresenceChanged callback{ nullptr };
        {
            std::scoped_lock lock{ _guidePresenceChangedMutex };
            callback = _guidePresenceChanged;
        }
        if (callback)
        {
            callback(hasGuides);
        }
    }

    void Core::SetGuideEditMode(bool enabled)
    {
        _guideOverlayManager->SetEditMode(enabled);
    }

    void MeasureToolCore::implementation::Core::SetToolCompletionEvent(ToolSessionCompleted sessionCompletedTrigger)
    {
        _commonState.sessionCompletedCallback = [trigger = std::move(sessionCompletedTrigger)] {
            trigger();
        };
    }

    void Core::SetToolbarWindowHandle(uint64_t windowHandle)
    {
        _guideOverlayManager->SetToolbarWindow(std::bit_cast<HWND>(windowHandle));
    }

    void MeasureToolCore::implementation::Core::SetToolbarBoundingBox(const int32_t fromX,
                                                                      const int32_t fromY,
                                                                      const int32_t toX,
                                                                      const int32_t toY)
    {
        const Box bounds{ RECT{ .left = fromX,
                               .top = fromY,
                               .right = toX,
                               .bottom = toY } };
        _commonState.SetToolbarBoundingBox(bounds);
        _guideOverlayManager->SetToolbarBoundingBox(bounds);
        for (const auto& overlay : _overlayUIStates)
        {
            overlay->RequestToolbarExclusionRegionUpdate();
        }
    }

    float MeasureToolCore::implementation::Core::GetDPIScaleForWindow(uint64_t windowHandle)
    {
        UINT dpi = DPIAware::DEFAULT_DPI;
        DPIAware::GetScreenDPIForWindow(std::bit_cast<HWND>(windowHandle), dpi);
        return static_cast<float>(dpi) / DPIAware::DEFAULT_DPI;
    }
}
