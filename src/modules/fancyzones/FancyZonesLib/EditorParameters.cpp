#include "pch.h"
#include "EditorParameters.h"

#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/MonitorUtils.h>
#include <FancyZonesLib/on_thread_executor.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/VirtualDesktop.h>
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

bool EditorParameters::Save() noexcept
{
    const auto virtualDesktopIdStr = FancyZonesUtils::GuidToString(VirtualDesktop::instance().GetCurrentVirtualDesktopId());
    if (!virtualDesktopIdStr)
    {
        Logger::error(L"Save editor params: invalid virtual desktop id");
        return false;
    }

    OnThreadExecutor dpiUnawareThread;
    dpiUnawareThread.submit(OnThreadExecutor::task_t{ [] {
        SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_UNAWARE);
        SetThreadDpiHostingBehavior(DPI_HOSTING_BEHAVIOR_MIXED);
    } }).wait();

    const bool spanZonesAcrossMonitors = FancyZonesSettings::settings().spanZonesAcrossMonitors;

    JsonUtils::EditorArgs argsJson;
    argsJson.processId = GetCurrentProcessId();
    argsJson.spanZonesAcrossMonitors = spanZonesAcrossMonitors;

    if (spanZonesAcrossMonitors)
    {
        RECT combinedWorkArea;
        dpiUnawareThread.submit(OnThreadExecutor::task_t{ [&]() {
            combinedWorkArea = FancyZonesUtils::GetAllMonitorsCombinedRect<&MONITORINFOEX::rcWork>();
            
        } }).wait();

        RECT combinedMonitorArea = FancyZonesUtils::GetAllMonitorsCombinedRect<&MONITORINFOEX::rcMonitor>();

        JsonUtils::MonitorInfo monitorJson;
        monitorJson.monitorName = ZonedWindowProperties::MultiMonitorName;
        monitorJson.monitorInstanceId = ZonedWindowProperties::MultiMonitorInstance;
        monitorJson.monitorNumber = 0;
        monitorJson.virtualDesktop = virtualDesktopIdStr.value();
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
        auto monitors = MonitorUtils::IdentifyMonitors();

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

        for (auto& monitorData : monitors)
        {
            HMONITOR monitor = monitorData.monitor;

            MONITORINFOEX monitorInfo{};
            dpiUnawareThread.submit(OnThreadExecutor::task_t{ [&] {
                monitorInfo.cbSize = sizeof(monitorInfo);
                if (!GetMonitorInfo(monitor, &monitorInfo))
                {
                    return;
                }
            } }).wait();

            JsonUtils::MonitorInfo monitorJson;

            if (monitor == targetMonitor)
            {
                monitorJson.isSelected = true; /* Is monitor selected for the main editor window opening */
            }

            monitorJson.monitorName = monitorData.deviceId.id;
            monitorJson.monitorInstanceId = monitorData.deviceId.instanceId;
            monitorJson.monitorNumber = monitorData.deviceId.number;
            monitorJson.monitorSerialNumber = monitorData.serialNumber;
            monitorJson.virtualDesktop = virtualDesktopIdStr.value();

            UINT dpi = 0;
            if (DPIAware::GetScreenDPIForMonitor(monitor, dpi) != S_OK)
            {
                continue;
            }

            monitorJson.dpi = dpi;
            monitorJson.top = monitorInfo.rcWork.top;
            monitorJson.left = monitorInfo.rcWork.left;
            monitorJson.workAreaWidth = monitorInfo.rcWork.right - monitorInfo.rcWork.left;
            monitorJson.workAreaHeight = monitorInfo.rcWork.bottom - monitorInfo.rcWork.top;

            float width = static_cast<float>(monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left);
            float height = static_cast<float>(monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top);
            DPIAware::Convert(monitor, width, height);

            monitorJson.monitorWidth = static_cast<int>(std::roundf(width));
            monitorJson.monitorHeight = static_cast<int>(std::roundf(height));

            argsJson.monitors.emplace_back(std::move(monitorJson));
        }
    }
    
    std::wstring folderPath = PTSettingsHelper::get_module_save_folder_location(NonLocalizable::ModuleKey);
    std::wstring fileName = folderPath + L"\\" + std::wstring(NonLocalizable::FancyZonesEditorParametersFile);
    json::to_file(fileName, JsonUtils::EditorArgs::ToJson(argsJson));

    return true;
}
