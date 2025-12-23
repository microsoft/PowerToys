#pragma once

#include <vector>
#include <wil/resource.h>
#include <Shlwapi.h>
#include <Psapi.h>
#include <string_view>

std::vector<wil::unique_process_handle> getProcessHandlesByName(std::wstring_view processName, DWORD handleAccess);
