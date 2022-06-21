#include "pch.h"
#include "MainWindow.xaml.h"
#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

#include <winrt/Microsoft.UI.Interop.h>
#include <winrt/Microsoft.UI.Windowing.h>
#include <Microsoft.ui.xaml.window.h>

#include <common/display/dpi_aware.h>

#include "ScreenCapturing.h"
#include "OverlayUIDrawing.h"

#include <shellscalingapi.h>

// TODO: into winrt:: ns
using namespace winrt::Windows::Graphics;
using namespace winrt::Microsoft::UI::Windowing;
using namespace winrt::Microsoft::UI::Xaml;

constexpr int32_t WINDOW_WIDTH = 242;
constexpr int32_t WINDOW_HEIGHT = 50;

namespace winrt::MeasureTool::implementation
{
    MainWindow::MainWindow()
    {
#if 0
                while (!IsDebuggerPresent())
                    Sleep(1);
#endif
        InitializeComponent();

        auto windowNative{ this->try_as<::IWindowNative>() };
        winrt::check_bool(windowNative);

        winrt::check_hresult(windowNative->get_WindowHandle(&_nativeWindowHandle));
        Microsoft::UI::WindowId windowId =
            Microsoft::UI::GetWindowIdFromWindow(_nativeWindowHandle);

        AppWindow appWindow = AppWindow::GetFromWindowId(windowId);

        POINT currentCursorPos{ 0 };

        if (GetCursorPos(&currentCursorPos))
        {
            unsigned _targetMonitorDPI = DPIAware::DEFAULT_DPI;
            _targetMonitor = MonitorFromPoint(currentCursorPos, MONITOR_DEFAULTTOPRIMARY);
            DPIAware::GetScreenDPIForMonitor(_targetMonitor, _targetMonitorDPI);
            _targetMonitorScaleRatio = _targetMonitorDPI / static_cast<float>(DPIAware::DEFAULT_DPI);
        }

        SizeInt32 windowSize{ static_cast<int32_t>(WINDOW_WIDTH),
                              static_cast<int32_t>(WINDOW_HEIGHT) };
        appWindow.Resize(windowSize);
        if (auto op = appWindow.Presenter().try_as<OverlappedPresenter>())
        {
            op.IsResizable(false);
            op.IsMinimizable(false);
            op.IsMaximizable(false);
            op.IsAlwaysOnTop(true);
            op.SetBorderAndTitleBar(true, false);
        }

        MoveToCurrentMonitor();
    }

    int32_t MainWindow::MyProperty()
    {
        throw hresult_not_implemented();
    }

    void MainWindow::MyProperty(int32_t /* value */)
    {
        throw hresult_not_implemented();
    }

    void MainWindow::HorizontalMeasuringTool_Click(IInspectable const&, RoutedEventArgs const&)
    {
        ResetState();

        _measureToolState.Access([](auto& state) {
            state.mode = MeasureToolState::Mode::Horizontal;
        });

        StartMeasureTool();
    }

    void MainWindow::VerticalMeasuringTool_Click(IInspectable const&, RoutedEventArgs const&)
    {
        ResetState();

        _measureToolState.Access([](auto& state) {
            state.mode = MeasureToolState::Mode::Vertical;
        });
        
        StartMeasureTool();
    }

    void MainWindow::MeasuringTool_Click(IInspectable const&, RoutedEventArgs const&)
    {
        ResetState();

        _measureToolState.Access([](auto& state) {
            state.mode = MeasureToolState::Mode::Cross;
        });

        StartMeasureTool();
    }

    void MainWindow::BoundsTool_Click(IInspectable const&, RoutedEventArgs const&)
    {
        ResetState();
    }

    void MainWindow::ResetState()
    {
        _measureToolState.Reset();
        DestroyWindow(_overlayUIWindowHandle);
    }

    void MainWindow::StartMeasureTool()
    {
        _overlayUIWindowHandle = DrawOverlayUIThread(_measureToolState, _targetMonitor);
        StartCapturingThread(_measureToolState, _overlayUIWindowHandle, _targetMonitor);
    }

    void MainWindow::MoveToCurrentMonitor()
    {
        SetWindowPos(_nativeWindowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

        MONITORINFOEX monitorInfo;
        monitorInfo.cbSize = sizeof(MONITORINFOEX);
        GetMonitorInfo(_targetMonitor, &monitorInfo);
        RECT rect;

        if (GetWindowRect(_nativeWindowHandle, &rect))
        {
            const int x = monitorInfo.rcWork.left + (monitorInfo.rcWork.right - monitorInfo.rcWork.left) / 2 - static_cast<int>(WINDOW_WIDTH / 2 * _targetMonitorScaleRatio);
            const int y = monitorInfo.rcWork.top;

            const int width = static_cast<int>(WINDOW_WIDTH * _targetMonitorScaleRatio);
            const int height = static_cast<int>(WINDOW_HEIGHT * _targetMonitorScaleRatio);

            SetWindowPos(_nativeWindowHandle,
                         HWND_TOPMOST,
                         x,
                         y,
                         width,
                         height,
                         SWP_SHOWWINDOW | SWP_NOSIZE);
        }
    }
}
