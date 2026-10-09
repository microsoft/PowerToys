#include "version.h"

#include <stdexcept>

version_architecture get_current_architecture()
{
#ifdef _M_ARM64
    return version_architecture::arm;
#else
    return version_architecture::x64;
#endif
}

const wchar_t* get_architecture_string(const version_architecture v)
{
    switch (v)
    {
    case version_architecture::x64:
        return L"x64";
    case version_architecture::arm:
        return L"arm64";
    default:
        throw std::runtime_error("unknown architecture");
    }
}