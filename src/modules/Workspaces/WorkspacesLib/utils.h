#pragma once

#include <vector>
#include <string>

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
};

CommandLineArgs split(std::wstring s, const std::wstring& delimiter)
{
    CommandLineArgs cmdArgs{};
    cmdArgs.isRestarted = false;

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

    for (const auto& token : tokens)
    {
        if (token == NonLocalizable::restartedString)
        {
            cmdArgs.isRestarted = true;
        }
        else
        {
            auto guid = GuidFromString(token);
            if (guid.has_value())
            {
                cmdArgs.workspaceId = token;
            }
            else
            {
                try
                {
                    auto invokePoint = static_cast<InvokePoint>(std::stoi(token));
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
