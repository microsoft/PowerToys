#include "pch.h"

#include "App.xaml.h"
#include "MainWindow.xaml.h"

#include <vector>
#include <string>
#include <filesystem>

#include <common/logger/logger.h>
#include <common/logger/logger_settings.h>
#include <common/utils/logger_helper.h>
#include <common/utils/gpo.h>

using namespace winrt;
using namespace winrt::Microsoft::UI::Xaml;
using namespace winrt::Microsoft::UI::Xaml::Controls;
using namespace winrt::Microsoft::UI::Xaml::Navigation;
using namespace PowerRenameUI;
using namespace PowerRenameUI::implementation;

namespace fs = std::filesystem;

//#define DEBUG_BENCHMARK_100K_ENTRIES

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
    UnhandledException([this](IInspectable const&, UnhandledExceptionEventArgs const& e) {
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

    if (powertoys_gpo::getConfiguredPowerRenameEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        Logger::warn(L"Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
        ExitProcess(0);
    }

    auto args = std::wstring{ GetCommandLine() };
    size_t pos{ args.rfind(L"\\\\.\\pipe\\") };

    std::wstring pipe_name;
    if (pos != std::wstring::npos)
    {
        pipe_name = args.substr(pos);
    }

    HANDLE hStdin;

    if (pipe_name.size() > 0)
    {
        while (1)
        {
            hStdin = CreateFile(
                pipe_name.c_str(), // pipe name
                GENERIC_READ | GENERIC_WRITE, // read and write
                0, // no sharing
                NULL, // default security attributes
                OPEN_EXISTING, // opens existing pipe
                0, // default attributes
                NULL); // no template file

            // Break if the pipe handle is valid.
            if (hStdin != INVALID_HANDLE_VALUE)
                break;

            // Exit if an error other than ERROR_PIPE_BUSY occurs.
            auto error = GetLastError();
            if (error != ERROR_PIPE_BUSY)
            {
                break;
            }

            if (!WaitNamedPipe(pipe_name.c_str(), 3))
            {
                printf("Could not open pipe: 20 second wait timed out.");
            }
        }
    }
    else
    {
        hStdin = GetStdHandle(STD_INPUT_HANDLE);
    }

    if (hStdin == INVALID_HANDLE_VALUE)
    {
        Logger::error(L"Invalid input handle.");
        ExitProcess(1);
    }

#ifdef DEBUG_BENCHMARK_100K_ENTRIES
    const std::wstring_view ROOT_PATH = L"R:\\PowerRenameBenchmark";

    std::wstring subdirectory_name = L"0";
    std::error_code _;

#if 1
    constexpr bool recreate_files = true;
#else
    constexpr bool recreate_files = false;
#endif
    if constexpr (recreate_files)
        fs::remove_all(ROOT_PATH, _);

    g_files.push_back(fs::path{ ROOT_PATH });
    constexpr int pow2_threshold = 10;
    constexpr int num_files = 100'000;
    for (int i = 0; i < num_files; ++i)
    {
        fs::path file_path{ ROOT_PATH };
        // Create a subdirectory for each subsequent 2^pow2_threshold files, o/w filesystem becomes too slow to create them in a reasonable time.
        if ((i & ((1 << pow2_threshold) - 1)) == 0)
        {
            subdirectory_name = std::to_wstring(i >> pow2_threshold);
        }

        file_path /= subdirectory_name;

        if constexpr (recreate_files)
        {
            fs::create_directories(file_path, _);
            file_path /= std::to_wstring(i) + L".txt";
            HANDLE hFile = CreateFileW(
                file_path.c_str(),
                GENERIC_WRITE,
                0,
                nullptr,
                CREATE_NEW,
                FILE_ATTRIBUTE_NORMAL,
                nullptr);
            CloseHandle(hFile);
        }
    }
#else
#define BUFSIZE 4096 * 4

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
    CloseHandle(hStdin);
#endif

    Logger::debug(L"Starting PowerRename with {} files selected", g_files.size());

    window = make<MainWindow>();
    window.Activate();
}