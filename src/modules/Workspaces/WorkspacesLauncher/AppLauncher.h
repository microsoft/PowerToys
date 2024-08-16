#pragma once

#include <WorkspacesLib/WorkspacesData.h>

using ErrorList = std::vector<std::pair<std::wstring, std::wstring>>;

bool Launch(WorkspacesData::WorkspacesProject& project, const std::vector<WorkspacesData::WorkspacesProject::Monitor>& monitors, ErrorList& launchErrors);