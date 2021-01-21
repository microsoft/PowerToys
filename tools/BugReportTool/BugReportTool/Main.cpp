#include <filesystem>
#include <fstream>
#include <string>
#include <vector>
#include <Shlobj.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>

#include "ZipTools/ZipFolder.h"
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/json.h>
#include <common/utils/timeutil.h>
#include <common/utils/exec.h>

#include "ReportMonitorInfo.h"
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

vector<pair<HKEY, wstring>> registryKeys = {
    { HKEY_CLASSES_ROOT, L"Software\\Classes\\CLSID\\{DD5CACDA-7C2E-4997-A62A-04A597B58F76}" },
    { HKEY_CLASSES_ROOT, L"powertoys" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{ddee2b8a-6807-48a6-bb20-2338174ff779}" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{36B27788-A8BB-4698-A756-DF9F11F64F84}" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{45769bcc-e8fd-42d0-947e-02beef77a1f5}" },
    { HKEY_CLASSES_ROOT, L"AppID\\{CF142243-F059-45AF-8842-DBBE9783DB14}" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{51B4D7E5-7568-4234-B4BB-47FB3C016A69}\\InprocServer32" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{0440049F-D1DC-4E46-B27B-98393D79486B}" },
    { HKEY_CLASSES_ROOT, L"AllFileSystemObjects\\ShellEx\\ContextMenuHandlers\\PowerRenameExt" },
    { HKEY_CURRENT_USER, L"SOFTWARE\\Classes\\AppUserModelId\\PowerToysRun" },
    { HKEY_CLASSES_ROOT, L".svg\\shellex\\{8895b1c6-b41f-4c1c-a562-0d564250836f}" },
    { HKEY_CLASSES_ROOT, L".svg\\shellex\\{E357FCCD-A995-4576-B01F-234630154E96}" },
    { HKEY_CLASSES_ROOT, L".md\\shellex\\{8895b1c6-b41f-4c1c-a562-0d564250836f}" }
};

vector<tuple<HKEY, wstring, wstring>> registryValues = {
    { HKEY_LOCAL_MACHINE, L"Software\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers", L"{ddee2b8a-6807-48a6-bb20-2338174ff779}" },
    { HKEY_LOCAL_MACHINE, L"Software\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers", L"{45769bcc-e8fd-42d0-947e-02beef77a1f5}" },
    { HKEY_LOCAL_MACHINE, L"Software\\Microsoft\\Internet Explorer\\Main\\FeatureControl\\FEATURE_BROWSER_EMULATION", L"prevhost.exe" },
    { HKEY_LOCAL_MACHINE, L"Software\\Microsoft\\Internet Explorer\\Main\\FeatureControl\\FEATURE_BROWSER_EMULATION", L"dllhost.exe" }
};

// Is there a Windows API for this?
std::unordered_map<HKEY, wstring> hKeyToString = {
    { HKEY_CLASSES_ROOT, L"HKEY_CLASSES_ROOT" },
    { HKEY_CURRENT_USER, L"HKEY_CURRENT_USER" },
    { HKEY_LOCAL_MACHINE, L"HKEY_LOCAL_MACHINE" },
    { HKEY_PERFORMANCE_DATA, L"HKEY_PERFORMANCE_DATA" },
    { HKEY_PERFORMANCE_NLSTEXT, L"HKEY_PERFORMANCE_NLSTEXT"},
    { HKEY_PERFORMANCE_TEXT, L"HKEY_PERFORMANCE_TEXT"},
    { HKEY_USERS, L"HKEY_USERS"},
};

vector<wstring> getXpathArray(wstring xpath)
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

void hideByXPath(IJsonValue& val, vector<wstring>& xpathArray, int p)
{
    if (val.ValueType() == JsonValueType::Array)
    {
        for (auto it : val.GetArray())
        {
            hideByXPath(it, xpathArray, p);
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

        hideByXPath(newVal, xpathArray, p + 1);
    }
}

void hideForFile(const path& dir, const wstring& relativePath)
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
        vector<wstring> xpathArray = getXpathArray(xpath);
        hideByXPath(jValue, xpathArray, 0);
    }

    json::to_file(jsonPath.wstring(), jObject.value());
}

