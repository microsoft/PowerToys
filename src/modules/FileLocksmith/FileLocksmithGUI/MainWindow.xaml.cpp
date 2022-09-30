#include "pch.h"
#include "MainWindow.xaml.h"
#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

#include "../FileLocksmithLib/IPC.h"
#include "../FileLocksmithLib/FileLocksmith.h"

#include "../../../common/Themes/theme_helpers.h"
#include "../../../common/Themes/theme_listener.h"

#pragma comment(lib, "shcore") // GetDpiForMonitor
#pragma comment(lib, "dwmapi") // Themes

using namespace winrt;
using namespace Microsoft::UI;
using namespace Microsoft::UI::Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace
{
    ThemeListener theme_listener{};
    HWND native_handle;

    void handle_theme()
    {
        auto theme = theme_listener.AppTheme;
        auto isDark = theme == AppTheme::Dark;
        ThemeHelpers::SetImmersiveDarkMode(native_handle, isDark);
    }
}

namespace winrt::FileLocksmithGUI::implementation
{
    MainWindow::MainWindow()
    {
        m_paths = ipc::read_paths_from_stdin();
        
        Title(L"File Locksmith");

        // Set up theme
        find_native_handle();
        native_handle = m_native_handle;
        theme_listener.AddChangedHandler(handle_theme);
        handle_theme();
        
        place_and_resize();
        InitializeComponent();
        
        start_finding_processes();
    }

    void MainWindow::start_finding_processes()
    {
        std::thread([&] {
            find_processes();
        }).detach();

        display_progress_ring();
    }

    void MainWindow::find_processes()
    {
        auto process_info = find_processes_recursive(m_paths);

        // Show results using the UI thread
        DispatcherQueue().TryEnqueue([&, process_info = std::move(process_info)] {
            stackPanel().Children().Clear();

            for (const auto& process : process_info)
            {
                ProcessEntry entry(process.name, process.pid, process.files.size());

                for (auto path : process.files)
                {
                    entry.AddPath(path);
                }

                stackPanel().Children().Append(entry);

                // Launch a thread to erase this entry if the process exits
                std::thread([&, pid = process.pid] {
                    watch_process(pid);
                }).detach();
            }

            display_no_results_if_empty();
        });
    }

    void MainWindow::find_native_handle()
    {
        auto windowNative{ this->try_as<::IWindowNative>() };
        winrt::check_bool(windowNative);
        windowNative->get_WindowHandle(&m_native_handle);
    }

    void MainWindow::place_and_resize()
    {
        if (!m_native_handle)
        {
            find_native_handle();
        }

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
        UINT window_dpi = GetDpiForWindow(m_native_handle);

        int width = 758;
        int height = 480;

        winrt::Windows::Graphics::RectInt32 rect;

        // Scale window size
        rect.Width = (int32_t)(width * (float)window_dpi / dpi_x);
        rect.Height = (int32_t)(height * (float)window_dpi / dpi_y);

        // Center to screen
        rect.X = display_area.WorkArea().X + display_area.WorkArea().Width / 2 - width / 2;
        rect.Y = display_area.WorkArea().Y + display_area.WorkArea().Height / 2 - height / 2;

        // Get app window
        auto window_id = GetWindowIdFromWindow(m_native_handle);
        auto app_window = Windowing::AppWindow::GetFromWindowId(window_id);
        
        app_window.MoveAndResize(rect);
    }

    void MainWindow::display_no_results_if_empty()
    {
        if (stackPanel().Children().Size() == 0)
        {
            // Construct the UI element and display it
            Controls::TextBlock text_block;
            
            text_block.Text(L"No results.");
            text_block.FontSize(24.0);
            text_block.HorizontalAlignment(HorizontalAlignment::Center);
            text_block.VerticalAlignment(VerticalAlignment::Center);
            
            stackPanel().Children().Append(text_block);
        }
    }

    void MainWindow::display_progress_ring()
    {
        stackPanel().Children().Clear();

        Controls::ProgressRing ring;
        ring.Width(64);
        ring.Height(64);
        ring.Margin(Thickness{ .Top = 16 });
        ring.IsIndeterminate(true);

        stackPanel().Children().Append(ring);
    }

    void MainWindow::watch_process(DWORD pid)
    {
        HANDLE process = OpenProcess(SYNCHRONIZE, FALSE, pid);

        if (!process)
        {
            return;
        }

        auto wait_result = WaitForSingleObject(process, INFINITE);
        CloseHandle(process);

        if (wait_result == WAIT_OBJECT_0)
        {
            // Find entry with this PID and erase it
            DispatcherQueue().TryEnqueue([&, pid] {

                for (uint32_t i = 0; i < stackPanel().Children().Size(); i++)
                {
                    auto element = stackPanel().Children().GetAt(i);
                    auto process_entry = element.try_as<ProcessEntry>();
                    if (!process_entry)
                    {
                        continue;
                    }

                    if (process_entry.Pid() == pid)
                    {
                        stackPanel().Children().RemoveAt(i);
                        display_no_results_if_empty();
                        return;
                    }
                }
            });
        }
    }
    
    void MainWindow::onRefreshClick(Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
    {
        start_finding_processes();
    }
}
