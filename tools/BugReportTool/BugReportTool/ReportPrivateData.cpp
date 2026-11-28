// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Hides or takes out private data in the report's copy of the settings folder. Kept apart from Main.cpp so the
// unit tests can run it on a folder of their own.

#include "ReportPrivateData.h"

#include <cwchar>
#include <fstream>
#include <map>
#include <string>
#include <vector>
#include <Windows.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>

#include <common/utils/json.h>

using namespace std;
using namespace std::filesystem;
using namespace winrt::Windows::Data::Json;

map<wstring, vector<wstring>> escapeInfo = {
    { L"FancyZones\\app-zone-history.json", { L"app-zone-history/app-path" } },
    { L"FancyZones\\settings.json", { L"properties/fancyzones_excluded_apps" } },
    { L"MouseWithoutBorders\\settings.json", { L"properties/SecurityKey" } }, // avoid leaking connection key
    { L"Workspaces/workspaces.json", { L"workspaces/applications/command-line-arguments" } },
    { L"AdvancedPaste/settings.json", {
        L"properties/custom-actions/value/name",
        L"properties/custom-actions/value/prompt"
        } },
};

// Before an update, the updater copies every json file of the settings folder and of each module's folder to
// ConfigBackup, in the same layout (common\updating\configBackup.h), and removes the copy after the update. If the
// update stops before that, the copy stays, and the report copies it too. So everything below is applied to the
// settings folder and to ConfigBackup alike.
const wstring configBackupFolder = L"ConfigBackup";

// Keyboard Manager files are handled by HideKeyboardManagerPrivateInfo, which takes out a file whose private data
// could not be hidden; if even that fails, the whole folder is taken out, and if that fails too, no report is
// created. Every json file in the folder is handled as a profile, except the ones listed here, which are kept as
// they are; that includes a json file in a subfolder (a copy of the folder made by hand). Any other file in the
// folder itself, and any file in a subfolder that is not a log (.log), is taken out: it cannot be searched for
// private data.
const wstring keyboardManagerFolder = L"Keyboard Manager";
const vector<wstring> keyboardManagerFilesKeptAsIs = {
    L"settings.json",
    L"editorSettings.json",
};

// Applied to every Keyboard Manager profile, not only default.json: avoid leaking personal information from
// text, URI or application mappings, and from key sequences (a sequence of letter keys can spell out text).
const vector<wstring> keyboardManagerProfileEscapeInfo = {
    L"remapKeysToText",
    L"remapShortcutsToText",
    L"remapKeys/inProcessSequence/newRemapKeys",
    L"remapShortcuts/global/runProgramFilePath",
    L"remapShortcuts/global/runProgramArgs",
    L"remapShortcuts/global/runProgramStartInDir",
    L"remapShortcuts/global/openUri",
    L"remapShortcuts/appSpecific/runProgramFilePath",
    L"remapShortcuts/appSpecific/runProgramArgs",
    L"remapShortcuts/appSpecific/runProgramStartInDir",
    L"remapShortcuts/appSpecific/openUri",
};

