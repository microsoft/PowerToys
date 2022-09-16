#pragma once

#include "MainWindow.g.h"
#include "../FileLocksmithLib/FileLocksmith.h"

namespace winrt::FileLocksmithGUI::implementation
{
    struct MainWindow : MainWindowT<MainWindow>
    {
        MainWindow();

    private:
        std::vector<ProcessResult> m_process_info;
        void find_processes();
    };
}

namespace winrt::FileLocksmithGUI::factory_implementation
{
    struct MainWindow : MainWindowT<MainWindow, implementation::MainWindow>
    {
    };
}
