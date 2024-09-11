#pragma once

#include <Windows.h>
#include <WorkspacesLib/LaunchingStateEnum.h>
#include <WorkspacesLib/WorkspacesData.h>

struct LaunchingApp
{
    WorkspacesData::WorkspacesProject::Application application;
    HWND window;
    LaunchingState state;
};

using LaunchingApps = std::vector<LaunchingApp>;