#include "pch.h"
#include "EditorParameters.h"

#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/MonitorUtils.h>
#include <FancyZonesLib/on_thread_executor.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/VirtualDesktop.h>
#include <FancyZonesLib/WorkArea.h>
#include <FancyZonesLib/util.h>

#include <common/Display/dpi_aware.h>
#include <common/logger/logger.h>


// Non-localizable strings
namespace NonLocalizable
{
    const wchar_t FancyZonesEditorParametersFile[] = L"editor-parameters.json";
}

namespace JsonUtils
{
    struct MonitorInfo
    {
        std::wstring monitorName;
        std::wstring monitorInstanceId;
        std::wstring monitorSerialNumber;
        int monitorNumber{};
        std::wstring virtualDesktop;
        int dpi{};
        int top{};
        int left{};
        int workAreaWidth{};
        int workAreaHeight{};
        int monitorWidth{};
        int monitorHeight{};
        bool isSelected = false;

        bool FillFromWorkArea(const WorkArea* const workArea)
        {
            const auto virtualDesktopIdStr = FancyZonesUtils::GuidToString(workArea->UniqueId().virtualDesktopId);
            if (!virtualDesktopIdStr)
            {
                return false;
            }

            const auto& monitorId = workArea->UniqueId().monitorId;
            monitorName = monitorId.deviceId.id;
            monitorInstanceId = monitorId.deviceId.instanceId;
            monitorNumber = monitorId.deviceId.number;
            monitorSerialNumber = monitorId.serialNumber;

            const auto& rect = workArea->GetWorkAreaRect();
            top = rect.top();
            left = rect.left();
            workAreaWidth = rect.width();
            workAreaHeight = rect.height();

            virtualDesktop = virtualDesktopIdStr.value();

            return true;
        }

        static json::JsonObject ToJson(const MonitorInfo& monitor)
        {
            json::JsonObject result{};

            result.SetNamedValue(NonLocalizable::EditorParametersIds::MonitorNameId, json::value(monitor.monitorName));
            result.SetNamedValue(NonLocalizable::EditorParametersIds::MonitorInstanceId, json::value(monitor.monitorInstanceId));
            result.SetNamedValue(NonLocalizable::EditorParametersIds::MonitorSerialNumberId, json::value(monitor.monitorSerialNumber));
            result.SetNamedValue(NonLocalizable::EditorParametersIds::MonitorNumberId, json::value(monitor.monitorNumber));
            result.SetNamedValue(NonLocalizable::EditorParametersIds::VirtualDesktopId, json::value(monitor.virtualDesktop));
            result.SetNamedValue(NonLocalizable::EditorParametersIds::Dpi, json::value(monitor.dpi));
            result.SetNamedValue(NonLocalizable::EditorParametersIds::TopCoordinate, json::value(monitor.top));
            result.SetNamedValue(NonLocalizable::EditorParametersIds::LeftCoordinate, json::value(monitor.left));
            result.SetNamedValue(NonLocalizable::EditorParametersIds::WorkAreaWidth, json::value(monitor.workAreaWidth));
            result.SetNamedValue(NonLocalizable::EditorParametersIds::WorkAreaHeight, json::value(monitor.workAreaHeight));
            result.SetNamedValue(NonLocalizable::EditorParametersIds::MonitorWidth, json::value(monitor.monitorWidth));
            result.SetNamedValue(NonLocalizable::EditorParametersIds::MonitorHeight, json::value(monitor.monitorHeight));
            result.SetNamedValue(NonLocalizable::EditorParametersIds::IsSelected, json::value(monitor.isSelected));

            return result;
        }
    };

    struct EditorArgs
    {
        DWORD processId{};
        bool spanZonesAcrossMonitors{};
        std::vector<MonitorInfo> monitors;

        static json::JsonObject ToJson(const EditorArgs& args)
        {
            json::JsonObject result{};

            result.SetNamedValue(NonLocalizable::EditorParametersIds::ProcessId, json::value(args.processId));
            result.SetNamedValue(NonLocalizable::EditorParametersIds::SpanZonesAcrossMonitors, json::value(args.spanZonesAcrossMonitors));

            json::JsonArray monitors;
            for (const auto& monitor : args.monitors)
            {
                monitors.Append(MonitorInfo::ToJson(monitor));
            }

            result.SetNamedValue(NonLocalizable::EditorParametersIds::Monitors, monitors);

            return result;
        }
    };
}

