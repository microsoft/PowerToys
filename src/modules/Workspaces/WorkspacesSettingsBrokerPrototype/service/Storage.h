// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <windows.h>

#include <string>
#include <vector>

#include "Tables.h"

namespace SettingsBrokerPrototype
{
    std::wstring GetStoreRoot();
    HRESULT ReadValue(const std::wstring& callerSid,
                      const TrustedTarget& target,
                      std::vector<BYTE>& bytes);
    HRESULT WriteValue(const std::wstring& callerSid,
                       const TrustedTarget& target,
                       const std::vector<BYTE>& bytes);
}
