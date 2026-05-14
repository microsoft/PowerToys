#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

namespace cmdArg
{
    const inline wchar_t* RUN_NONELEVATED = L"-run-non-elevated";
    const inline wchar_t* RUN_AS_USER = L"-run-as-user";
    const inline wchar_t* RUN_AS_ADMIN = L"-run-as-admin";
}
