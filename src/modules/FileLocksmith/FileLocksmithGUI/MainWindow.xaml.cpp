#include "pch.h"
#include "MainWindow.xaml.h"
#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

#include "../FileLocksmithLib/IPC.h"
#include "../FileLocksmithLib/FileLocksmith.h"

#pragma comment(lib, "shcore") // GetDpiForMonitor

using namespace winrt;
using namespace Microsoft::UI;
using namespace Microsoft::UI::Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace winrt::FileLocksmithGUI::implementation
{
    MainWindow::MainWindow()
    {
        place_and_resize();
        InitializeComponent();
        find_processes();
    }

    void MainWindow::find_processes()
    {
        auto paths = ipc::read_paths_from_stdin();
        m_process_info = find_processes_recursive(paths);

        // TODO move to another thread
        stackPanel().Children().Clear();

        for (const auto& process : m_process_info)
        {
            ProcessEntry entry(process.name, process.pid, process.num_files);
            stackPanel().Children().Append(entry);
        }

        if (m_process_info.empty())
        {
            DisplayNoResultsInfo();
        }
    }

    void MainWindow::place_and_resize()
    {
        // Get native handle
        auto windowNative{ this->try_as<::IWindowNative>() };
        winrt::check_bool(windowNative);
        HWND hwnd{ 0 };
        windowNative->get_WindowHandle(&hwnd);

        // Get mouse cursor position
        POINT cursorPosition{0, 0};
        GetCursorPos(&cursorPosition);
        ::Windows::Graphics::PointInt32 point{ cursorPosition.x, cursorPosition.y };

        // Get monitor area for mouse position
        auto display_area = Windowing::DisplayArea::GetFromPoint(point, Windowing::DisplayAreaFallback::Nearest);
        HMONITOR monitor = MonitorFromPoint(cursorPosition, MONITOR_DEFAULTTOPRIMARY);
        MONITORINFOEXW monitor_info;
        monitor_info.cbSize = sizeof(MONITORINFOEX);
        GetMonitorInfoW(monitor, &monitor_info);
        UINT dpi_x, dpi_y;
        GetDpiForMonitor(monitor, MONITOR_DPI_TYPE::MDT_EFFECTIVE_DPI, &dpi_x, &dpi_y);
        UINT window_dpi = GetDpiForWindow(hwnd);

        int width = 720;
        int height = 405;

        winrt::Windows::Graphics::RectInt32 rect;

        // Scale window size
        rect.Width = (int32_t)(width * (float)window_dpi / dpi_x);
        rect.Height = (int32_t)(height * (float)window_dpi / dpi_y);

        // Center to screen
        rect.X = display_area.WorkArea().X + display_area.WorkArea().Width / 2 - width / 2;
        rect.Y = display_area.WorkArea().Y + display_area.WorkArea().Height / 2 - height / 2;

        // Get app window
        auto window_id = GetWindowIdFromWindow(hwnd);
        auto app_window = Windowing::AppWindow::GetFromWindowId(window_id);
        
        app_window.MoveAndResize(rect);
    }

    void MainWindow::DisplayNoResultsInfo()
    {
        // Construct the UI element and display it
        Controls::TextBlock text;
        text.Text(L"No results.");
        text.HorizontalAlignment(HorizontalAlignment::Center);
        text.VerticalAlignment(VerticalAlignment::Center);
        stackPanel().Children().Append(text);
    }
}
