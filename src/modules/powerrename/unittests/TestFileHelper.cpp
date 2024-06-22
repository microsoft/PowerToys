#include "pch.h"
#include "TestFileHelper.h"
#include <iostream>
#include <fstream>
#include <Objbase.h>

namespace fs = std::filesystem;

CTestFileHelper::CTestFileHelper()
{
    _CreateTempDirectory();
}
CTestFileHelper::~CTestFileHelper()
{
    _DeleteTempDirectory();
}

// Pass a relative path which will be appended to the temp directory path
bool CTestFileHelper::AddFile(_In_ const std::wstring path)
{
    fs::path newFilePath = _tempDirectory;
    newFilePath.append(path);
    std::ofstream ofs(newFilePath);
    ofs.close();
    return true;
}

// Pass a relative path which will be appended to the temp directory path
bool CTestFileHelper::AddFolder(_In_ const std::wstring path)
{
    fs::path newFolderPath = _tempDirectory;
    newFolderPath.append(path);
    return fs::create_directory(fs::path(newFolderPath));
}

fs::path CTestFileHelper::GetFullPath(_In_ const std::wstring path)
{
    fs::path fullPath = _tempDirectory;
    fullPath.append(path);
    return fullPath;
}

bool CTestFileHelper::PathExists(_In_ const std::wstring path)
{
    fs::path fullPath = _tempDirectory;
    fullPath.append(path);
    return fs::exists(fullPath);
}

bool CTestFileHelper::PathExistsCaseSensitive(_In_ const std::wstring path)
{
    fs::path tempDirPath = fs::path(_tempDirectory);
    for (const auto& entry : fs::directory_iterator(tempDirPath))
    {
        if (entry.path().filename().wstring() == path)
        {
            return true;
        }
    }
    return false;
}

bool CTestFileHelper::_CreateTempDirectory()
{
    // Initialize to the temp directory
    _tempDirectory = fs::temp_directory_path();

    // Create a unique folder name
    GUID guid = { 0 };
    CoCreateGuid(&guid);

    wchar_t uniqueName[MAX_PATH] = { 0 };
    StringFromGUID2(guid, uniqueName, ARRAYSIZE(uniqueName));

    _tempDirectory.append(uniqueName);

    return fs::create_directory(_tempDirectory);
}

void CTestFileHelper::_DeleteTempDirectory()
{
    fs::remove_all(_tempDirectory);
}
