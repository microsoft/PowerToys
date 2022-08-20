#include "pch.h"

#include <common/display/dpi_aware.h>
#include <common/display/monitors.h>
#include <common/utils/logger_helper.h>
#include <common/logger/logger.h>

#include "constants.h"
#include "PowerToys.MeasureToolCore.h"
#include "Core.g.cpp"
#include "OverlayUI.h"
#include "ScreenCapturing.h"

namespace winrt::PowerToys::MeasureToolCore::implementation
{
    void Core::MouseCaptureThread()
    {
        while (!_stopMouseCaptureThreadSignal.is_signaled())
        {
            static_assert(sizeof(_commonState.cursorPos) == sizeof(LONG64));
            POINT cursorPos = {};
            GetCursorPos(&cursorPos);
            InterlockedExchange64(reinterpret_cast<LONG64*>(&_commonState.cursorPos), std::bit_cast<LONG64>(cursorPos));
            std::this_thread::sleep_for(consts::TARGET_FRAME_DURATION);
        }
    }

    Core::Core() :
        _mouseCaptureThread{ [this] { MouseCaptureThread(); } },
        _stopMouseCaptureThreadSignal{ wil::EventOptions::ManualReset }
    {
        LoggerHelpers::init_logger(L"Measure Tool", L"Core", "Measure Tool");
    }

    Core::~Core()
    {
        _stopMouseCaptureThreadSignal.SetEvent();
        _mouseCaptureThread.join();

        ResetState();
    }

    void Core::ResetState()
    {
        _overlayUIStates.clear();
        _boundsToolState = { .commonState = &_commonState };
        _measureToolState.Reset();
        for (auto& thread : _screenCaptureThreads)
        {
            if (thread.joinable())
            {
                thread.join();
            }
        }
        _screenCaptureThreads.clear();

        _settings = Settings::LoadFromFile();

        _commonState.lineColor.r = _settings.lineColor[0] / 255.f;
        _commonState.lineColor.g = _settings.lineColor[1] / 255.f;
        _commonState.lineColor.b = _settings.lineColor[2] / 255.f;
    }

    void Core::StartBoundsTool()
    {
        ResetState();

        for (const auto& monitorInfo : MonitorInfo::GetMonitors(true))
        {
            auto overlayUI = OverlayUIState::Create(_boundsToolState, _commonState, monitorInfo);
            if (!overlayUI)
                continue;
            _overlayUIStates.push_back(std::move(overlayUI));
        }
    }

    void Core::StartMeasureTool(const bool horizontal, const bool vertical)
    {
        ResetState();

        _measureToolState.Access([horizontal, vertical, this](MeasureToolState& state) {
            if (horizontal)
                state.mode = vertical ? MeasureToolState::Mode::Cross : MeasureToolState::Mode::Horizontal;
            else
                state.mode = MeasureToolState::Mode::Vertical;

            state.continuousCapture = _settings.continuousCapture;
            state.drawFeetOnCross = _settings.drawFeetOnCross;
            state.pixelTolerance = _settings.pixelTolerance;
        });

        for (const auto& monitorInfo : MonitorInfo::GetMonitors(true))
        {
            auto overlayUI = OverlayUIState::Create(_measureToolState, _commonState, monitorInfo);
            if (!overlayUI)
                continue;
            _screenCaptureThreads.push_back(StartCapturingThread(_commonState,
                                                                 _measureToolState,
                                                                 overlayUI->overlayWindowHandle(),
                                                                 monitorInfo));
            _overlayUIStates.push_back(std::move(overlayUI));
        }
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
