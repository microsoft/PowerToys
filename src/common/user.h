#pragma once

#include <optional>
#include <string>
#include <string_view>

std::optional<std::wstring> ObtainActiveUserName();

std::wstring ObtainStableGlobalNameForKernelObject(const std::wstring_view name, const bool restricted);