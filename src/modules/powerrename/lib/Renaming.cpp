#include "pch.h"
#include <winrt/base.h>

#include "Renaming.h"
#include <Helpers.h>

namespace fs = std::filesystem;

bool DoRename(CComPtr<IPowerRenameRegEx>& spRenameRegEx, unsigned long& itemEnumIndex, CComPtr<IPowerRenameItem>& spItem)
{
    bool wouldRename = false;
    DWORD flags = 0;
    winrt::check_hresult(spRenameRegEx->GetFlags(&flags));

    PWSTR replaceTerm = nullptr;
    bool useFileTime = false;

    winrt::check_hresult(spRenameRegEx->GetReplaceTerm(&replaceTerm));

    if (isFileTimeUsed(replaceTerm))
    {
        useFileTime = true;
    }
    CoTaskMemFree(replaceTerm);

    int id = -1;
    winrt::check_hresult(spItem->GetId(&id));

    bool isFolder = false;
    bool isSubFolderContent = false;
    winrt::check_hresult(spItem->GetIsFolder(&isFolder));
    winrt::check_hresult(spItem->GetIsSubFolderContent(&isSubFolderContent));
    if ((isFolder && (flags & PowerRenameFlags::ExcludeFolders)) ||
        (!isFolder && (flags & PowerRenameFlags::ExcludeFiles)) ||
        (isSubFolderContent && (flags & PowerRenameFlags::ExcludeSubfolders)) ||
        (isFolder && (flags & PowerRenameFlags::ExtensionOnly)))
    {
        // Exclude this item from renaming.  Ensure new name is cleared.
        winrt::check_hresult(spItem->PutNewName(nullptr));

        return wouldRename;
    }

    PWSTR originalName = nullptr;
    winrt::check_hresult(spItem->GetOriginalName(&originalName));

    PWSTR currentNewName = nullptr;
    winrt::check_hresult(spItem->GetNewName(&currentNewName));

    wchar_t sourceName[MAX_PATH] = { 0 };

    if (isFolder)
    {
        StringCchCopy(sourceName, ARRAYSIZE(sourceName), originalName);
    }
    else
    {
        if (flags & NameOnly)
        {
            StringCchCopy(sourceName, ARRAYSIZE(sourceName), fs::path(originalName).stem().c_str());
        }
        else if (flags & ExtensionOnly)
        {
            std::wstring extension = fs::path(originalName).extension().wstring();
            if (!extension.empty() && extension.front() == '.')
            {
                extension = extension.erase(0, 1);
            }
            StringCchCopy(sourceName, ARRAYSIZE(sourceName), extension.c_str());
        }
        else
        {
            StringCchCopy(sourceName, ARRAYSIZE(sourceName), originalName);
        }
    }

    SYSTEMTIME fileTime = { 0 };

    if (useFileTime)
    {
        winrt::check_hresult(spItem->GetTime(&fileTime));
        winrt::check_hresult(spRenameRegEx->PutFileTime(fileTime));
    }

    PWSTR newName = nullptr;

    // Failure here means we didn't match anything or had nothing to match
    // Call put_newName with null in that case to reset it
    winrt::check_hresult(spRenameRegEx->Replace(sourceName, &newName, itemEnumIndex));

    if (useFileTime)
    {
        winrt::check_hresult(spRenameRegEx->ResetFileTime());
    }

    wchar_t resultName[MAX_PATH] = { 0 };

    PWSTR newNameToUse = nullptr;

    // newName == nullptr likely means we have an empty search string.  We should leave newNameToUse
    // as nullptr so we clear the renamed column
    // Except string transformation is selected.

    if (newName == nullptr && (flags & Uppercase || flags & Lowercase || flags & Titlecase || flags & Capitalized))
    {
        SHStrDup(sourceName, &newName);
    }

    if (newName != nullptr)
    {
        newNameToUse = resultName;

        if (isFolder)
        {
            StringCchCopy(resultName, ARRAYSIZE(resultName), newName);
        }
        else
        {
            if (flags & NameOnly)
            {
                StringCchPrintf(resultName, ARRAYSIZE(resultName), L"%s%s", newName, fs::path(originalName).extension().c_str());
            }
            else if (flags & ExtensionOnly)
            {
                std::wstring extension = fs::path(originalName).extension().wstring();
                if (!extension.empty())
                {
                    StringCchPrintf(resultName, ARRAYSIZE(resultName), L"%s.%s", fs::path(originalName).stem().c_str(), newName);
                }
                else
                {
                    StringCchCopy(resultName, ARRAYSIZE(resultName), originalName);
                }
            }
            else
            {
                StringCchCopy(resultName, ARRAYSIZE(resultName), newName);
            }
        }
    }

    wchar_t trimmedName[MAX_PATH] = { 0 };
    if (newNameToUse != nullptr)
    {
        winrt::check_hresult(GetTrimmedFileName(trimmedName, ARRAYSIZE(trimmedName), newNameToUse));
        newNameToUse = trimmedName;
    }

    wchar_t transformedName[MAX_PATH] = { 0 };
    if (newNameToUse != nullptr && (flags & Uppercase || flags & Lowercase || flags & Titlecase || flags & Capitalized))
    {
        try
        {
            winrt::check_hresult(GetTransformedFileName(transformedName, ARRAYSIZE(transformedName), newNameToUse, flags, isFolder));
        }
        catch (...)
        {
        }
        newNameToUse = transformedName;
    }

    // No change from originalName so set newName to
    // null so we clear it from our UI as well.
    if (lstrcmp(originalName, newNameToUse) == 0)
    {
        newNameToUse = nullptr;
    }

    spItem->PutStatus(PowerRenameItemRenameStatus::ShouldRename);
    if (newNameToUse != nullptr)
    {
        wouldRename = true;
        std::wstring newNameToUseWstr{ newNameToUse };
        PWSTR path = nullptr;
        spItem->GetPath(&path);

        // Following characters cannot be used for file names.
        // Ref https://learn.microsoft.com/windows/win32/fileio/naming-a-file#naming-conventions
        if (newNameToUseWstr.contains('<') ||
            newNameToUseWstr.contains('>') ||
            newNameToUseWstr.contains(':') ||
            newNameToUseWstr.contains('"') ||
            newNameToUseWstr.contains('\\') ||
            newNameToUseWstr.contains('/') ||
            newNameToUseWstr.contains('|') ||
            newNameToUseWstr.contains('?') ||
            newNameToUseWstr.contains('*'))
        {
            spItem->PutStatus(PowerRenameItemRenameStatus::ItemNameInvalidChar);
            wouldRename = false;
        }
        // Max file path is 260 and max folder path is 247.
        // Ref https://learn.microsoft.com/windows/win32/fileio/maximum-file-path-limitation?tabs=registry
        else if ((isFolder && lstrlen(path) + (lstrlen(newNameToUse) - lstrlen(originalName)) > 247) ||
                 lstrlen(path) + (lstrlen(newNameToUse) - lstrlen(originalName)) > 260)
        {
            spItem->PutStatus(PowerRenameItemRenameStatus::ItemNameTooLong);
            wouldRename = false;
        }
    }

    winrt::check_hresult(spItem->PutNewName(newNameToUse));

    CoTaskMemFree(newName);
    CoTaskMemFree(currentNewName);
    CoTaskMemFree(originalName);

    return wouldRename;
}