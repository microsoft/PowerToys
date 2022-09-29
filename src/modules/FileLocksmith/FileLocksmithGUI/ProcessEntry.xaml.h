#pragma once

#include "winrt/Microsoft.UI.Xaml.h"
#include "winrt/Microsoft.UI.Xaml.Markup.h"
#include "winrt/Microsoft.UI.Xaml.Controls.Primitives.h"
#include "ProcessEntry.g.h"

namespace winrt::FileLocksmithGUI::implementation
{
    struct ProcessEntry : ProcessEntryT<ProcessEntry>
    {
        ProcessEntry(const winrt::hstring& process, DWORD pid, uint64_t num_files);
        DWORD Pid();
        void AddPath(const winrt::hstring& path);

        void killProcessClick(winrt::Windows::Foundation::IInspectable const& sender, winrt::Microsoft::UI::Xaml::RoutedEventArgs const& e);
    private:
        DWORD m_pid;
    };
}

namespace winrt::FileLocksmithGUI::factory_implementation
{
    struct ProcessEntry : ProcessEntryT<ProcessEntry, implementation::ProcessEntry>
    {
    };
}
