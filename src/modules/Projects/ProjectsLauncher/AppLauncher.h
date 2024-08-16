#pragma once

#include <ProjectsLib/ProjectsData.h>

using ErrorList = std::vector<std::pair<std::wstring, std::wstring>>;

bool Launch(ProjectsData::Project& project, const std::vector<ProjectsData::Project::Monitor>& monitors, ErrorList& launchErrors);