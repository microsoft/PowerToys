#pragma once

#include <map>
#include <string>

namespace PowerScriptsRunner
{
    void RunAction(
        const std::wstring& scriptId,
        const std::map<std::wstring, std::wstring>& parameters);
}
