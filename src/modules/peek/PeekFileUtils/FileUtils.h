#pragma once

#include "common.h"

#pragma comment(lib, "shlwapi.lib")

namespace FileUtils
{
    HRESULT GetSelectedFile(String& filepath);
};
