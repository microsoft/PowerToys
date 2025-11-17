#pragma once

#include <WorkspacesLib/WbemHelper.h>

class CommandLineArgsHelper
{
public:
    CommandLineArgsHelper();
    ~CommandLineArgsHelper() = default;

    std::wstring GetCommandLineArgs(DWORD processID) const;

private:
    std::unique_ptr<WbemHelper> m_wbemHelper;
};
