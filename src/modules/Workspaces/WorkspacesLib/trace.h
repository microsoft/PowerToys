#pragma once

#include <modules/interface/powertoy_module_interface.h>

#include <WorkspacesLib/WorkspacesData.h>
#include <workspaces-common/InvokePoint.h>

#include <common/Telemetry/TraceBase.h>

class Trace
{
public:
    class Workspaces : public telemetry::TraceBase
    {
    public:
        static void Enable(bool enabled) noexcept;
        static void Launch(bool success,
                           const WorkspacesData::WorkspacesProject& project,
                           InvokePoint invokePoint,
                           double launchTimeSeconds,
                           bool setupIsDifferent,
                           const std::vector<std::pair<std::wstring, std::wstring>> errors) noexcept;
    };
};
