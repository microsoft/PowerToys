#include "pch.h"

#include <common/display/dpi_aware.h>
#include <common/utils/logger_helper.h>
#include <common/logger/logger.h>

#include "PowerToys.MeasureToolCore.h"
#include "Core.g.cpp"
#include "OverlayUIDrawing.h"
#include "ScreenCapturing.h"

namespace winrt::PowerToys::MeasureToolCore::implementation
{
    Core::Core()
    {
        LoggerHelpers::init_logger(L"Measure Tool", L"Core", "Measure Tool");
    }

    void Core::ResetState()
    {
        _boundsToolState = {};
        _measureToolState.Reset();
        POINT currentCursorPos{};

        if (GetCursorPos(&currentCursorPos))
        {
            unsigned _targetMonitorDPI = DPIAware::DEFAULT_DPI;
            _targetMonitor = MonitorFromPoint(currentCursorPos, MONITOR_DEFAULTTOPRIMARY);
            DPIAware::GetScreenDPIForMonitor(_targetMonitor, _targetMonitorDPI);
            _targetMonitorScaleRatio = _targetMonitorDPI / static_cast<float>(DPIAware::DEFAULT_DPI);
        }

        DestroyWindow(_overlayUIWindowHandle);

        _settings = Settings::LoadFromFile();
    }

    void Core::StartMeasureTool(const bool horizontal, const bool vertical)
    {
        ResetState();

        _measureToolState.Access([horizontal, vertical, this](MeasureToolState::State& state) {
            if (horizontal)
                state.mode = vertical ? MeasureToolState::Mode::Cross : MeasureToolState::Mode::Horizontal;
            else
                state.mode = MeasureToolState::Mode::Vertical;

            state.continuousCapture = _settings.continuousCapture;

            state.crossColor.r = _settings.lineColor[0] / 255.f;
            state.crossColor.g = _settings.lineColor[1] / 255.f;
            state.crossColor.b = _settings.lineColor[2] / 255.f;
        });

        _overlayUIWindowHandle = LaunchOverlayUI(_measureToolState, _targetMonitor);
        StartCapturingThread(_measureToolState, _overlayUIWindowHandle, _targetMonitor);
    }

    void Core::StartBoundsTool()
    {
        ResetState();

        _boundsToolState.lineColor.r = _settings.lineColor[0] / 255.f;
        _boundsToolState.lineColor.g = _settings.lineColor[1] / 255.f;
        _boundsToolState.lineColor.b = _settings.lineColor[2] / 255.f;

        _overlayUIWindowHandle = LaunchOverlayUI(_boundsToolState, _targetMonitor);
    }

    winrt::PowerToys::MeasureToolCore::Point MeasureToolCore::implementation::Core::GetCursorPosition()
    {
        winrt::PowerToys::MeasureToolCore::Point result;
        POINT cursorPos = {};

        GetCursorPos(&cursorPos);

        result.X = cursorPos.x;
        result.Y = cursorPos.y;

        return result;
    }
}
