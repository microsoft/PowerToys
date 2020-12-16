#include "pch.h"

#include "dotnet_installation.h"
#include "http_client.h"

#include "utils/exec.h"
#include "utils/winapi_error.h"

namespace fs = std::filesystem;

namespace updating
{
    bool dotnet_is_installed()
    {
        auto runtimes = exec_and_read_output(LR"(dotnet --list-runtimes)");
        if (!runtimes)
        {
            return false;
        }
        const char DESKTOP_DOTNET_RUNTIME_STRING[] = "Microsoft.WindowsDesktop.App 3.1.";
        return runtimes->find(DESKTOP_DOTNET_RUNTIME_STRING) != std::string::npos;
    }

    std::optional<fs::path> download_dotnet()
    {
        const wchar_t DOTNET_DESKTOP_DOWNLOAD_LINK[] = L"https://download.visualstudio.microsoft.com/download/pr/513acf37-8da2-497d-bdaa-84d6e33c1fee/eb7b010350df712c752f4ec4b615f89d/windowsdesktop-runtime-3.1.10-win-x64.exe";
        const wchar_t DOTNET_DESKTOP_FILENAME[] = L"windowsdesktop-runtime.exe";

        auto dotnet_download_path = fs::temp_directory_path() / DOTNET_DESKTOP_FILENAME;
        winrt::Windows::Foundation::Uri download_link{ DOTNET_DESKTOP_DOWNLOAD_LINK };

        const size_t max_attempts = 3;
        bool download_success = false;
        for (size_t i = 0; i < max_attempts; ++i)
        {
            try
            {
                http::HttpClient client;
                client.download(download_link, dotnet_download_path).wait();
                download_success = true;
                break;
            }
            catch (...)
            {
                // couldn't download
            }
        }
        return download_success ? std::make_optional(dotnet_download_path) : std::nullopt;
    }

    bool install_dotnet(fs::path dotnet_download_path, const bool silent = false)
    {
        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOASYNC | SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NO_CONSOLE };
        sei.lpFile = dotnet_download_path.c_str();
        sei.nShow = SW_SHOWNORMAL;
        std::wstring dotnet_flags = L"/install ";
        dotnet_flags += silent ? L"/quiet" : L"/passive";
        sei.lpParameters = dotnet_flags.c_str();
        if (ShellExecuteExW(&sei) != TRUE)
        {
            return false;
        }
        WaitForSingleObject(sei.hProcess, INFINITE);
        CloseHandle(sei.hProcess);
        return true;
    }
}