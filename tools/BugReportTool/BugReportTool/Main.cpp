#include <cwchar>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>
#include <Shlobj.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.System.UserProfile.h>
#include <winrt/Windows.Globalization.h>

#include "ZipTools/ZipFolder.h"
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/json.h>
#include <common/utils/timeutil.h>
#include <common/utils/exec.h>

#include "Package.h"
#include "ReportMonitorInfo.h"
#include "RegistryUtils.h"
#include "EventViewer.h"
#include "InstallationFolder.h"
#include "ReportGPOValues.h"
#include "ReportPrivateData.h"
#include "ReportSanitizer.h"

using namespace std;
using namespace std::filesystem;
using namespace winrt::Windows::Data::Json;

void ReportWindowsVersion(const filesystem::path& tmpDir)
{
    auto versionReportPath = tmpDir;
    versionReportPath = versionReportPath.append("windows-version.txt");
    OSVERSIONINFOEXW osInfo{};

    try
    {
        NTSTATUS(WINAPI * RtlGetVersion)
        (LPOSVERSIONINFOEXW) = nullptr;
        *reinterpret_cast<FARPROC*>(& RtlGetVersion) = GetProcAddress(GetModuleHandleA("ntdll"), "RtlGetVersion");
        if (RtlGetVersion)
        {
            osInfo.dwOSVersionInfoSize = sizeof(osInfo);
            RtlGetVersion(&osInfo);
        }
    }
    catch (...)
    {
        printf("Failed to get windows version info\n");
        return;
    }

    try
    {
        wofstream versionReport(versionReportPath);
        versionReport << "MajorVersion: " << osInfo.dwMajorVersion << endl;
        versionReport << "MinorVersion: " << osInfo.dwMinorVersion << endl;
        versionReport << "BuildNumber: " << osInfo.dwBuildNumber << endl;
    }
    catch (...)
    {
        printf("Failed to write to %s\n", versionReportPath.string().c_str());
    }
}

void ReportWindowsSettings(const filesystem::path& tmpDir)
{
    std::wstring userLanguage;
    std::wstring userLocale;
    try
    {
        const auto lang = winrt::Windows::System::UserProfile::GlobalizationPreferences::Languages().GetAt(0);
        userLanguage = winrt::Windows::Globalization::Language{ lang }.DisplayName().c_str();
        wchar_t localeName[LOCALE_NAME_MAX_LENGTH]{};
        if (!LCIDToLocaleName(GetThreadLocale(), localeName, LOCALE_NAME_MAX_LENGTH, 0))
        {
            throw -1;
        }
        userLocale = localeName;
    }
    catch (...)
    {
        printf("Failed to get windows settings\n");
        return;
    }

    try
    {
        wofstream settingsReport(tmpDir / "windows-settings.txt");
        settingsReport << "Preferred user language: " << userLanguage << endl;
        settingsReport << "User locale: " << userLocale << endl;
    }
    catch (...)
    {
        printf("Failed to write windows settings\n");
    }
}

void ReportDotNetInstallationInfo(const filesystem::path& tmpDir)
{
    auto dotnetInfoPath = tmpDir;
    dotnetInfoPath.append("dotnet-installation-info.txt");
    try
    {
        wofstream dotnetReport(dotnetInfoPath);
        auto dotnetInfo = exec_and_read_output(LR"(dotnet --list-runtimes)");
        if (!dotnetInfo.has_value())
        {
            printf("Failed to get dotnet installation information\n");
            return;
        }

        dotnetReport << dotnetInfo.value().c_str();
    }
    catch (...)
    {
        printf("Failed to report dotnet installation information");
    }
}

void ReportInstallerLogs(const filesystem::path& tmpDir, const filesystem::path& reportDir)
{
    const char* bootstrapperLogFilePrefix = "powertoys-bootstrapper-msi-";
    const char* PTLogFilePrefix = "PowerToysMSIInstaller_";

    for (auto& entry : directory_iterator{ tmpDir })
    {
        std::error_code ec;
        if (entry.is_directory(ec) || !entry.path().has_filename())
        {
            continue;
        }

        const auto fileName = entry.path().filename().string();
        if (!fileName.starts_with(bootstrapperLogFilePrefix) && !fileName.starts_with(PTLogFilePrefix))
        {
            continue;
        }
        copy(entry.path(), reportDir / fileName, ec);
    }
}

