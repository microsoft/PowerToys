#pragma once

#include <string>
#include <Windows.h>

#pragma comment(lib, "shlwapi.lib")

// typedef String based on compilation configuration
#ifndef UNICODE
typedef std::string String;
#else
typedef std::wstring String;
#endif

namespace FileUtils
{
    HRESULT GetSelectedFile(String& filepath);
};
