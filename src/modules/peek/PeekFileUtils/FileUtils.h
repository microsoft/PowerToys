#pragma once

#include "common.h"

#pragma comment(lib, "shlwapi.lib")

namespace FileUtils
{
    extern "C" __declspec(dllexport) wchar_t* GetSelectedFile();
};
