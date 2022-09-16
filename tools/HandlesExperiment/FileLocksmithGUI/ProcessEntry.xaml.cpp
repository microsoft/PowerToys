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
    ProcessEntry::ProcessEntry(const winrt::hstring& process, int pid)
    {
        InitializeComponent();
        processName().Text(process);
        processPid().Text(winrt::to_hstring(pid));
    }

    void ProcessEntry::killProcessClick(Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
    {
        MessageBoxW(NULL, L"Kill process", L"OK", MB_OK);
    }
}



