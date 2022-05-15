#include "pch.h"

#include "App.xaml.h"
#include "MainWindow.xaml.h"

#include <vector>
#include <string>

#include <common/logger/logger.h>
#include <common/logger/logger_settings.h>
#include <common/utils/logger_helper.h>

using namespace winrt;
using namespace winrt::Microsoft::UI::Xaml;
using namespace winrt::Microsoft::UI::Xaml::Controls;
using namespace winrt::Microsoft::UI::Xaml::Navigation;
using namespace PowerRenameUI;
using namespace PowerRenameUI::implementation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

std::vector<std::wstring> g_files;

const std::wstring moduleName = L"PowerRename";

/// <summary>
/// Initializes the singleton application object.  This is the first line of authored code
/// executed, and as such is the logical equivalent of main() or WinMain().
/// </summary>
App::App()
{
    InitializeComponent();

#if defined _DEBUG && !defined DISABLE_XAML_GENERATED_BREAK_ON_UNHANDLED_EXCEPTION
    UnhandledException([this](IInspectable const&, UnhandledExceptionEventArgs const& e)
    {
        if (IsDebuggerPresent())
        {
            auto errorMessage = e.Message();
            __debugbreak();
        }
    });
#endif
}

/// <summary>
/// Invoked when the application is launched normally by the end user.  Other entry points
/// will be used such as when the application is launched to open a specific file.
/// </summary>
/// <param name="e">Details about the launch request and process.</param>
void App::OnLaunched(LaunchActivatedEventArgs const&)
{
    LoggerHelpers::init_logger(moduleName, L"", LogSettings::powerRenameLoggerName);

#define BUFSIZE 4096 * 4

    HANDLE hStdin = GetStdHandle(STD_INPUT_HANDLE);
    if (hStdin == INVALID_HANDLE_VALUE)
    {
        Logger::error(L"Invalid input handle.");
        ExitProcess(1);
    }

    BOOL bSuccess;
    WCHAR chBuf[BUFSIZE];
    DWORD dwRead;
    for (;;)
    {
        // Read from standard input and stop on error or no data.
        bSuccess = ReadFile(hStdin, chBuf, BUFSIZE * sizeof(wchar_t), &dwRead, NULL);

        if (!bSuccess || dwRead == 0)
            break;

        std::wstring inputBatch{ chBuf, dwRead / sizeof(wchar_t) };

        std::wstringstream ss(inputBatch);
        std::wstring item;
        wchar_t delimiter = '?';
        while (std::getline(ss, item, delimiter))
        {
            g_files.push_back(item);
        }

        if (!bSuccess)
            break;
    }

    Logger::debug(L"Starting PowerRename with {} files selected", g_files.size());

    window = make<MainWindow>();
    window.Activate();
}