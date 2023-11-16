#include "RegistryUtils.h"
#include <common/utils/winapi_error.h>
#include <map>

using namespace std;

extern std::vector<std::wstring> processes;

namespace
{
    vector<pair<HKEY, wstring>> registryKeys = {
    { HKEY_CLASSES_ROOT, L"CLSID\\{DD5CACDA-7C2E-4997-A62A-04A597B58F76}" },
    { HKEY_CLASSES_ROOT, L"powertoys" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{ddee2b8a-6807-48a6-bb20-2338174ff779}" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{36B27788-A8BB-4698-A756-DF9F11F64F84}" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{45769bcc-e8fd-42d0-947e-02beef77a1f5}" },
    { HKEY_CLASSES_ROOT, L"AppID\\{CF142243-F059-45AF-8842-DBBE9783DB14}" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{07665729-6243-4746-95b7-79579308d1b2}" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{ec52dea8-7c9f-4130-a77b-1737d0418507}" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{8AA07897-C30B-4543-865B-00A0E5A1B32D}" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{BCC13D15-9720-4CC4-8371-EA74A274741E}" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{BFEE99B4-B74D-4348-BCA5-E757029647FF}" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{8BC8AFC2-4E7C-4695-818E-8C1FFDCEA2AF}" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{51B4D7E5-7568-4234-B4BB-47FB3C016A69}\\InprocServer32" },
    { HKEY_CLASSES_ROOT, L"CLSID\\{0440049F-D1DC-4E46-B27B-98393D79486B}" },
    { HKEY_CLASSES_ROOT, L"AllFileSystemObjects\\ShellEx\\ContextMenuHandlers\\PowerRenameExt" },
    { HKEY_CLASSES_ROOT, L".svg\\shellex\\{8895b1c6-b41f-4c1c-a562-0d564250836f}" },
    { HKEY_CLASSES_ROOT, L".svg\\shellex\\{E357FCCD-A995-4576-B01F-234630154E96}" },
    { HKEY_CLASSES_ROOT, L".md\\shellex\\{8895b1c6-b41f-4c1c-a562-0d564250836f}" },
    { HKEY_CLASSES_ROOT, L".pdf\\shellex\\{8895b1c6-b41f-4c1c-a562-0d564250836f}" },
    { HKEY_CLASSES_ROOT, L".pdf\\shellex\\{E357FCCD-A995-4576-B01F-234630154E96}" },
    { HKEY_CLASSES_ROOT, L".qoi\\shellex\\{8895b1c6-b41f-4c1c-a562-0d564250836f}" },
    { HKEY_CLASSES_ROOT, L".qoi\\shellex\\{E357FCCD-A995-4576-B01F-234630154E96}" },
    { HKEY_CLASSES_ROOT, L".gcode\\shellex\\{8895b1c6-b41f-4c1c-a562-0d564250836f}" },
    { HKEY_CLASSES_ROOT, L".gcode\\shellex\\{E357FCCD-A995-4576-B01F-234630154E96}" },
    { HKEY_CLASSES_ROOT, L".stl\\shellex\\{E357FCCD-A995-4576-B01F-234630154E96}" }
    };

    vector<tuple<HKEY, wstring, wstring>> registryValues = {
        { HKEY_LOCAL_MACHINE, L"Software\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers", L"{ddee2b8a-6807-48a6-bb20-2338174ff779}" },
        { HKEY_LOCAL_MACHINE, L"Software\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers", L"{45769bcc-e8fd-42d0-947e-02beef77a1f5}" },
        { HKEY_LOCAL_MACHINE, L"Software\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers", L"{07665729-6243-4746-95b7-79579308d1b2}" },
        { HKEY_LOCAL_MACHINE, L"Software\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers", L"{ec52dea8-7c9f-4130-a77b-1737d0418507}" },
        { HKEY_LOCAL_MACHINE, L"Software\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers", L"{8AA07897-C30B-4543-865B-00A0E5A1B32D}" },
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

    vector<pair<wstring, wstring>> QueryValues(HKEY key)
    {
        DWORD cValues;
        RegQueryInfoKeyW(key, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, &cValues, nullptr, nullptr, nullptr, nullptr);
        TCHAR achValue[255];
        DWORD cchValue = 255;
        LPBYTE value;
        vector<pair<wstring, wstring>> results;
        // Values
        if (cValues)
        {
            for (DWORD i = 0, retCode = ERROR_SUCCESS; i < cValues; i++)
            {
                cchValue = 255;
                achValue[0] = '\0';
                value = new BYTE[16383];
                retCode = RegEnumValueW(key, i, achValue, &cchValue, NULL, NULL, value, &cchValue);

                if (retCode == ERROR_SUCCESS)
                {
                    results.push_back({ achValue, (LPCTSTR)value });
                }
            }
        }

        return results;
    }

    void QueryKey(HKEY key, wostream& stream, int indent = 1)
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
        retCode = RegQueryInfoKeyW(key, achClass, &cchClassName, NULL, &cSubKeys, &cbMaxSubKey, &cchMaxClass, &cValues, &cchMaxValue, &cbMaxValueData, NULL, NULL);

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

                    stream << " > " << reinterpret_cast<LPCTSTR>(value) << "\n";
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
                    QueryKey(hTestKey, stream, indent + 1);
                    RegCloseKey(hTestKey);
                }
                else
                {
                    stream << "Error " << retCode << "\n";
                }
            }
        }
    }
}

void ReportCompatibilityTab(HKEY key, wofstream& report)
{
    map<wstring, wstring> flags;
    for (auto app : processes)
    {
        flags[app] = L"";
    }

    try
    {
        HKEY outKey;
        LONG result = RegOpenKeyExW(key, L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers", 0, KEY_READ, &outKey);
        if (result == ERROR_SUCCESS)
        {
            auto values = QueryValues(outKey);
            for (auto value : values)
            {
                for (auto app : processes)
                {
                    if (value.first.find(app) != wstring::npos)
                    {
                        flags[app] += value.second;
                    }
                }
            }
        }
        else
        {
            report << "Failed to get the report. " << get_last_error_or_default(GetLastError());
            return;
        }
    }
    catch (...)
    {
        report << "Failed to get the report";
        return;
    }

    for (auto flag : flags)
    {
        report << flag.first << ": " << flag.second << endl;
    }
}

void ReportCompatibilityTab(const std::filesystem::path& tmpDir)
{
    auto reportPath = tmpDir;
    reportPath.append(L"compatibility-tab-info.txt");
    wofstream report(reportPath);
    report << "Current user report" << endl;
    ReportCompatibilityTab(HKEY_CURRENT_USER, report);
    report << endl << endl;
    report << "Local machine report" << endl;
    ReportCompatibilityTab(HKEY_LOCAL_MACHINE, report);
}

void ReportRegistry(const filesystem::path& tmpDir)
{
    auto registryReportPath = tmpDir;
    registryReportPath.append("registry-report-info.txt");

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
                QueryKey(outKey, registryReport);
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
                    dataSize = sizeof(data);
                    result = RegGetValueW(rootKey, subKey.c_str(), value.c_str(), flags, &type, &data, &dataSize);
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
