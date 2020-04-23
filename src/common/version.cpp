#include "pch.h"
#include "version.h"

version_architecture get_current_architecture()
{
    // TODO: detect ARM build with #ifdef
    return version_architecture::x64;
}

const wchar_t* get_architecture_string(const version_architecture v)
{
    switch (v)
    {
    case version_architecture::x64:
        return L"x64";
    case version_architecture::arm:
        return L"arm";
    default:
        throw std::runtime_error("unknown architecture");
    }
}