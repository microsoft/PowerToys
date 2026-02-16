#include "ReportMonitorInfo.h"
#include <Windows.h>
#include <ShellScalingApi.h>
#include <filesystem>
#include <vector>
#include <cmath>

using namespace std;

namespace
{
    struct MonitorData
    {
        LONG left{};
        LONG top{};
        LONG right{};
        LONG bottom{};
        LONG width{};
        LONG height{};
        UINT dpi{};
        int scalingPercent{};
        bool primary{};
        wstring deviceName;
        wstring deviceId;
        wstring deviceString;
        bool active{};
        bool mirroring{};
    };

    struct GapData
    {
        size_t monitorA{};
        size_t monitorB{};
        LONG gapPx{};
        LONG verticalOverlapPx{};
    };

    UINT GetMonitorDpi(HMONITOR hMonitor)
    {
        UINT dpiX = 0, dpiY = 0;
        if (SUCCEEDED(GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, &dpiX, &dpiY)) && dpiX > 0)
        {
            return dpiX;
        }
        return 96;
    }

    vector<MonitorData> CollectMonitorData()
    {
        struct EnumState
        {
            vector<MonitorData>* monitors = nullptr;
        };

        vector<MonitorData> monitors;
        EnumState state;
        state.monitors = &monitors;

        auto callback = [](HMONITOR hMonitor, HDC, RECT*, LPARAM param) -> BOOL {
            auto& monitors = *(reinterpret_cast<EnumState*>(param))->monitors;
            MONITORINFOEXW mi{};
            mi.cbSize = sizeof(mi);

            if (!GetMonitorInfoW(hMonitor, &mi))
            {
                return TRUE;
            }

            MonitorData data;
            data.left = mi.rcMonitor.left;
            data.top = mi.rcMonitor.top;
            data.right = mi.rcMonitor.right;
            data.bottom = mi.rcMonitor.bottom;
            data.width = mi.rcMonitor.right - mi.rcMonitor.left;
            data.height = mi.rcMonitor.bottom - mi.rcMonitor.top;
            data.primary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
            data.deviceName = mi.szDevice;
            data.dpi = GetMonitorDpi(hMonitor);
            data.scalingPercent = static_cast<int>(round((data.dpi / 96.0) * 100.0));

            DISPLAY_DEVICE displayDevice = { sizeof(displayDevice) };
            if (EnumDisplayDevicesW(mi.szDevice, 0, &displayDevice, EDD_GET_DEVICE_INTERFACE_NAME))
            {
                data.active = (displayDevice.StateFlags & DISPLAY_DEVICE_ACTIVE) != 0;
                data.mirroring = (displayDevice.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER) != 0;
                data.deviceId = displayDevice.DeviceID;
                data.deviceString = displayDevice.DeviceString;
            }

            monitors.push_back(std::move(data));
            return TRUE;
        };

        EnumDisplayMonitors(nullptr, nullptr, callback, reinterpret_cast<LPARAM>(&state));
        return monitors;
    }

    vector<GapData> AnalyzeGaps(const vector<MonitorData>& monitors)
    {
        vector<GapData> gaps;
        for (size_t i = 0; i < monitors.size(); i++)
        {
            for (size_t j = i + 1; j < monitors.size(); j++)
            {
                const auto& m1 = monitors[i];
                const auto& m2 = monitors[j];

                LONG hGap = min(abs(m1.right - m2.left), abs(m2.right - m1.left));
                LONG vOverlapStart = max(m1.top, m2.top);
                LONG vOverlapEnd = min(m1.bottom, m2.bottom);
                LONG vOverlap = vOverlapEnd - vOverlapStart;

                if (hGap > 50 && vOverlap > 0)
                {
                    gaps.push_back({ i, j, hGap, vOverlap });
                }
            }
        }
        return gaps;
    }

    // Escape a wide string for JSON output
    wstring JsonEscape(const wstring& input)
    {
        wstring result;
        result.reserve(input.size());
        for (auto ch : input)
        {
            switch (ch)
            {
            case L'\\': result += L"\\\\"; break;
            case L'"': result += L"\\\""; break;
            case L'\n': result += L"\\n"; break;
            case L'\r': result += L"\\r"; break;
            case L'\t': result += L"\\t"; break;
            default: result += ch; break;
            }
        }
        return result;
    }

    void WriteJsonReport(const filesystem::path& outputPath, const vector<MonitorData>& monitors, const vector<GapData>& gaps)
    {
        wofstream os(outputPath);
        os << L"{\n";
        os << L"  \"monitor_count\": " << monitors.size() << L",\n";
        os << L"  \"monitors\": [\n";

        for (size_t i = 0; i < monitors.size(); i++)
        {
            const auto& m = monitors[i];
            os << L"    {\n";
            os << L"      \"left\": " << m.left << L",\n";
            os << L"      \"top\": " << m.top << L",\n";
            os << L"      \"right\": " << m.right << L",\n";
            os << L"      \"bottom\": " << m.bottom << L",\n";
            os << L"      \"width\": " << m.width << L",\n";
            os << L"      \"height\": " << m.height << L",\n";
            os << L"      \"dpi\": " << m.dpi << L",\n";
            os << L"      \"scaling_percent\": " << m.scalingPercent << L",\n";
            os << L"      \"primary\": " << (m.primary ? L"true" : L"false") << L",\n";
            os << L"      \"device_name\": \"" << JsonEscape(m.deviceName) << L"\",\n";
            os << L"      \"device_id\": \"" << JsonEscape(m.deviceId) << L"\",\n";
            os << L"      \"device_string\": \"" << JsonEscape(m.deviceString) << L"\",\n";
            os << L"      \"active\": " << (m.active ? L"true" : L"false") << L",\n";
            os << L"      \"mirroring\": " << (m.mirroring ? L"true" : L"false") << L"\n";
            os << L"    }" << (i + 1 < monitors.size() ? L"," : L"") << L"\n";
        }

        os << L"  ],\n";
        os << L"  \"coordinate_gaps\": [\n";

        for (size_t i = 0; i < gaps.size(); i++)
        {
            const auto& g = gaps[i];
            os << L"    {\n";
            os << L"      \"monitor_a\": " << g.monitorA << L",\n";
            os << L"      \"monitor_b\": " << g.monitorB << L",\n";
            os << L"      \"gap_px\": " << g.gapPx << L",\n";
            os << L"      \"vertical_overlap_px\": " << g.verticalOverlapPx << L"\n";
            os << L"    }" << (i + 1 < gaps.size() ? L"," : L"") << L"\n";
        }

        os << L"  ]\n";
        os << L"}\n";
    }
}

void ReportMonitorInfo(const filesystem::path& tmpDir)
{
    auto reportPath = tmpDir / L"monitor-report-info.json";

    try
    {
        auto monitors = CollectMonitorData();
        auto gaps = AnalyzeGaps(monitors);
        WriteJsonReport(reportPath, monitors, gaps);
    }
    catch (std::exception& ex)
    {
        printf("Failed to report monitor info. %s\n", ex.what());
    }
    catch (...)
    {
        printf("Failed to report monitor info\n");
    }
}
