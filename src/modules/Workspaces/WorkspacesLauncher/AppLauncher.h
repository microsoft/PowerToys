#pragma once

#include <shellapi.h>

#include <WorkspacesLib/LaunchingStatus.h>
#include <WorkspacesLib/Result.h>
#include <WorkspacesLib/WorkspacesData.h>

namespace AppLauncher
{
    using ErrorList = std::vector<std::pair<std::wstring, std::wstring>>;

    Result<SHELLEXECUTEINFO, std::wstring> LaunchApp(const std::wstring& appPath, const std::wstring& commandLineArgs, bool elevated);

    bool Launch(WorkspacesData::WorkspacesProject& project, LaunchingStatus& launchingStatus, ErrorList& launchErrors);
}