bool deleteFolder(wstring path)
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

void hideUserPrivateInfo(const filesystem::path& dir)
{
    // Replace data in json files
    for (auto& it : escapeInfo)
    {
        hideForFile(dir, it.first);
    }

    // delete files
    for (auto it : filesToDelete)
    {
        auto path = dir;
        path = path.append(it);
        deleteFolder(path);
    }
}

void reportMonitorInfo(const filesystem::path& tmpDir)
{
    auto monitorReportPath = tmpDir;
    monitorReportPath.append("monitor-report-info.txt");

    try
    {
        wofstream monitorReport(monitorReportPath);
        monitorReport << "GetSystemMetrics = " << GetSystemMetrics(SM_CMONITORS) << '\n';
        report(monitorReport);
    }
    catch (std::exception& ex)
    {
        printf("Failed to report monitor info. %s\n", ex.what());
    }
    catch (...)
    {
        printf("Failed to report monitor info\n");
    }
}

void reportWindowsVersion(const filesystem::path& tmpDir)
{
    auto versionReportPath = tmpDir;
    versionReportPath = versionReportPath.append("windows-version.txt");
    OSVERSIONINFOEXW osInfo;

    try
    {
        NTSTATUS(WINAPI * RtlGetVersion)
        (LPOSVERSIONINFOEXW) = nullptr;
        *(FARPROC*)&RtlGetVersion = GetProcAddress(GetModuleHandleA("ntdll"), "RtlGetVersion");
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
    catch(...)
    {
        printf("Failed to write to %s\n", versionReportPath.string().c_str());
    }
}

void reportDotNetInstallationInfo(const filesystem::path& tmpDir)
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

void queryKey(HKEY key, wofstream& stream, int indent = 1)
{
    TCHAR achKey[255];
    DWORD cbName;
    TCHAR achClass[MAX_PATH] = TEXT("");
    DWORD cchClassName = MAX_PATH;
    DWORD cSubKeys = 0;
    DWORD cbMaxSubKey; 
    DWORD cchMaxClass; 
    DWORD cValues;
    DWORD cchMaxValue; 
    DWORD cbMaxValueData;

    DWORD i, retCode;

    TCHAR achValue[255];
    DWORD cchValue = 255;
    LPBYTE value;

    // Get the class name and the value count. 
    retCode = RegQueryInfoKeyW(key,achClass, &cchClassName, NULL, &cSubKeys, &cbMaxSubKey, &cchMaxClass, &cValues, &cchMaxValue, &cbMaxValueData, NULL, NULL);

    // Values
    if (cValues)
    {
        for (i = 0, retCode = ERROR_SUCCESS; i < cValues; i++)
        {
            cchValue = 255;
            achValue[0] = '\0';
            value = new BYTE[16383];
            retCode = RegEnumValueW(key, i, achValue, &cchValue, NULL, NULL, value, &cchValue);

            if (retCode == ERROR_SUCCESS)
            {
                stream << wstring(indent, '\t');
                if (achValue[0] == '\0')
                {
                    stream << "Default";
                }
                else
                {
                    stream << achValue;
                }

                stream << " > " << (LPCTSTR)value << "\n";
            }
            else
            {
                stream << "Error " << retCode << "\n";
            }
        }
    }

    // Keys
    if (cSubKeys)
    {
        std::vector<wstring> vecKeys;
        vecKeys.reserve(cSubKeys);

        for (i = 0; i < cSubKeys; ++i)
        {
            cbName = 255;
            retCode = RegEnumKeyExW(key, i, achKey, &cbName, NULL, NULL, NULL, NULL);
            if (retCode == ERROR_SUCCESS)
            {
                vecKeys.push_back(achKey);
            }
        }

        // Parsing subkeys recursively
        for (auto& child : vecKeys)
        {
            HKEY hTestKey;
            if (RegOpenKeyExW(key, child.c_str(), 0, KEY_READ, &hTestKey) == ERROR_SUCCESS)
            {
                stream << wstring(indent, '\t') << child << "\n";
                queryKey(hTestKey, stream, indent + 1);
                RegCloseKey(hTestKey);
            }
            else
            {
                stream << "Error " << retCode << "\n";
            }
        }
    }
}

void reportRegistry(const filesystem::path& tmpDir)
{
    auto registryReportPath = tmpDir;
    registryReportPath.append("registry.txt");

    wofstream registryReport(registryReportPath);
    try
    {
        for (auto [rootKey, subKey] : registryKeys)
        {
            registryReport << hKeyToString[rootKey] << "\\" << subKey << "\n";

            HKEY outKey;
            LONG result = RegOpenKeyExW(rootKey, subKey.c_str(), 0, KEY_READ, &outKey);
            if (result == ERROR_SUCCESS)
            {
                queryKey(outKey, registryReport);
                RegCloseKey(rootKey);
            }
            else
            {
                registryReport << "ERROR " << result << "\n";
            }
            registryReport << "\n";
        }

        for (auto [rootKey, subKey, value] : registryValues)
        {
            registryReport << hKeyToString[rootKey] << "\\" << subKey << "\n";

            // Reading size
            DWORD dataSize = 0;
            DWORD flags = RRF_RT_ANY;
            DWORD type;
            LONG result = RegGetValueW(rootKey, subKey.c_str(), value.c_str(), flags, &type, NULL, &dataSize);
            if (result == ERROR_SUCCESS)
            {
                // Reading value
                if (type == REG_SZ) // string
                {
                    std::wstring data(dataSize / sizeof(wchar_t) + 1, L' ');
                    result = RegGetValueW(rootKey, subKey.c_str(), value.c_str(), flags, &type, &data[0], &dataSize);
                    if (result == ERROR_SUCCESS)
                    {
                        registryReport << "\t" << value << " > " << data << "\n";
                    }
                    else
                    {
                        registryReport << "ERROR " << result << "\n";
                    }
                }
                else
                {
                    DWORD data = 0;
                    DWORD dataSize = sizeof(data);
                    LONG retCode = RegGetValueW(rootKey, subKey.c_str(), value.c_str(), flags, &type, &data, &dataSize);
                    if (result == ERROR_SUCCESS)
                    {
                        registryReport << "\t" << value << " > " << data << "\n";
                    }
                    else
                    {
                        registryReport << "ERROR " << result << "\n";
                    }
                }
                RegCloseKey(rootKey);
            }
            else
            {
                registryReport << "ERROR " << result << "\n";
            }
            registryReport << "\n";
        }
    }
    catch (...)
    {
        printf("Failed to get registry keys\n");
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
    settingsRootPath = settingsRootPath + L"\\";

    // Copy to a temp folder
    auto tmpDir = temp_directory_path();
    tmpDir = tmpDir.append("PowerToys\\");
    if (!deleteFolder(tmpDir))
    {
        printf("Failed to delete temp folder\n");
        return 1;
    }

    try
    {
        copy(settingsRootPath, tmpDir, copy_options::recursive);
        // Remove updates folder contents
        deleteFolder(tmpDir / "Updates");
    }
    catch (...)
    {
        printf("Failed to copy PowerToys folder\n");
        return 1;
    }

    // Hide sensitive information
    hideUserPrivateInfo(tmpDir);

    // Write monitors info to the temporary folder
    reportMonitorInfo(tmpDir);

    // Write windows version info to the temporary folder
    reportWindowsVersion(tmpDir);

    // Write dotnet installation info to the temporary folder
    reportDotNetInstallationInfo(tmpDir);

    // Write registry to the temporary folder
    reportRegistry(tmpDir);

    // Zip folder
    auto zipPath = path::path(saveZipPath);
    std::string reportFilename{"PowerToysReport_"};
    reportFilename += timeutil::format_as_local("%F-%H-%M-%S", timeutil::now());
    reportFilename += ".zip";
    zipPath /= reportFilename;

    try
    {
        zipFolder(zipPath, tmpDir);
    }
    catch (...)
    {
        printf("Failed to zip folder\n");
        return 1;
    }

    deleteFolder(tmpDir);
    return 0;
}