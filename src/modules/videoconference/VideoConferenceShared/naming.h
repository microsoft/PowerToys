#pragma once
#include <string_view>
#include <string>

std::wstring ObtainStableGlobalNameForKernelObject(const std::wstring_view name, const bool restricted);