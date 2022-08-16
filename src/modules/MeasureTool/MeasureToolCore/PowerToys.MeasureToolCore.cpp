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
            _commonState.monitor = MonitorFromPoint(currentCursorPos, MONITOR_DEFAULTTOPRIMARY);
        }

        PostMessageW(_overlayUIWindowHandle, WM_CLOSE, {}, {});

        _settings = Settings::LoadFromFile();

        while (IsWindow(_overlayUIWindowHandle))
        {
            Sleep(20);
        }
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
            state.drawFeetOnCross = _settings.drawFeetOnCross;

            state.crossColor.r = _settings.lineColor[0] / 255.f;
            state.crossColor.g = _settings.lineColor[1] / 255.f;
            state.crossColor.b = _settings.lineColor[2] / 255.f;

            state.pixelTolerance = _settings.pixelTolerance;
        });

        _overlayUIWindowHandle = LaunchOverlayUI(_measureToolState, _commonState);
        StartCapturingThread(_measureToolState, _overlayUIWindowHandle, _commonState.monitor);
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

    void Core::StartBoundsTool()
    {
        ResetState();

        _boundsToolState.lineColor.r = _settings.lineColor[0] / 255.f;
        _boundsToolState.lineColor.g = _settings.lineColor[1] / 255.f;
        _boundsToolState.lineColor.b = _settings.lineColor[2] / 255.f;

        _overlayUIWindowHandle = LaunchOverlayUI(_boundsToolState, _commonState);
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

    float MeasureToolCore::implementation::Core::GetDPIScaleForWindow(uint64_t windowHandle)
    {
        UINT dpi = DPIAware::DEFAULT_DPI;
        DPIAware::GetScreenDPIForWindow(std::bit_cast<HWND>(windowHandle), dpi);
        return static_cast<float>(dpi) / DPIAware::DEFAULT_DPI;
    }
}
