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
    bool isRestarted;
    bool forceSave;
    bool skipMinimized;
};

CommandLineArgs split(std::wstring s, const std::wstring& delimiter)
{
    CommandLineArgs cmdArgs{};
    cmdArgs.isRestarted = false;
    cmdArgs.forceSave = false;
    cmdArgs.skipMinimized = false;

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

    bool isFirstToken = true; // Track if this is the first token (exe path)
    for (const auto& tokenValue : tokens)
    {
        auto normalizedToken = normalizeToken(tokenValue);
        if (normalizedToken.empty())
        {
            continue;
        }

        // Skip the first token only if it looks like an executable path
        if (isFirstToken)
        {
            isFirstToken = false;
            // Skip if it ends with .exe or contains path separators
            if ((normalizedToken.length() > 4 && normalizedToken.substr(normalizedToken.length() - 4) == L".exe") || 
                normalizedToken.find(L'\\') != std::wstring::npos ||
                normalizedToken.find(L'/') != std::wstring::npos)
            {
                continue;
            }
        }

        if (_wcsicmp(normalizedToken.c_str(), NonLocalizable::restartedString) == 0)
        {
            cmdArgs.isRestarted = true;
        }
        else if (_wcsicmp(normalizedToken.c_str(), L"-force") == 0)
        {
            cmdArgs.forceSave = true;
        }
        else if (_wcsicmp(normalizedToken.c_str(), L"-skipMinimized") == 0)
        {
            cmdArgs.skipMinimized = true;
        }
        else
        {
            // If it's not a flag, treat it as workspaceId
            // This allows for both GUID format and custom formats like "yy-MM-dd-HH-mm"
            if (cmdArgs.workspaceId.empty())
            {
                cmdArgs.workspaceId = normalizedToken;
            }
        }
    }

    return cmdArgs;
}
