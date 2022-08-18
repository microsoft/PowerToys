#include "pch.h"

#include <common/display/dpi_aware.h>
#include <common/utils/logger_helper.h>
#include <common/logger/logger.h>

#include "PowerToys.MeasureToolCore.h"
#include "Core.g.cpp"
#include "OverlayUI.h"
#include "ScreenCapturing.h"

namespace winrt::PowerToys::MeasureToolCore::implementation
{
    Core::Core()
    {
        LoggerHelpers::init_logger(L"Measure Tool", L"Core", "Measure Tool");
    }

    void Core::ResetState()
    {
        _overlayUIState = {};
        _boundsToolState = {};
        _measureToolState.Reset();

        _settings = Settings::LoadFromFile();

        _commonState.lineColor.r = _settings.lineColor[0] / 255.f;
        _commonState.lineColor.g = _settings.lineColor[1] / 255.f;
        _commonState.lineColor.b = _settings.lineColor[2] / 255.f;

        POINT currentCursorPos{};
        if (GetCursorPos(&currentCursorPos))
        {
            _commonState.monitor = MonitorFromPoint(currentCursorPos, MONITOR_DEFAULTTOPRIMARY);
        }
    }

    void Core::StartBoundsTool()
    {
        ResetState();

        _overlayUIState = OverlayUIState::Create(_boundsToolState, _commonState);
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

        _overlayUIState = OverlayUIState::Create(_measureToolState, _commonState);
        if (_overlayUIState)
        {
            StartCapturingThread(_measureToolState, _overlayUIState->overlayWindowHandle(), _commonState.monitor);
        }
    }

    void MeasureToolCore::implementation::Core::SetToolCompletionEvent(ToolSessionCompleted sessionCompletedTrigger)
    {
        _commonState.sessionCompletedCallback = [trigger = std::move(sessionCompletedTrigger)] {
            trigger();
        };
    }

    void MeasureToolCore::implementation::Core::SetToolbarBoundingBox(const uint32_t fromX, const uint32_t fromY, const uint32_t toX, const uint32_t toY)
    {
        _commonState.toolbarBoundingBox.left = fromX;
        _commonState.toolbarBoundingBox.right = toX;
        _commonState.toolbarBoundingBox.top = fromY;
        _commonState.toolbarBoundingBox.bottom = toY;
    }

    float MeasureToolCore::implementation::Core::GetDPIScaleForWindow(uint64_t windowHandle)
    {
        UINT dpi = DPIAware::DEFAULT_DPI;
        DPIAware::GetScreenDPIForWindow(std::bit_cast<HWND>(windowHandle), dpi);
        return static_cast<float>(dpi) / DPIAware::DEFAULT_DPI;
    }
}
