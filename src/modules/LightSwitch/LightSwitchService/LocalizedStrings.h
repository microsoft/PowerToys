// Copyright (c) Microsoft Corporation.
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "Generated Files/resource.h"
#include <common/utils/resources.h>
#include <format>

// GET_RESOURCE_STRING uses __ImageBase, so shared service sources also load
// resources from their containing DLL when compiled into the native tests.
namespace LightSwitchStrings
{
    template<typename... Args>
    std::wstring Format(const std::wstring& text, const Args&... args)
    {
        return std::vformat(text, std::make_wformat_args(args...));
    }
}
