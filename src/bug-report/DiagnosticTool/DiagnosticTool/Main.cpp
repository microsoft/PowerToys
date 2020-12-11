#include <filesystem>
#include <fstream>
#include <string>
#include <vector>
#include <Shlobj.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>

#include "zip.h"

using namespace std;
using namespace std::filesystem;
using namespace winrt::Windows::Data::Json;

map<wstring, vector<wstring>> escapeInfo = {
    { L"FancyZones\\app-zone-history.json", { L"app-zone-history/app-path" } }
};

vector<wstring> filesToDelete = {
};

std::wstring get_root_save_folder_location()
{
    PWSTR local_app_path;
    winrt::check_hresult(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, NULL, &local_app_path));
    std::wstring result{ local_app_path };
    CoTaskMemFree(local_app_path);

    result += L"\\Microsoft\\PowerToys";
    path save_path(result);
    if (!exists(save_path))
    {
        create_directories(save_path);
    }
    return result;
}

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
        auto privateDatavalue = JsonValue::CreateStringValue(L"<private_data>");
        val.GetObjectW().SetNamedValue(xpathArray[p], privateDatavalue);
        return;
    }

    auto newVal = val.GetObjectW().GetNamedValue(xpathArray[p]);
    hideByXPath(newVal, xpathArray, p + 1);
}

void hideForFile(path dir, wstring relativePath)
{
    wstring jsonString, tmp;
    dir = dir.append(relativePath);
    wifstream file(dir);
    while (std::getline(file, tmp))
    {
        jsonString += tmp;
    }

    JsonObject::Parse(jsonString);
    JsonValue jValue = NULL;
    if (!JsonValue::TryParse(jsonString, jValue))
    {
        wprintf(L"Can not parse file %s", dir.c_str());
        return;
    }

    for (auto xpath : escapeInfo[relativePath])
    {
        vector<wstring> xpathArray = getXpathArray(xpath);
        hideByXPath(jValue, xpathArray, 0);
    }

    jsonString = jValue.Stringify();
    wofstream out(dir);
    out << jsonString;
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
        remove_all(it);
    }
}

// create zip folder
// Zip utility from https://www.codeproject.com/Articles/7530/Zip-Utils-Clean-Elegant-Simple-Cplusplus-Win
// Alternative. Use Shell COM object. https://stackoverflow.com/questions/118547/creating-a-zip-file-on-windows-xp-2003-in-c-c
// Alternative. https://github.com/sebastiandev/zipper
void zipFolder(filesystem::path folderPath, filesystem::path zipPath) 
{
    HZIP hz = CreateZip(zipPath.c_str(), 0);
    using recursive_directory_iterator = recursive_directory_iterator;
    int rootSize = folderPath.wstring().size();
    for (const auto& dirEntry : recursive_directory_iterator(folderPath))
    {
        if (dirEntry.is_regular_file())
        {
            auto path = dirEntry.path().wstring();
            auto relativePath = path.substr(rootSize, path.size());
            ZipAdd(hz, relativePath.c_str(), path.c_str());
        }
    }

    CloseZip(hz);
}

int main()
{
    auto powerToys = get_root_save_folder_location();
    
    // Copy to a temp folder
    auto tmpDir = temp_directory_path();
    tmpDir = tmpDir.append("PowerToys\\");
    powerToys = powerToys + L"\\";
    remove_all(tmpDir);
    copy(powerToys, tmpDir, copy_options::recursive);
    
    // Hide sensative information
    hideUserPrivateInfo(tmpDir);

    // Zip folder
    auto zipPath = path::path(L"C:\\JaneaSystems\\1\\");
    zipPath = zipPath.append("PowerToys.zip");
    zipFolder(tmpDir, zipPath);
}