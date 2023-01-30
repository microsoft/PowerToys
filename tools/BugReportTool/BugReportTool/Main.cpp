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

using namespace std;
using namespace std::filesystem;
using namespace winrt::Windows::Data::Json;

map<wstring, vector<wstring>> escapeInfo = {
    { L"FancyZones\\app-zone-history.json", { L"app-zone-history/app-path" } },
    { L"FancyZones\\settings.json", { L"properties/fancyzones_excluded_apps" } }
};

vector<wstring> filesToDelete = {
    L"PowerToys Run\\Cache",
    L"PowerRename\\replace-mru.json",
    L"PowerRename\\search-mru.json",
    L"PowerToys Run\\Settings\\UserSelectedRecord.json",
    L"PowerToys Run\\Settings\\QueryHistory.json"
};

vector<wstring> GetXpathArray(wstring xpath)
{
    vector<wstring> result;
    wstring cur = L"";
    for (auto ch : xpath)
    {
        if (ch == L'/')
        {
            result.push_back(cur);
            cur = L"";
            continue;
        }

        cur += ch;
    }

    if (!cur.empty())
    {
        result.push_back(cur);
    }

    return result;
}

void HideByXPath(IJsonValue& val, vector<wstring>& xpathArray, int p)
{
    if (val.ValueType() == JsonValueType::Array)
    {
        for (auto it : val.GetArray())
        {
            HideByXPath(it, xpathArray, p);
        }

        return;
    }

    if (p == xpathArray.size() - 1)
    {
        if (val.ValueType() == JsonValueType::Object)
        {
            auto obj = val.GetObjectW();
            if (obj.HasKey(xpathArray[p]))
            {
                auto privateDatavalue = JsonValue::CreateStringValue(L"<private_data>");
                obj.SetNamedValue(xpathArray[p], privateDatavalue);
            }
        }

        return;
    }

    if (val.ValueType() == JsonValueType::Object)
    {
        IJsonValue newVal;
        try
        {
            newVal = val.GetObjectW().GetNamedValue(xpathArray[p]);
        }
        catch (...)
        {
            return;
        }

        HideByXPath(newVal, xpathArray, p + 1);
    }
}

void HideForFile(const path& dir, const wstring& relativePath)
{
    path jsonPath = dir;
    jsonPath.append(relativePath);
    auto jObject = json::from_file(jsonPath.wstring());
    if (!jObject.has_value())
    {
        wprintf(L"Failed to parse file %s\n", jsonPath.c_str());
        return;
    }

    JsonValue jValue = json::value(jObject.value());
    for (auto xpath : escapeInfo[relativePath])
    {
        vector<wstring> xpathArray = GetXpathArray(xpath);
        HideByXPath(jValue, xpathArray, 0);
    }

    json::to_file(jsonPath.wstring(), jObject.value());
}

bool DeleteFolder(wstring path)
{
    error_code err;
    remove_all(path, err);
    if (err.value() != 0)
    {
        wprintf_s(L"Failed to delete %s. Error code: %d\n", path.c_str(), err.value());
        return false;
    }

    return true;
}

void HideUserPrivateInfo(const filesystem::path& dir)
{
    // Replace data in json files
    for (auto& it : escapeInfo)
    {
        HideForFile(dir, it.first);
    }

    // Delete files
    for (auto it : filesToDelete)
    {
        auto path = dir;
        path = path.append(it);
        DeleteFolder(path);
    }
}

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

void ReportVCMLogs(const filesystem::path& tmpDir, const filesystem::path& reportDir)
{
    error_code ec;
    copy(tmpDir / "PowerToysVideoConference_x86.log", reportDir, ec);
    copy(tmpDir / "PowerToysVideoConference_x64.log", reportDir, ec);
}

void ReportInstallerLogs(const filesystem::path& tmpDir, const filesystem::path& reportDir)
{
    const char* logFilePrefix = "powertoys-bootstrapper-msi-";

    for (auto& entry : directory_iterator{ tmpDir })
    {
        std::error_code ec;
        if (entry.is_directory(ec) || !entry.path().has_filename())
        {
            continue;
        }

        const auto fileName = entry.path().filename().string();
        if (!fileName.starts_with(logFilePrefix))
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
        printf("Failed to delete temp folder\n");
    }

    try
    {
        copy(settingsRootPath, reportDir, copy_options::recursive);

        // Remove updates folder contents
        DeleteFolder(reportDir / "Updates");
    }
	
    catch (...)
    {
        printf("Failed to copy PowerToys folder\n");
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
    HideUserPrivateInfo(reportDir);

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

    ReportVCMLogs(tempDir, reportDir);
    
    ReportInstallerLogs(tempDir, reportDir);

    ReportInstalledContextMenuPackages(reportDir);

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
