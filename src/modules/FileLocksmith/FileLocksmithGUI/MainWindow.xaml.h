#pragma once

#include "MainWindow.g.h"
#include "../FileLocksmithLib/FileLocksmith.h"

namespace winrt::FileLocksmithGUI::implementation
{
    struct MainWindow : MainWindowT<MainWindow>
    {
        MainWindow();
        void onRefreshClick(winrt::Windows::Foundation::IInspectable const& sender, winrt::Microsoft::UI::Xaml::RoutedEventArgs const& e);
    private:
        void start_finding_processes();
        void find_processes();
        void find_native_handle();
        void place_and_resize();
        void display_no_results_if_empty();
        void display_progress_ring();
        void watch_process(DWORD pid);

        HWND m_native_handle = NULL;
        std::vector<std::wstring> m_paths;
    };
}

namespace winrt::FileLocksmithGUI::factory_implementation
{
    struct MainWindow : MainWindowT<MainWindow, implementation::MainWindow>
    {
    };
}
