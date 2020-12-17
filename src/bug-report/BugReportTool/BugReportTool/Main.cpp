#include <filesystem>
#include <fstream>
#include <string>
#include <vector>
#include <Shlobj.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>

#include "ZipTools/ZipFolder.h"
#include "../../../common/SettingsAPI/settings_helpers.h"
#include "../../../common/utils/json.h"

using namespace std;
using namespace std::filesystem;
using namespace winrt::Windows::Data::Json;

map<wstring, vector<wstring>> escapeInfo = {
    { L"FancyZones\\app-zone-history.json", { L"app-zone-history/app-path" } },
    { L"PowerRename\\replace-mru.json", { L"MRUList" } },
    { L"PowerRename\\search-mru.json", { L"MRUList" } }
};

vector<wstring> filesToDelete = {
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
        wprintf(L"Can not parse file %s\n", jsonPath.c_str());
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

bool del(wstring path)
{
    error_code err;
    remove_all(path, err);
    if (err.value() != 0)
    {
        wprintf_s(L"Can not delete %s. Error code: %d", path.c_str(), err.value());
        return false;
    }

    return true;
}

void hideUserPrivateInfo(filesystem::path dir) 
{
    // Replace data in json files
    for (auto& it : escapeInfo)
    {
        hideForFile(dir, it.first);
    }

    // delete files
    for (auto it : filesToDelete)
    {
        del(it);
    }
}

string getCurrentDate()
{
    time_t now = time(0);
    tm ltm; 
    if (localtime_s(&ltm, &now) != 0)
    {
        return "";
    }

    int year = 1900 + ltm.tm_year;
    int month = 1 + ltm.tm_mon;
    int day = ltm.tm_mday;
    return to_string(year) + "-" + to_string(month) + "-" + to_string(day);
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
            printf("Can not retrieve a desktop path. Error code: %d", GetLastError());
        }
    }

    auto powerToys = PTSettingsHelper::get_root_save_folder_location();
    
    // Copy to a temp folder
    auto tmpDir = temp_directory_path();
    tmpDir = tmpDir.append("PowerToys\\");
    powerToys = powerToys + L"\\";
    if (!del(tmpDir))
    {
        return 1;
    }

    try
    {
        copy(powerToys, tmpDir, copy_options::recursive);
    }
    catch (std::bad_alloc err)
    {
        printf("Copy PowerToys directory failed. %s", err.what());
        return 1;
    }
    catch (...)
    {
        printf("Copy PowerToys directory failed");
        return 1;
    }
    
    // Hide sensative information
    hideUserPrivateInfo(tmpDir);

    // Zip folder
    auto zipPath = path::path(saveZipPath);
    zipPath = zipPath.append("PowerToysReport_" + getCurrentDate() + ".zip");
    try
    {
        zipFolder(zipPath, tmpDir);
    }
    catch (...)
    {
        printf("Zip folder failed");
        return 1;
    }
    
    del(tmpDir);
    return 0;
}