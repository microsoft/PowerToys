#include "ZipFolder.h"
#include <common/utils/timeutil.h>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

void ZipFolder(std::filesystem::path zipPath, std::filesystem::path folderPath)
{
    std::string reportFilename{ "PowerToysReport_" };
    reportFilename += timeutil::format_as_local("%F-%H-%M-%S", timeutil::now());
    reportFilename += ".zip";
    auto finalReportFullPath{ zipPath / reportFilename };

    std::string tempReportFilename{ reportFilename + ".tmp" };
    auto tempReportFullPath{ zipPath / tempReportFilename };

    // tar -c --format=zip -f "reportzipfile" *
    const std::string executable{ R"(c:\windows\system32\tar.exe)" };
    std::string commandline{ executable + R"( -c --format=zip -f ")" };
    commandline += tempReportFullPath.string();
    commandline += R"(" *)";

    const auto folderPathAsString{ folderPath.lexically_normal().string() };

    PROCESS_INFORMATION pi{};
    STARTUPINFOA si{ .cb = sizeof(STARTUPINFOA) };
    if (!CreateProcessA(executable.c_str(),
                        commandline.data() /* must be mutable */,
                        nullptr,
                        nullptr,
                        FALSE,
                        DETACHED_PROCESS,
                        nullptr,
                        folderPathAsString.c_str(),
                        &si,
                        &pi))
    {
        printf("Cannot open zip.");
        throw -1;
    }

    WaitForSingleObject(pi.hProcess, INFINITE);

    std::error_code err{};
    std::filesystem::rename(tempReportFullPath, finalReportFullPath, err);
    if (err.value() != 0)
    {
        wprintf_s(L"Failed to rename %s. Error code: %d\n", tempReportFullPath.native().c_str(), err.value());
    }
}
