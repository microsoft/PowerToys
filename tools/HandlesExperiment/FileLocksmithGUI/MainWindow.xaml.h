#pragma once

#include "MainWindow.g.h"
#include "../FileLocksmithLib/FileLocksmith.h"

namespace winrt::FileLocksmithGUI::implementation
{
    struct MainWindow : MainWindowT<MainWindow>
    {
        MainWindow();

        int32_t MyProperty();
        void MyProperty(int32_t value);

        void myButton_Click(Windows::Foundation::IInspectable const& sender, Microsoft::UI::Xaml::RoutedEventArgs const& args);

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