bool EditorParameters::Save(const WorkAreaConfiguration& configuration, OnThreadExecutor& dpiUnawareThread) noexcept
{
    const bool spanZonesAcrossMonitors = FancyZonesSettings::settings().spanZonesAcrossMonitors;

    JsonUtils::EditorArgs argsJson;
    argsJson.processId = GetCurrentProcessId();
    argsJson.spanZonesAcrossMonitors = spanZonesAcrossMonitors;

    if (spanZonesAcrossMonitors)
    {
        const auto& workArea = configuration.GetWorkArea(nullptr);
        if (!workArea)
        {
            return false;
        }

        JsonUtils::MonitorInfo monitorJson;
        if (!monitorJson.FillFromWorkArea(workArea))
        {
            return false;
        }

        RECT combinedWorkArea;
        dpiUnawareThread.submit(OnThreadExecutor::task_t{
            [&]() {
                combinedWorkArea = FancyZonesUtils::GetAllMonitorsCombinedRect<&MONITORINFOEX::rcWork>();
        } }).wait();
        RECT combinedMonitorArea = FancyZonesUtils::GetAllMonitorsCombinedRect<&MONITORINFOEX::rcMonitor>();

        // use dpi-unaware values
        monitorJson.top = combinedWorkArea.top;
        monitorJson.left = combinedWorkArea.left;
        monitorJson.workAreaWidth = combinedWorkArea.right - combinedWorkArea.left;
        monitorJson.workAreaHeight = combinedWorkArea.bottom - combinedWorkArea.top;

        monitorJson.monitorWidth = combinedMonitorArea.right - combinedMonitorArea.left;
        monitorJson.monitorHeight = combinedMonitorArea.bottom - combinedMonitorArea.top;
        monitorJson.isSelected = true;
        monitorJson.dpi = 0; // unused

        argsJson.monitors.emplace_back(std::move(monitorJson));
    }
    else
    {
        HMONITOR targetMonitor{};
        if (FancyZonesSettings::settings().use_cursorpos_editor_startupscreen)
        {
            POINT currentCursorPos{};
            GetCursorPos(&currentCursorPos);
            targetMonitor = MonitorFromPoint(currentCursorPos, MONITOR_DEFAULTTOPRIMARY);
        }
        else
        {
            targetMonitor = MonitorFromWindow(GetForegroundWindow(), MONITOR_DEFAULTTOPRIMARY);
        }

        if (!targetMonitor)
        {
            Logger::error("No target monitor to open editor");
            return false;
        }

        const auto& config = configuration.GetAllWorkAreas();
        for (auto& [monitor, workArea] : config)
        {
            JsonUtils::MonitorInfo monitorJson;
            if (!monitorJson.FillFromWorkArea(workArea.get()))
            {
                continue;
            }

            monitorJson.isSelected = monitor == targetMonitor; /* Is monitor selected for the main editor window opening */

            UINT dpi = 0;
            if (DPIAware::GetScreenDPIForMonitor(monitor, dpi) != S_OK)
            {
                continue;
            }

            monitorJson.dpi = dpi;
            
            // Get DPI-unaware values for dimensions (virtual coordinates for WPF sizing)
            MONITORINFOEX monitorInfoUnaware{};
            dpiUnawareThread.submit(OnThreadExecutor::task_t{ [&] {
                monitorInfoUnaware.cbSize = sizeof(monitorInfoUnaware);
                GetMonitorInfo(monitor, &monitorInfoUnaware);
            } }).wait();

            // Dimensions in virtual coordinates (from DPI-unaware thread)
            monitorJson.monitorWidth = monitorInfoUnaware.rcMonitor.right - monitorInfoUnaware.rcMonitor.left;
            monitorJson.monitorHeight = monitorInfoUnaware.rcMonitor.bottom - monitorInfoUnaware.rcMonitor.top;
            monitorJson.workAreaWidth = monitorInfoUnaware.rcWork.right - monitorInfoUnaware.rcWork.left;
            monitorJson.workAreaHeight = monitorInfoUnaware.rcWork.bottom - monitorInfoUnaware.rcWork.top;

            // Position in virtual coordinates (matched by DPI-unaware context in WPF editor)
            monitorJson.left = monitorInfoUnaware.rcWork.left;
            monitorJson.top = monitorInfoUnaware.rcWork.top;

            argsJson.monitors.emplace_back(std::move(monitorJson));
        }
    }
    
    std::wstring folderPath = PTSettingsHelper::get_module_save_folder_location(NonLocalizable::ModuleKey);
    std::wstring fileName = folderPath + L"\\" + std::wstring(NonLocalizable::FancyZonesEditorParametersFile);
    json::to_file(fileName, JsonUtils::EditorArgs::ToJson(argsJson));

    return true;
}
