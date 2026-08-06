// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <cstdint>
#include <string>
#include <vector>

namespace SettingsBrokerPrototype
{
    struct TrustedTarget
    {
        uint32_t id;
        const wchar_t* nameSpace;
        const wchar_t* fileName;
    };

    struct CallerBinding
    {
        const wchar_t* executableBasename;
        std::vector<uint32_t> allowedTargetIds;
    };

    const TrustedTarget* FindTarget(uint32_t targetId);
    const CallerBinding* FindCallerBinding(const std::wstring& basename);
    bool BindingAllowsTarget(const CallerBinding& binding, uint32_t targetId);
}
