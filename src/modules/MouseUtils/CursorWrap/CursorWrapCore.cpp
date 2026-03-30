// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CursorWrapCore.h"
#include "../../../common/logger/logger.h"
#include <sstream>
#include <iomanip>
#include <ctime>

CursorWrapCore::CursorWrapCore()
{
}

#ifdef _DEBUG
std::wstring CursorWrapCore::GenerateTopologyJSON() const
{
    std::wostringstream json;

    // Get current time
    auto now = std::time(nullptr);
    std::tm tm{};
    localtime_s(&tm, &now);

    wchar_t computerName[MAX_COMPUTERNAME_LENGTH + 1] = {0};
    DWORD size = MAX_COMPUTERNAME_LENGTH + 1;
    GetComputerNameW(computerName, &size);

    wchar_t userName[256] = {0};
    size = 256;
    GetUserNameW(userName, &size);

    json << L"{\n";
    json << L"  \"captured_at\": \"" << std::put_time(&tm, L"%Y-%m-%dT%H:%M:%S%z") << L"\",\n";
    json << L"  \"computer_name\": \"" << computerName << L"\",\n";
    json << L"  \"user_name\": \"" << userName << L"\",\n";
    json << L"  \"monitor_count\": " << m_monitors.size() << L",\n";
    json << L"  \"monitors\": [\n";

    for (size_t i = 0; i < m_monitors.size(); ++i)
    {
        const auto& monitor = m_monitors[i];

        // Get DPI for this monitor
        UINT dpiX = 96, dpiY = 96;
        POINT center = {
            (monitor.rect.left + monitor.rect.right) / 2,
            (monitor.rect.top + monitor.rect.bottom) / 2
        };
        HMONITOR hMon = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
        if (hMon)
        {
            // Try GetDpiForMonitor (requires linking Shcore.lib)
            using GetDpiForMonitorFunc = HRESULT (WINAPI *)(HMONITOR, int, UINT*, UINT*);
            HMODULE shcore = LoadLibraryW(L"Shcore.dll");
            if (shcore)
            {
                auto getDpi = reinterpret_cast<GetDpiForMonitorFunc>(GetProcAddress(shcore, "GetDpiForMonitor"));
                if (getDpi)
                {
                    getDpi(hMon, 0, &dpiX, &dpiY); // MDT_EFFECTIVE_DPI = 0
                }
                FreeLibrary(shcore);
            }
        }

        int scalingPercent = static_cast<int>((dpiX / 96.0) * 100);

        json << L"    {\n";
        json << L"      \"left\": " << monitor.rect.left << L",\n";
        json << L"      \"top\": " << monitor.rect.top << L",\n";
        json << L"      \"right\": " << monitor.rect.right << L",\n";
        json << L"      \"bottom\": " << monitor.rect.bottom << L",\n";
        json << L"      \"width\": " << (monitor.rect.right - monitor.rect.left) << L",\n";
        json << L"      \"height\": " << (monitor.rect.bottom - monitor.rect.top) << L",\n";
        json << L"      \"dpi\": " << dpiX << L",\n";
        json << L"      \"scaling_percent\": " << scalingPercent << L",\n";
        json << L"      \"primary\": " << (monitor.isPrimary ? L"true" : L"false") << L",\n";
        json << L"      \"monitor_id\": " << monitor.monitorId << L"\n";
        json << L"    }";
        if (i < m_monitors.size() - 1)
        {
            json << L",";
        }
        json << L"\n";
    }

    json << L"  ]\n";
    json << L"}";

    return json.str();
}
#endif

