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
        Title(L"File Locksmith");

        // Set up theme
        find_native_handle();
        native_handle = m_native_handle;
        theme_listener.AddChangedHandler(handle_theme);
        handle_theme();
        
        place_and_resize();
        InitializeComponent();
        
        std::thread([&] {
            find_processes();
        }).detach();

        display_text_info(L"Working...");
    }

    void MainWindow::find_processes()
    {
        auto paths = ipc::read_paths_from_stdin();
        auto process_info = find_processes_recursive(paths);

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

            if (process_info.empty())
            {
                DisplayNoResultsInfo();
            }
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

        int width = 728;
        int height = 405;

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

    void MainWindow::DisplayNoResultsInfo()
    {
        display_text_info(L"No results.");
    }

    void MainWindow::display_text_info(std::wstring text)
    {
        stackPanel().Children().Clear();

        // Construct the UI element and display it
        Controls::TextBlock text_block;
        text_block.Text(text);
        text_block.FontSize(24.0);
        text_block.HorizontalAlignment(HorizontalAlignment::Center);
        text_block.VerticalAlignment(VerticalAlignment::Center);
        stackPanel().Children().Append(text_block);
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
                        return;
                    }
                }
            });
        }
    }
}
