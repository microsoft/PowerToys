#include "naming.h"

#include "username.h"

std::wstring ObtainStableGlobalNameForKernelObject(const std::wstring_view name, const bool restricted)
{
    static const std::optional<std::wstring> username = ObtainActiveUserName();
    std::wstring result = L"Global\\";
    if (restricted)
    {
        result += L"Restricted\\";
    }
    if (username)
    {
        result += *username;
    }
    result += name;
    return result;
}
