#pragma once

#include <filesystem>
#include <string>
#include <windows.h>

class CTestFileHelper
{
public:
    CTestFileHelper();
    ~CTestFileHelper();

    bool AddFile(_In_ const std::wstring path);
    bool AddFolder(_In_ const std::wstring path);
    const std::filesystem::path GetTempDirectory() { return _tempDirectory; }
    bool PathExists(_In_ const std::wstring path);
    bool PathExistsCaseSensitive(_In_ const std::wstring path);
    std::filesystem::path GetFullPath(_In_ const std::wstring path);

private:
    bool _CreateTempDirectory();
    void _DeleteTempDirectory();

    std::filesystem::path _tempDirectory;
};