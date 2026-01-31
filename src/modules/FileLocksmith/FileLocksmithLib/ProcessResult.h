#pragma once
#include <string>
#include <vector>
#include <Windows.h>

struct ProcessResult
{
    std::wstring name;
    DWORD pid;
    std::wstring user;
    std::vector<std::wstring> files;
};
