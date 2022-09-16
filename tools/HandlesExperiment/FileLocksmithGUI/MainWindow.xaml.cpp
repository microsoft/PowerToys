#include "pch.h"
#include "MainWindow.xaml.h"
#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

#include "../FileLocksmithLib/IPC.h"
#include "../FileLocksmithLib/FileLocksmith.h"

using namespace winrt;
using namespace Microsoft::UI::Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace winrt::FileLocksmithGUI::implementation
{
    MainWindow::MainWindow()
    {
        InitializeComponent();
        find_processes();
    }

    int32_t MainWindow::MyProperty()
    {
        throw hresult_not_implemented();
    }

    void MainWindow::MyProperty(int32_t /* value */)
    {
        throw hresult_not_implemented();
    }

    void MainWindow::myButton_Click(IInspectable const&, RoutedEventArgs const&)
    {
    }
    
    void MainWindow::find_processes()
    {
        auto paths = ipc::read_paths_from_stdin();
        m_process_info = find_processes_nonrecursive(paths);

        // TODO move to another thread
        stackPanel().Children().Clear();
        for (const auto& process : m_process_info)
        {
            ProcessEntry entry(process.name, process.pid);
            stackPanel().Children().Append(entry);
        }
    }
}
