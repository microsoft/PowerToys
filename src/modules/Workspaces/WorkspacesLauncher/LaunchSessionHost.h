// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#pragma once

#include <optional>
#include <string>
#include <vector>

namespace WorkspacesSession
{
    std::optional<int> TryRunHost(const std::vector<std::wstring>& arguments);
}
