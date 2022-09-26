#include "pch.h"
#include "ProcessEntry.xaml.h"
#if __has_include("ProcessEntry.g.cpp")
#include "ProcessEntry.g.cpp"
#endif

using namespace winrt;
using namespace Microsoft::UI::Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace winrt::FileLocksmithGUI::implementation
{
    ProcessEntry::ProcessEntry(const winrt::hstring& process, DWORD pid, uint64_t num_files)
    {
        InitializeComponent();
        processName().Text(process);

        auto processPidStr = L"Process ID: " + std::to_wstring(pid);
        auto processFileCountStr = L"Files used: " + std::to_wstring(num_files);

        processPid().Text(processPidStr);
        processFileCount().Text(processFileCountStr);

        m_pid = pid;
    }

    void ProcessEntry::killProcessClick(Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
    {
        HANDLE process = OpenProcess(PROCESS_TERMINATE, FALSE, m_pid);
        if (!process || !TerminateProcess(process, 1))
        {
            MessageBoxW(NULL, L"Failed to kill process.", L"Error", MB_OK);
            return;
        }

        CloseHandle(process);
    }
    
    DWORD ProcessEntry::Pid()
    {
        return m_pid;
    }
}
