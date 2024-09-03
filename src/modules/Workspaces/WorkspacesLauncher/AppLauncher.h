#pragma once

#include <shellapi.h>

#include <WorkspacesLib/Result.h>
#include <WorkspacesLib/WorkspacesData.h>

#include <LauncherUIHelper.h>

using ErrorList = std::vector<std::pair<std::wstring, std::wstring>>;

Result<SHELLEXECUTEINFO, std::wstring> LaunchApp(const std::wstring& appPath, const std::wstring& commandLineArgs, bool elevated);

bool Launch(WorkspacesData::WorkspacesProject& project, const LauncherUIHelper& uiHelper, ErrorList& launchErrors);