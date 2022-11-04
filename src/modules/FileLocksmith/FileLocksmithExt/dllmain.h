#pragma once

#include "pch.h"

namespace globals
{
    extern HMODULE instance;
    extern std::atomic<ULONG> ref_count;
    extern std::atomic<bool> enabled;
}
