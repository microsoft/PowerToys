#pragma once
#include "MainWindow.g.h"

#include "MeasureToolState.h"

namespace winrt::MeasureTool::implementation
{
    struct MainWindow : MainWindowT<MainWindow>
    {
        MainWindow();
        
        
        void ResetState();
        void StartMeasureTool();
        void MoveToCurrentMonitor();

        int32_t MyProperty();
        void MyProperty(int32_t value);

        void HorizontalMeasuringTool_Click(Windows::Foundation::IInspectable const& sender, Microsoft::UI::Xaml::RoutedEventArgs const& args);
        void VerticalMeasuringTool_Click(Windows::Foundation::IInspectable const& sender, Microsoft::UI::Xaml::RoutedEventArgs const& args);
        void MeasuringTool_Click(Windows::Foundation::IInspectable const& sender, Microsoft::UI::Xaml::RoutedEventArgs const& args);
        void BoundsTool_Click(Windows::Foundation::IInspectable const& sender, Microsoft::UI::Xaml::RoutedEventArgs const& args);

        HWND _overlayUIWindowHandle = {};
        HWND _nativeWindowHandle = {};
        HMONITOR _targetMonitor = nullptr;
        float _targetMonitorScaleRatio = 1.f;

        MeasureToolState _measureToolState;
    };
}

namespace winrt::MeasureTool::factory_implementation
{
    struct MainWindow : MainWindowT<MainWindow, implementation::MainWindow>
    {
    };
}
