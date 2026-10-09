#pragma once

#include <FancyZonesLib/on_thread_executor.h>
#include <FancyZonesLib/WorkAreaConfiguration.h>

namespace NonLocalizable
{
    namespace EditorParametersIds
    {
        const static wchar_t* Dpi = L"dpi";
        const static wchar_t* MonitorNameId = L"monitor";
        const static wchar_t* MonitorInstanceId = L"monitor-instance-id";
        const static wchar_t* MonitorSerialNumberId = L"monitor-serial-number";
        const static wchar_t* MonitorNumberId = L"monitor-number";
        const static wchar_t* VirtualDesktopId = L"virtual-desktop";
        const static wchar_t* TopCoordinate = L"top-coordinate";
        const static wchar_t* LeftCoordinate = L"left-coordinate";
        const static wchar_t* WorkAreaWidth = L"work-area-width";
        const static wchar_t* WorkAreaHeight = L"work-area-height";
        const static wchar_t* MonitorWidth = L"monitor-width";
        const static wchar_t* MonitorHeight = L"monitor-height";
        const static wchar_t* IsSelected = L"is-selected";
        const static wchar_t* ProcessId = L"process-id";
        const static wchar_t* SpanZonesAcrossMonitors = L"span-zones-across-monitors";
        const static wchar_t* Monitors = L"monitors";
    }
}

class EditorParameters
{
public:
    static bool Save(const WorkAreaConfiguration& configuration, OnThreadExecutor& dpiUnawareThread) noexcept;
};
