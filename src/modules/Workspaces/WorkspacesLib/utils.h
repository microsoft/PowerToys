#pragma once

#include <algorithm>
#include <cwchar>
#include <string>
#include <vector>

#include <workspaces-common/GuidUtils.h>
#include <workspaces-common/InvokePoint.h>

namespace NonLocalizable
{
    const wchar_t restartedString[] = L"restarted";
}

struct CommandLineArgs
{
    std::wstring workspaceId;
    InvokePoint invokePoint;
    bool isRestarted;
    bool forceSave;
};

CommandLineArgs split(std::wstring s, const std::wstring& delimiter)
{
    CommandLineArgs cmdArgs{};
    cmdArgs.isRestarted = false;
    cmdArgs.forceSave = false;

    size_t pos = 0;
    std::wstring token;
    std::vector<std::wstring> tokens;
    while ((pos = s.find(delimiter)) != std::wstring::npos)
    {
        token = s.substr(0, pos);
        tokens.push_back(token);
        s.erase(0, pos + delimiter.length());
    }
    tokens.push_back(s);

    auto normalizeToken = [](std::wstring value) {
        if (!value.empty() && value.front() == L'"')
        {
            value.erase(0, 1);
        }
        if (!value.empty() && value.back() == L'"')
        {
            value.pop_back();
        }
        return value;
    };

    for (const auto& tokenValue : tokens)
    {
        auto normalizedToken = normalizeToken(tokenValue);
        if (normalizedToken.empty())
        {
            continue;
        }

        if (_wcsicmp(normalizedToken.c_str(), NonLocalizable::restartedString) == 0)
        {
            cmdArgs.isRestarted = true;
        }
        else if (_wcsicmp(normalizedToken.c_str(), L"-force") == 0)
        {
            cmdArgs.forceSave = true;
        }
        else
        {
            auto guid = GuidFromString(normalizedToken);
            if (guid.has_value())
            {
                cmdArgs.workspaceId = normalizedToken;
            }
            else
            {
                try
                {
                    auto invokePoint = static_cast<InvokePoint>(std::stoi(normalizedToken));
                    cmdArgs.invokePoint = invokePoint;
                }
                catch (std::exception)
                {
                }
            }
        }
    }

    return cmdArgs;
}
