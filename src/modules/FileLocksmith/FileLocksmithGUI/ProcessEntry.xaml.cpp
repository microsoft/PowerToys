#include "pch.h"
#include "ProcessEntry.xaml.h"
#if __has_include("ProcessEntry.g.cpp")
#include "ProcessEntry.g.cpp"
#endif

#include "../FileLocksmithLib/FileLocksmith.h"

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
        auto processUserStr = L"User: " + pid_to_user(pid);

        processPid().Text(processPidStr);
        processFileCount().Text(processFileCountStr);
        processUser().Text(processUserStr);

        m_pid = pid;
    }
    
    DWORD ProcessEntry::Pid()
    {
        return m_pid;
    }

    void ProcessEntry::AddPath(const winrt::hstring& path)
    {
        m_paths.push_back(path);
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

    void ProcessEntry::showFilesClick(Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
    {
        if (!m_files_shown)
        {
            for (const auto& path : m_paths)
            {
                Controls::TextBlock row;
                row.Text(path);
                filesContainer().Children().Append(row);
            }

            showFilesButton().Content(box_value(L"Hide files"));
        }
        else
        {
            filesContainer().Children().Clear();
            showFilesButton().Content(box_value(L"Show files"));
        }

        m_files_shown ^= true;
    }
}