int wmain(int argc, wchar_t* argv[], wchar_t*)
{
    // Get path to save zip
    wstring saveZipPath;
    if (argc > 1)
    {
        saveZipPath = argv[1];
    }
    else
    {
        wchar_t buffer[MAX_PATH];
        if (SHGetSpecialFolderPath(HWND_DESKTOP, buffer, CSIDL_DESKTOP, FALSE))
        {
            saveZipPath = buffer;
        }
        else
        {
            printf("Failed to retrieve the desktop path. Error code: %d\n", GetLastError());
            return 1;
        }
    }

    auto settingsRootPath = PTSettingsHelper::get_root_save_folder_location();
    settingsRootPath += L"\\";

    auto localLowPath = PTSettingsHelper::get_local_low_folder_location();
    localLowPath += L"\\logs\\";

    const auto tempDir = temp_directory_path();
    auto reportDir = temp_directory_path() / "PowerToys\\";
    if (!DeleteFolder(reportDir))
    {
        // A copy left by an earlier run may hold data that was never hidden: say where it is.
        wprintf_s(L"Failed to delete the working copy left by an earlier run in %s; delete it by hand\n", reportDir.c_str());
    }

    try
    {
        create_directory(reportDir, settingsRootPath);
        for (const auto& entry : directory_iterator(settingsRootPath))
        {
            const auto filename = entry.path().filename();
            // Runner holds this synchronization file exclusively for its entire lifetime.
            if (_wcsicmp(filename.c_str(), L"settings.json.cli-lock") == 0)
            {
                continue;
            }

            copy(entry.path(), reportDir / filename, copy_options::recursive);
        }

        // Remove updates folder contents
        DeleteFolder(reportDir / "Updates");
    }
	
    catch (...)
    {
        printf("Failed to copy PowerToys folder\n");
        DeleteReportCopyOrSayWhere(reportDir);
        return 1;
    }

    try
    {
        copy(localLowPath, reportDir, copy_options::recursive);
    }

    catch (...)
    {
        printf("Failed to copy logs saved in LocalLow\n");
    }

#ifndef _DEBUG
    InstallationFolder::ReportStructure(reportDir);
#endif

    // Hide sensitive information
    if (!HideUserPrivateInfo(reportDir))
    {
        printf("Failed to remove private Keyboard Manager data from the report; no report was created\n");
        DeleteReportCopyOrSayWhere(reportDir);
        return 1;
    }

    // Write windows settings to the temporary folder
    ReportWindowsSettings(reportDir);

    // Write monitors info to the temporary folder
    ReportMonitorInfo(reportDir);

    // Write windows version info to the temporary folder
    ReportWindowsVersion(reportDir);

    // Write dotnet installation info to the temporary folder
    ReportDotNetInstallationInfo(reportDir);

    // Write registry to the temporary folder
    ReportRegistry(reportDir);

    // Write gpo policies to the temporary folder
    ReportGPOValues(reportDir);

    // Write compatibility tab info to the temporary folder
    ReportCompatibilityTab(reportDir);

    // Write event viewer logs info to the temporary folder
    EventViewer::ReportEventViewerInfo(reportDir);

    // Write AppXDeployment-Server event logs to the temporary folder
    EventViewer::ReportAppXDeploymentLogs(reportDir);

    ReportInstallerLogs(tempDir, reportDir);

    ReportInstalledContextMenuPackages(reportDir);

    std::vector<path> removedPaths;
    try
    {
        removedPaths = ReportSanitizer::Sanitize(reportDir);
    }
    catch (const std::exception& error)
    {
        printf("Failed to redact report data: %s\n", error.what());
        DeleteReportCopyOrSayWhere(reportDir);
        return 1;
    }

    for (const auto& removedPath : removedPaths)
    {
        wprintf_s(L"Report file or folder removed because it could not be sanitized: %s\n", removedPath.lexically_relative(reportDir).c_str());
    }

    // Zip folder
    auto zipPath = path::path(saveZipPath);

    try
    {
        ZipFolder(zipPath, reportDir);
    }
    catch (...)
    {
        printf("Failed to zip folder\n");
        return 1;
    }

    DeleteFolder(reportDir);
    return 0;
}
