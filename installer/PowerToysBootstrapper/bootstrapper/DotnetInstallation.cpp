#include "pch.h"

#include "DotnetInstallation.h"

#include <common/utils/exec.h>
#include <common/utils/HttpClient.h>
#include <common/utils/winapi_error.h>
namespace fs = std::filesystem;

namespace updating
{
    bool dotnet_is_installed(const size_t major, const size_t minor, const size_t requiredMinimalPatch)
    {
        auto runtimes = exec_and_read_output(LR"(dotnet --list-runtimes)");
        if (!runtimes)
        {
            return false;
        }
        std::array<char, 512> regexBuffer;
        sprintf_s(regexBuffer.data(),
                  regexBuffer.size(),
                  R"(Microsoft\.WindowsDesktop\.App\s%zu\.%zu\.(\d+))",
                  major,
                  minor);
        std::regex dotnetRegex{ regexBuffer.data() };

        size_t latestPatchInstalled = 0;
        using rexit = std::sregex_iterator;
        for (auto it = rexit{ begin(*runtimes), end(*runtimes), dotnetRegex }; it != rexit{}; ++it)
        {
            if (!it->ready() || it->size() < 2)
            {
                continue;
            }
            auto patchNumberGroup = (*it)[1];
            if (!patchNumberGroup.matched)
            {
                continue;
            }
            const auto patchString = patchNumberGroup.str();
            size_t patch = 0;
            if (auto [_, ec] = std::from_chars(&*begin(patchString), &*end(patchString), patch); ec == std::errc())
            {
                latestPatchInstalled = std::max(patch, latestPatchInstalled);
            }
        }
        return latestPatchInstalled >= requiredMinimalPatch;
    }

    std::optional<fs::path> download_dotnet(const wchar_t* dotnetDesktopDownloadLink)
    {
        const wchar_t DOTNET_DESKTOP_FILENAME[] = L"windowsdesktop-runtime.exe";

        auto dotnet_download_path = fs::temp_directory_path() / DOTNET_DESKTOP_FILENAME;
        winrt::Windows::Foundation::Uri download_link{ dotnetDesktopDownloadLink };

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