vector<wstring> filesToDelete = {
    L"AdvancedPaste\\lastQuery.json",
    L"AdvancedPaste\\kernelQueryCache.json",
    L"PowerToys Run\\Cache",
    L"PowerRename\\replace-mru.json",
    L"PowerRename\\search-mru.json",
    L"PowerToys Run\\Settings\\UserSelectedRecord.json",
    L"PowerToys Run\\Settings\\QueryHistory.json",
    L"NewPlus\\Templates",
    L"etw",
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

// Returns false when the file was left as it is, because it could not be parsed or written.
bool HideForFile(const path& dir, const wstring& relativePath, const vector<wstring>& escapePaths)
{
    path jsonPath = dir;
    jsonPath.append(relativePath);
    auto jObject = json::from_file(jsonPath.wstring());
    if (!jObject.has_value())
    {
        wprintf(L"Failed to parse file %s\n", jsonPath.c_str());
        return false;
    }

    JsonValue jValue = json::value(jObject.value());
    for (auto xpath : escapePaths)
    {
        vector<wstring> xpathArray = GetXpathArray(xpath);
        HideByXPath(jValue, xpathArray, 0);
    }

    // Same output as json::to_file, which does not tell whether the file was written.
    ofstream output{ jsonPath, ios::binary };
    output << winrt::to_string(jObject.value().Stringify());
    output.flush();
    if (!output.good())
    {
        wprintf(L"Failed to write file %s\n", jsonPath.c_str());
        return false;
    }

    return true;
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

// The report is stopped before it is zipped: delete the working copy, which may still hold data that was not hidden
// yet. If that fails, the file that could not be cleaned is probably what stops the delete: say where the copy is.
void DeleteReportCopyOrSayWhere(const path& reportDir)
{
    if (!DeleteFolder(reportDir))
    {
        wprintf_s(L"The report's working copy was left in %s; delete it by hand\n", reportDir.c_str());
    }
}

// A profile whose private data could not be hidden must not stay in the report as it is: either it could not
// be parsed, so it cannot be searched for private data, or the result could not be written back. Leave a short
// note in its place, worded for both cases: that the profile could not be processed is useful for diagnosis.
// Returns false when the file could neither be replaced nor deleted, so its content is still in the report.
bool RemoveKeyboardManagerProfileContent(const path& profilePath)
{
    bool replaced = false;
    try
    {
        ofstream note{ profilePath, ios::binary | ios::trunc };
        note << R"({"bugReportToolNote":"The private data in this Keyboard Manager profile could not be hidden: the file could not be read as JSON, or it could not be rewritten. Its content was removed from the report."})";
        note.flush();
        replaced = note.good();
    }
    catch (...)
    {
    }

    if (replaced)
    {
        return true;
    }

    error_code err;
    remove(profilePath, err);
    if (err.value() != 0)
    {
        wprintf_s(L"Failed to delete %s. Error code: %d\n", profilePath.c_str(), err.value());
        return false;
    }

    return true;
}

// The report holds copies, which keep the attributes of the user's files: a profile the user made read-only would
// otherwise be neither rewritten nor replaced. Only the copy is changed.
void ClearReadOnlyAttribute(const path& filePath)
{
    const DWORD attributes = GetFileAttributesW(filePath.c_str());
    if (attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_READONLY) != 0)
    {
        SetFileAttributesW(filePath.c_str(), attributes & ~FILE_ATTRIBUTE_READONLY);
    }
}

bool IsKeyboardManagerFileKeptAsIs(const path& filename)
{
    for (const auto& name : keyboardManagerFilesKeptAsIs)
    {
        if (_wcsicmp(filename.c_str(), name.c_str()) == 0)
        {
            return true;
        }
    }

    return false;
}

// Hides the private data of every Keyboard Manager file under folder (dir is the root it is relative to). Returns
// false when some of it could still be in the report.
bool HideKeyboardManagerFiles(const path& dir, const path& folder)
{
    // Keyboard Manager keeps one file per profile, named by the user. List the files first: they are rewritten below.
    vector<path> profiles;
    vector<path> otherFiles;
    error_code err;
    for (directory_iterator it{ folder, err }; !err && it != directory_iterator{}; it.increment(err))
    {
        error_code entryErr;
        if (!it->is_regular_file(entryErr))
        {
            continue;
        }

        const auto filename = it->path().filename();
        if (_wcsicmp(filename.extension().c_str(), L".json") != 0)
        {
            // Not a file Keyboard Manager reads: a temporary file left behind when a save was interrupted, or a
            // copy made by hand ("Work.json.bak"). Either can hold a whole profile.
            otherFiles.push_back(it->path());
        }
        else if (!IsKeyboardManagerFileKeptAsIs(filename))
        {
            profiles.push_back(it->path());
        }
    }

    if (err)
    {
        wprintf(L"Failed to list the Keyboard Manager files\n");
        return false;
    }

    // The files in subfolders. Keyboard Manager writes only logs (.log) there, so any other file is a copy made by
    // hand (a folder of backups: "Work.json", "Work.json.bak") or left behind: a json file is cleaned like a
    // profile, and a file of another kind is taken out.
    for (recursive_directory_iterator it{ folder, err }; !err && it != recursive_directory_iterator{}; it.increment(err))
    {
        error_code entryErr;
        if (it.depth() == 0 || !it->is_regular_file(entryErr))
        {
            continue;
        }

        const auto extension = it->path().extension();
        if (_wcsicmp(extension.c_str(), L".json") == 0)
        {
            profiles.push_back(it->path());
        }
        else if (_wcsicmp(extension.c_str(), L".log") != 0)
        {
            otherFiles.push_back(it->path());
        }
    }

    if (err)
    {
        wprintf(L"Failed to list the Keyboard Manager files\n");
        return false;
    }

    // One profile at a time, so that a failure does not leave the other profiles as they are.
    bool allHidden = true;
    for (const auto& profile : profiles)
    {
        ClearReadOnlyAttribute(profile);
        bool hidden = false;
        try
        {
            hidden = HideForFile(dir, profile.lexically_relative(dir).wstring(), keyboardManagerProfileEscapeInfo);
        }
        catch (...)
        {
            wprintf(L"Failed to hide private data in %s\n", profile.c_str());
        }

        if (!hidden && !RemoveKeyboardManagerProfileContent(profile))
        {
            allHidden = false;
        }
    }

    for (const auto& otherFile : otherFiles)
    {
        ClearReadOnlyAttribute(otherFile);
        if (!DeleteFolder(otherFile.wstring()))
        {
            allHidden = false;
        }
    }

    return allHidden;
}

// Returns false when private Keyboard Manager data could still be in the report.
bool HideKeyboardManagerPrivateInfo(const path& dir)
{
    const path folder = dir / keyboardManagerFolder;
    error_code err;
    if (!exists(folder, err))
    {
        if (!err)
        {
            return true;
        }

        wprintf(L"Failed to check the Keyboard Manager folder %s\n", folder.c_str());
        return false;
    }

    if (HideKeyboardManagerFiles(dir, folder))
    {
        return true;
    }

    // Something could not be cleaned (the files that were not listed cannot be handled one by one): take the whole
    // folder out of the report instead.
    wprintf(L"Removing the Keyboard Manager folder %s from the report\n", folder.c_str());
    return DeleteFolder(folder.wstring());
}

// Hides the private data in one copy of the settings folder: the report's, or the updater's (ConfigBackup), which
// has the same layout. Returns false when private Keyboard Manager data could still be in it.
bool HideUserPrivateInfoInSettingsCopy(const filesystem::path& dir)
{
    // Replace data in json files
    for (auto& it : escapeInfo)
    {
        HideForFile(dir, it.first, it.second);
    }

    const bool keyboardManagerHidden = HideKeyboardManagerPrivateInfo(dir);

    // Delete files
    for (auto it : filesToDelete)
    {
        auto path = dir;
        path = path.append(it);
        DeleteFolder(path);
    }

    return keyboardManagerHidden;
}

// Returns false when private Keyboard Manager data could still be in the report.
bool HideUserPrivateInfo(const filesystem::path& dir)
{
    bool hidden = HideUserPrivateInfoInSettingsCopy(dir);

    // The updater's copy, if an update stopped before removing it.
    const path configBackup = dir / configBackupFolder;
    error_code err;
    if (exists(configBackup, err))
    {
        hidden = HideUserPrivateInfoInSettingsCopy(configBackup) && hidden;
    }
    else if (err)
    {
        wprintf(L"Failed to check the folder %s\n", configBackup.c_str());
        hidden = DeleteFolder(configBackup.wstring()) && hidden;
    }

    return hidden;
}
