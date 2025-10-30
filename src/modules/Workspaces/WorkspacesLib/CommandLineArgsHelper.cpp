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
        // First try to find quoted executable path (handles paths with spaces)
        std::wstring quotedPath = L"\"" + executablePath + L"\"";
        auto quotedPos = commandLineArgs.find(quotedPath);
        if (quotedPos != std::wstring::npos)
        {
            // Ensure this is at the beginning or after whitespace (not a substring)
            if (quotedPos == 0 || std::iswspace(commandLineArgs[quotedPos - 1]))
            {
                commandLineArgs = commandLineArgs.substr(quotedPos + quotedPath.size());
                // Remove leading space if present
                if (!commandLineArgs.empty() && commandLineArgs[0] == L' ')
                {
                    commandLineArgs = commandLineArgs.substr(1);
                }
            }
            else
            {
                // Fall back to unquoted path logic
                goto try_unquoted;
            }
        }
        else
        {
try_unquoted:
            // Fall back to unquoted executable path (original behavior)
            auto pos = commandLineArgs.find(executablePath);
            if (pos != std::wstring::npos)
            {
                // Ensure this is at the beginning or after whitespace (not a substring)
                if (pos == 0 || std::iswspace(commandLineArgs[pos - 1]))
                {
                    commandLineArgs = commandLineArgs.substr(pos + executablePath.size());
                    auto spacePos = commandLineArgs.find_first_of(' ');
                    if (spacePos != std::wstring::npos)
                    {
                        commandLineArgs = commandLineArgs.substr(spacePos + 1);
                    }
                    else
                    {
                        commandLineArgs = L"";
                    }
                }
            }
        }
    }

    return commandLineArgs;
}
