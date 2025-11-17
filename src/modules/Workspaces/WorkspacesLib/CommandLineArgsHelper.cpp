#include "pch.h"
#include "CommandLineArgsHelper.h"

#include <common/logger/logger.h>

CommandLineArgsHelper::CommandLineArgsHelper() :
    m_wbemHelper(WbemHelper::Create())
{
    if (!m_wbemHelper)
    {
        Logger::error(L"Failed to create WbemHelper");
    }
}

std::wstring CommandLineArgsHelper::GetCommandLineArgs(DWORD processID) const
{
    if (!m_wbemHelper)
    {
        Logger::error(L"WbemHelper not initialized");
        return L"";
    }

    std::wstring executablePath = m_wbemHelper->GetExecutablePath(processID);
    std::wstring commandLineArgs = m_wbemHelper->GetCommandLineArgs(processID);

    if (!commandLineArgs.empty())
    {
        auto pos = commandLineArgs.find(executablePath);
        if (pos != std::wstring::npos)
        {
            commandLineArgs = commandLineArgs.substr(pos + executablePath.size());
            auto spacePos = commandLineArgs.find_first_of(' ');
            if (spacePos != std::wstring::npos)
            {
                commandLineArgs = commandLineArgs.substr(spacePos + 1);
            }
        }
    }

    return commandLineArgs;
}
