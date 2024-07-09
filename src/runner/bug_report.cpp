#include "pch.h"
#include "bug_report.h"
#include "Generated files/resource.h"
#include <common/notifications/NotificationUtil.h>
#include <common/utils/exec.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/registry.h>
#include <common/utils/elevation.h>
#include <common/version/version.h>
#include <runner/general_settings.h>

#include <winrt/Windows.System.UserProfile.h>
#include <winrt/Windows.Globalization.h>


#include <regex>
#include <thread>
#include <atomic>
#include <future>

using namespace std;
using namespace registry::install_scope;
namespace fs = std::filesystem;

std::atomic_bool isBugReportThreadRunning = false;
std::mutex LockObject;
std::promise<void> cancelPromise;
static std::atomic<bool> canceled{ false };
static HWND hwndMessageBox = nullptr;

std::string LaunchBugReport()
{
    std::string bugReportFileName;
    std::wstring bug_report_path = get_module_folderpath();
    bug_report_path += L"\\Tools\\PowerToys.BugReportTool.exe";

    bool expected_isBugReportThreadRunning = false;
    if (isBugReportThreadRunning.compare_exchange_strong(expected_isBugReportThreadRunning, true))
    {
        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC | SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NO_CONSOLE };
        sei.lpFile = bug_report_path.c_str();
        sei.nShow = SW_HIDE;
        if (ShellExecuteExW(&sei))
        {
            while (WaitForSingleObject(sei.hProcess, 100) == WAIT_TIMEOUT)
            {
                if (canceled.load())
                {
                    TerminateProcess(sei.hProcess, 0);
                    CloseHandle(sei.hProcess);
                    isBugReportThreadRunning.store(false);
                    return "";
                }
            }
            CloseHandle(sei.hProcess);

            // Find the newest bug report file on the desktop
            bugReportFileName = FindNewestBugReportFile();
        }

        isBugReportThreadRunning.store(false);
    }
    return bugReportFileName;
}

void InitializeReportBugLinkAsync()
{
    std::string gitHubURL;
    std::string bugReportResult;

    notifications::show_toast(GET_RESOURCE_STRING(IDS_BUGREPORT_TEXT), GET_RESOURCE_STRING(IDS_BUGREPORT_TITLE));

    // Launch the bug report task
    auto bugReportTask = std::async(std::launch::async, [&bugReportResult] {
        bugReportResult = LaunchBugReport();
    });

    bugReportTask.wait();
    
    if (!bugReportResult.empty())
    {
        std::wstring wVersion = get_product_version();
        std::string version;
        std::transform(wVersion.begin() + 1, wVersion.end(), std::back_inserter(version), [](wchar_t c) {
            return static_cast<char>(c);
        });

        std::string additionalInfo = "OS Build Version: " + GetOSVersion() + "%0a" + ".NET Version: " + GetDotNetVersion() + "%0a%0a";
        GeneralSettings generalSettings = get_general_settings();
        std::string isElevatedRun = generalSettings.isElevated ? "Running as admin: Yes" : "Running as admin: No";

        std::string windowsSettings = ReportWindowsSettings();

        const InstallScope current_install_scope = get_current_install_scope();

        std::string installScope = current_install_scope == InstallScope::PerUser ? "Installation : User" : "Installation : System";

        additionalInfo += windowsSettings + "%0a" + installScope + "%0a" + isElevatedRun;

        gitHubURL = "https://github.com/gokcekantarci/PowerToys/issues/new?assignees=&labels=Issue-Bug%2CNeeds-Triage&template=bug_report.yml" +
                    std::string("&version=") + version +
                    std::string("&additionalInfo=") + additionalInfo;

        std::wstring wideBugReportResult = L"Bug report generated on your desktop. Please attach the file to the GitHub issue.\n\n" + stringToWideString(bugReportResult);
        MessageBox(nullptr, wideBugReportResult.c_str(), L"Bug Report", MB_OKCANCEL | MB_ICONINFORMATION);
    }
    else
    {
        MessageBox(nullptr, L"Failed to start bug report tool.", L"Bug Report", MB_OKCANCEL | MB_ICONINFORMATION);
        gitHubURL = "https://aka.ms/powerToysReportBug";
    }

    // Open the URL
    std::wstring wGitHubURL(gitHubURL.begin(), gitHubURL.end());
    ShellExecuteW(nullptr, L"open", wGitHubURL.c_str(), nullptr, nullptr, SW_SHOWNORMAL);
}