void CursorWrapCore::UpdateMonitorInfo()
{
    size_t previousMonitorCount = m_monitors.size();
    Logger::info(L"======= UPDATE MONITOR INFO START =======");
    Logger::info(L"Previous monitor count: {}", previousMonitorCount);

    m_monitors.clear();

    EnumDisplayMonitors(nullptr, nullptr, [](HMONITOR hMonitor, HDC, LPRECT, LPARAM lParam) -> BOOL {
        auto* self = reinterpret_cast<CursorWrapCore*>(lParam);

        MONITORINFO mi{};
        mi.cbSize = sizeof(MONITORINFO);
        if (GetMonitorInfo(hMonitor, &mi))
        {
            MonitorInfo info{};
            info.hMonitor = hMonitor; // Store handle for direct comparison later
            info.rect = mi.rcMonitor;
            info.isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
            info.monitorId = static_cast<int>(self->m_monitors.size());
            self->m_monitors.push_back(info);
            
            Logger::info(L"Enumerated monitor {}: hMonitor={}, rect=({},{},{},{}), primary={}",
                info.monitorId, reinterpret_cast<uintptr_t>(hMonitor),
                mi.rcMonitor.left, mi.rcMonitor.top, mi.rcMonitor.right, mi.rcMonitor.bottom,
                info.isPrimary ? L"yes" : L"no");
        }

        return TRUE;
    }, reinterpret_cast<LPARAM>(this));

    if (previousMonitorCount != m_monitors.size())
    {
        Logger::info(L"*** MONITOR CONFIGURATION CHANGED: {} -> {} monitors ***", 
            previousMonitorCount, m_monitors.size());
    }

    m_topology.Initialize(m_monitors);

    // Log monitor configuration summary
    Logger::info(L"Monitor configuration updated: {} monitor(s)", m_monitors.size());
    for (size_t i = 0; i < m_monitors.size(); ++i)
    {
        const auto& m = m_monitors[i];
        int width = m.rect.right - m.rect.left;
        int height = m.rect.bottom - m.rect.top;
        Logger::info(L"  Monitor {}: {}x{} at ({}, {}){}",
            i, width, height, m.rect.left, m.rect.top,
            m.isPrimary ? L" [PRIMARY]" : L"");
    }
    Logger::info(L"  Detected {} outer edges for cursor wrapping", m_topology.GetOuterEdges().size());

    // Detect and log monitor gaps
    auto gaps = m_topology.DetectMonitorGaps();
    if (!gaps.empty())
    {
        Logger::warn(L"Monitor configuration has coordinate gaps that may prevent wrapping:");
        for (const auto& gap : gaps)
        {
            Logger::warn(L"  Gap between Monitor {} and Monitor {}: {}px horizontal gap, {}px vertical overlap",
                gap.monitor1Index, gap.monitor2Index, gap.horizontalGap, gap.verticalOverlap);
        }
        Logger::warn(L"  If monitors appear snapped in Display Settings but show gaps here:");
        Logger::warn(L"  1. Try dragging monitors apart and snapping them back together");
        Logger::warn(L"  2. Update your GPU drivers");
    }

    Logger::info(L"======= UPDATE MONITOR INFO END =======");
}

void CursorWrapCore::ResetWrapState()
{
    m_hasPreviousPosition = false;
    m_hasLastWrapDestination = false;
    m_previousPosition = { LONG_MIN, LONG_MIN };
    m_lastWrapDestination = { LONG_MIN, LONG_MIN };
}

CursorDirection CursorWrapCore::CalculateDirection(const POINT& currentPos) const
{
    CursorDirection dir = { 0, 0 };
    if (m_hasPreviousPosition)
    {
        dir.dx = currentPos.x - m_previousPosition.x;
        dir.dy = currentPos.y - m_previousPosition.y;
    }
    return dir;
}

bool CursorWrapCore::IsWithinWrapThreshold(const POINT& currentPos) const
{
    if (!m_hasLastWrapDestination)
    {
        return false;
    }
    
    int dx = currentPos.x - m_lastWrapDestination.x;
    int dy = currentPos.y - m_lastWrapDestination.y;
    int distanceSquared = dx * dx + dy * dy;
    
    return distanceSquared <= (WRAP_DISTANCE_THRESHOLD * WRAP_DISTANCE_THRESHOLD);
}

