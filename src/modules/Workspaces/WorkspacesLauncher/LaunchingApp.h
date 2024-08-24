#pragma once

#include <Windows.h>
#include <WorkspacesLib/WorkspacesData.h>

struct LaunchingApp
{
    WorkspacesData::WorkspacesProject::Application application;
    HWND window;
    std::wstring state;
};

using LaunchingApps = std::vector<LaunchingApp>;