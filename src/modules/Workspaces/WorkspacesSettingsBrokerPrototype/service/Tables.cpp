// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "Tables.h"

#include <algorithm>

namespace SettingsBrokerPrototype
{
    namespace
    {
        constexpr TrustedTarget kTargets[] = {
            { 1, L"Workspaces", L"workspaces.json" },
            { 2, L"KeyboardManager", L"default.json" },
        };

        const CallerBinding kCallers[] = {
            { L"PTSettingsBrokerPrototype.WorkspacesClient.exe", { 1 } },
            { L"PTSettingsBrokerPrototype.KeyboardManagerClient.exe", { 2 } },
        };
    }

    const TrustedTarget* FindTarget(uint32_t targetId)
    {
        for (const auto& target : kTargets)
        {
            if (target.id == targetId)
            {
                return &target;
            }
        }
        return nullptr;
    }

    const CallerBinding* FindCallerBinding(const std::wstring& basename)
    {
        for (const auto& caller : kCallers)
        {
            if (_wcsicmp(caller.executableBasename, basename.c_str()) == 0)
            {
                return &caller;
            }
        }
        return nullptr;
    }

    bool BindingAllowsTarget(const CallerBinding& binding, uint32_t targetId)
    {
        return std::find(binding.allowedTargetIds.begin(),
                         binding.allowedTargetIds.end(),
                         targetId) != binding.allowedTargetIds.end();
    }
}
