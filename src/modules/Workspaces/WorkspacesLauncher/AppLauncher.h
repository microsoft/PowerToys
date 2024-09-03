#pragma once

#include <WorkspacesLib/WorkspacesData.h>

#include <LauncherUIHelper.h>

using ErrorList = std::vector<std::pair<std::wstring, std::wstring>>;

bool Launch(WorkspacesData::WorkspacesProject& project, const LauncherUIHelper& helper, ErrorList& launchErrors);