std::string FindNewestBugReportFile()
{
    char* desktopPathC;
    size_t len;
    if (_dupenv_s(&desktopPathC, &len, "USERPROFILE") != 0 || desktopPathC == nullptr)
    {
        return "";
    }

    std::string desktopPath(desktopPathC);
    free(desktopPathC);

    desktopPath += "\\Desktop";
    fs::path desktopDir(desktopPath);

    if (!fs::exists(desktopDir) || !fs::is_directory(desktopDir))
    {
        return "";
    }

    std::string newestFile;
    std::time_t newestTime = 0;

    for (const auto& entry : fs::directory_iterator(desktopDir))
    {
        if (entry.is_regular_file() && entry.path().filename().string().find("PowerToysReport_") == 0)
        {
            std::time_t fileTime = fs::last_write_time(entry).time_since_epoch().count();
            if (fileTime > newestTime)
            {
                newestTime = fileTime;
                newestFile = entry.path().string();
            }
        }
    }

    return newestFile;
}

std::wstring ReadRegistryString(HKEY hKeyRoot, const std::wstring& subKey, const std::wstring& valueName)
{
    HKEY hKey;
    if (RegOpenKeyEx(hKeyRoot, subKey.c_str(), 0, KEY_READ, &hKey) != ERROR_SUCCESS)
    {
        return L"";
    }

    wchar_t value[256];
    DWORD bufferSize = sizeof(value);
    DWORD type;
    if (RegQueryValueEx(hKey, valueName.c_str(), 0, &type, (LPBYTE)value, &bufferSize) != ERROR_SUCCESS || type != REG_SZ)
    {
        RegCloseKey(hKey);
        return L"";
    }

    RegCloseKey(hKey);
    return std::wstring(value);
}

// Helper function to convert std::wstring to std::string
std::string WideStringToString(const std::wstring& wstr)
{
    if (wstr.empty())
        return std::string();
    int size_needed = WideCharToMultiByte(CP_UTF8, 0, &wstr[0], static_cast<int>(wstr.size()), NULL, 0, NULL, NULL);
    std::string str(size_needed, 0);
    WideCharToMultiByte(CP_UTF8, 0, &wstr[0], static_cast<int>(wstr.size()), &str[0], size_needed, NULL, NULL);
    return str;
}

std::wstring stringToWideString(const std::string& str)
{
    if (str.empty())
        return std::wstring();
    int size_needed = MultiByteToWideChar(CP_UTF8, 0, &str[0], static_cast<int>(str.size()), NULL, 0);
    std::wstring wstr(size_needed, 0);
    MultiByteToWideChar(CP_UTF8, 0, &str[0], static_cast<int>(str.size()), &wstr[0], size_needed);
    return wstr;
}

// Function to get the .NET version
std::string GetDotNetVersion()
{
    try
    {
        std::string dotnetInfo = (exec_and_read_output(L"dotnet --list-runtimes")).value();
        if (dotnetInfo.empty())
        {
            return "Unknown .NET Version";
        }
        
        std::regex versionRegex(R"((\d+\.\d+\.\d+))");
        std::sregex_iterator begin(dotnetInfo.begin(), dotnetInfo.end(), versionRegex), end;

        std::string latestVersion;
        for (std::sregex_iterator i = begin; i != end; ++i)
        {
            std::string version = (*i).str();
            if (version > latestVersion)
            {
                latestVersion = version;
            }
        }

        return latestVersion.empty() ? "Unknown .NET Version" : ".NET " + latestVersion;
    }
    catch (const std::exception& e)
    {
        return "Failed to get .NET Version: " + std::string(e.what());
    }
}


std::string GetOSVersion()
{
    OSVERSIONINFOEXW osInfo = { 0 };
    try
    {
        NTSTATUS(WINAPI * RtlGetVersion)
        (LPOSVERSIONINFOEXW) = nullptr;
        *reinterpret_cast<FARPROC*>(&RtlGetVersion) = GetProcAddress(GetModuleHandleA("ntdll"), "RtlGetVersion");
        if (RtlGetVersion)
        {
            osInfo.dwOSVersionInfoSize = sizeof(osInfo);
            RtlGetVersion(&osInfo);
        }
    }
    catch (...)
    {
        return "Unknown Windows Version";
    }

    try
    {
        std::ostringstream osVersion;
        osVersion << osInfo.dwMajorVersion << "." << osInfo.dwMinorVersion << "." << osInfo.dwBuildNumber;
        return osVersion.str();
    }
    catch (...)
    {
        return "Unknown Windows Version";
    }
}


std::string GetModuleFolderPath()
{
    char buffer[MAX_PATH];
    GetModuleFileNameA(NULL, buffer, MAX_PATH);
    std::string::size_type pos = std::string(buffer).find_last_of("\\/");
    return std::string(buffer).substr(0, pos);
}

std::string ReportWindowsSettings()
{
    std::wstring userLanguage;
    std::wstring userLocale;
    std::string result;

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
        return "Failed to get windows settings %0a";
    }

    result = "Preferred user language: " + WideStringToString(userLanguage) + "%0a";
    result += "User locale: " + WideStringToString(userLocale) + "%0a";

    return result;
}