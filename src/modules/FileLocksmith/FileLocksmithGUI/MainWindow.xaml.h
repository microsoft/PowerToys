#pragma once

#include "MainWindow.g.h"
#include "../FileLocksmithLib/FileLocksmith.h"

namespace winrt::FileLocksmithGUI::implementation
{
    struct MainWindow : MainWindowT<MainWindow>
    {
        MainWindow();
    private:
        void find_processes();
        void find_native_handle();
        void place_and_resize();
        void display_no_results_if_empty();
        void display_progress_ring();
        void watch_process(DWORD pid);

        HWND m_native_handle = NULL;
    };
}

namespace winrt::FileLocksmithGUI::factory_implementation
{
    struct MainWindow : MainWindowT<MainWindow, implementation::MainWindow>
    {
    };
}
