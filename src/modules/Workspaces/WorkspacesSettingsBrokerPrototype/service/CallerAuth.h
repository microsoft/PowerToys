// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <windows.h>

#include <string>

#include "Tables.h"

namespace SettingsBrokerPrototype
{
    struct CallerIdentity
    {
        std::wstring sid;
        std::wstring imagePath;
        DWORD processId = 0;
        const CallerBinding* binding = nullptr;
    };

    HRESULT AuthenticateCaller(HANDLE pipe, CallerIdentity& identity);
}