POINT CursorWrapCore::HandleMouseMove(const POINT& currentPos, bool disableWrapDuringDrag, int wrapMode, bool disableOnSingleMonitor)
{
    // Check if wrapping should be disabled on single monitor
    if (disableOnSingleMonitor && m_monitors.size() <= 1)
    {
#ifdef _DEBUG
        static bool loggedOnce = false;
        if (!loggedOnce)
        {
            OutputDebugStringW(L"[CursorWrap] Single monitor detected - cursor wrapping disabled\n");
            loggedOnce = true;
        }
#endif
        m_previousPosition = currentPos;
        m_hasPreviousPosition = true;
        return currentPos;
    }

    // Check if wrapping should be disabled during drag
    if (disableWrapDuringDrag && (GetAsyncKeyState(VK_LBUTTON) & 0x8000))
    {
#ifdef _DEBUG
        OutputDebugStringW(L"[CursorWrap] [DRAG] Left mouse button down - skipping wrap\n");
#endif
        m_previousPosition = currentPos;
        m_hasPreviousPosition = true;
        return currentPos;
    }

    // Check distance threshold to prevent rapid oscillation
    if (IsWithinWrapThreshold(currentPos))
    {
#ifdef _DEBUG
        OutputDebugStringW(L"[CursorWrap] [THRESHOLD] Cursor within wrap threshold - skipping wrap\n");
#endif
        m_previousPosition = currentPos;
        m_hasPreviousPosition = true;
        return currentPos;
    }
    
    // Clear wrap destination threshold once cursor moves away
    if (m_hasLastWrapDestination && !IsWithinWrapThreshold(currentPos))
    {
        m_hasLastWrapDestination = false;
    }

    // Calculate cursor movement direction
    CursorDirection direction = CalculateDirection(currentPos);

    // Convert int wrapMode to WrapMode enum
    WrapMode mode = static_cast<WrapMode>(wrapMode);

#ifdef _DEBUG
    {
        std::wostringstream oss;
        oss << L"[CursorWrap] [MOVE] Cursor at (" << currentPos.x << L", " << currentPos.y << L")";
        oss << L" direction=(" << direction.dx << L", " << direction.dy << L")";

        // Get current monitor and identify which one
        HMONITOR currentMonitor = MonitorFromPoint(currentPos, MONITOR_DEFAULTTONEAREST);
        RECT monitorRect;
        if (m_topology.GetMonitorRect(currentMonitor, monitorRect))
        {
            // Find monitor ID
            int monitorId = -1;
            for (const auto& monitor : m_monitors)
            {
                if (monitor.rect.left == monitorRect.left &&
                    monitor.rect.top == monitorRect.top &&
                    monitor.rect.right == monitorRect.right &&
                    monitor.rect.bottom == monitorRect.bottom)
                {
                    monitorId = monitor.monitorId;
                    break;
                }
            }
            oss << L" on Monitor " << monitorId << L" [" << monitorRect.left << L".." << monitorRect.right
                << L", " << monitorRect.top << L".." << monitorRect.bottom << L"]";
        }
        else
        {
            oss << L" (beyond monitor bounds)";
        }
        oss << L"\n";
        OutputDebugStringW(oss.str().c_str());
    }
#endif

    // Get current monitor
    HMONITOR currentMonitor = MonitorFromPoint(currentPos, MONITOR_DEFAULTTONEAREST);

    // Check if cursor is on an outer edge (filtered by wrap mode and direction)
    EdgeType edgeType;
    if (!m_topology.IsOnOuterEdge(currentMonitor, currentPos, edgeType, mode, &direction))
    {
#ifdef _DEBUG
        static bool lastWasNotOuter = false;
        if (!lastWasNotOuter)
        {
            OutputDebugStringW(L"[CursorWrap] [MOVE] Not on outer edge - no wrapping\n");
            lastWasNotOuter = true;
        }
#endif
        m_previousPosition = currentPos;
        m_hasPreviousPosition = true;
        return currentPos; // Not on an outer edge
    }

#ifdef _DEBUG
    {
        const wchar_t* edgeStr = L"Unknown";
        switch (edgeType)
        {
        case EdgeType::Left: edgeStr = L"Left"; break;
        case EdgeType::Right: edgeStr = L"Right"; break;
        case EdgeType::Top: edgeStr = L"Top"; break;
        case EdgeType::Bottom: edgeStr = L"Bottom"; break;
        }
        std::wostringstream oss;
        oss << L"[CursorWrap] [EDGE] Detected outer " << edgeStr << L" edge at (" << currentPos.x << L", " << currentPos.y << L")\n";
        OutputDebugStringW(oss.str().c_str());
    }
#endif

    // Calculate wrap destination
    POINT newPos = m_topology.GetWrapDestination(currentMonitor, currentPos, edgeType);

#ifdef _DEBUG
    if (newPos.x != currentPos.x || newPos.y != currentPos.y)
    {
        std::wostringstream oss;
        oss << L"[CursorWrap] [WRAP] Position change: (" << currentPos.x << L", " << currentPos.y
            << L") -> (" << newPos.x << L", " << newPos.y << L")\n";
        oss << L"[CursorWrap] [WRAP] Delta: (" << (newPos.x - currentPos.x) << L", " << (newPos.y - currentPos.y) << L")\n";
        OutputDebugStringW(oss.str().c_str());
    }
    else
    {
        OutputDebugStringW(L"[CursorWrap] [WRAP] No position change (same-monitor wrap?)\n");
    }
#endif

    // Update tracking state
    m_previousPosition = currentPos;
    m_hasPreviousPosition = true;
    
    // Store wrap destination for threshold checking
    if (newPos.x != currentPos.x || newPos.y != currentPos.y)
    {
        m_lastWrapDestination = newPos;
        m_hasLastWrapDestination = true;
    }

    return newPos;
}
