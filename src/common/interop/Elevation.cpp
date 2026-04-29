#include "pch.h"
#define SUPRESS_LOGGER
#include "../utils/elevation.h"
#include "Elevation.h"
#include "Elevation.g.cpp"

namespace winrt::PowerToys::Interop::implementation
{
    void Elevation::RunNonElevated(const winrt::hstring& file, const winrt::hstring& params)
    {
        RunNonElevatedFailsafe((std::wstring)file, params.c_str(), get_module_folderpath());
    }
}

