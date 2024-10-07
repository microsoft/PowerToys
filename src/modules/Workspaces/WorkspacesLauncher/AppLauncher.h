#pragma once

#include <shellapi.h>

#include <WorkspacesLib/AppUtils.h>
#include <WorkspacesLib/LaunchingStatus.h>
#include <WorkspacesLib/Result.h>
#include <WorkspacesLib/WorkspacesData.h>

namespace AppLauncher
{
    using ErrorList = std::vector<std::pair<std::wstring, std::wstring>>;

    bool Launch(const WorkspacesData::WorkspacesProject::Application& app, ErrorList& launchErrors);
    Result<SHELLEXECUTEINFO, std::wstring> LaunchApp(const std::wstring& appPath, const std::wstring& commandLineArgs, bool elevated);
}
