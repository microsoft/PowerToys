#include "pch.h"
#include "WorkArea.h"

#include <common/logger/call_tracer.h>
#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

#include "FancyZonesData/AppliedLayouts.h"
#include "FancyZonesData/AppZoneHistory.h"
#include "FancyZonesDataTypes.h"
#include "SettingsObserver.h"
#include "ZonesOverlay.h"
#include "trace.h"
#include "on_thread_executor.h"
#include "Settings.h"
#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/VirtualDesktop.h>
#include <FancyZonesLib/WindowUtils.h>

#include <ShellScalingApi.h>
#include <mutex>
#include <fileapi.h>

// disabling warning 4458 - declaration of 'identifier' hides class member
// to avoid warnings from GDI files - can't add winRT directory to external code
// in the Cpp.Build.props
#pragma warning(push)
#pragma warning(disable : 4458)
#include <gdiplus.h>
#pragma warning(pop)

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t ToolWindowClassName[] = L"FancyZones_ZonesOverlay";
}

using namespace FancyZonesUtils;

namespace
{
    // The reason for using this class is the need to call ShowWindow(window, SW_SHOWNORMAL); on each
    // newly created window for it to be displayed properly. The call sometimes has side effects when
    // a fullscreen app is running, and happens when the resolution change event is triggered
    // (e.g. when running some games).
    // This class will serve as a pool of windows for which this call was already done.
    class WindowPool
    {
        std::vector<HWND> m_pool;
        std::mutex m_mutex;

        HWND ExtractWindow()
        {
            std::unique_lock lock(m_mutex);

            if (m_pool.empty())
            {
                return NULL;
            }

            HWND window = m_pool.back();
            m_pool.pop_back();
            return window;
        }

    public:
        HWND NewZonesOverlayWindow(Rect position, HINSTANCE hinstance, WorkArea* owner)
        {
            HWND windowFromPool = ExtractWindow();
            if (windowFromPool == NULL)
            {
                HWND window = CreateWindowExW(WS_EX_TOOLWINDOW, NonLocalizable::ToolWindowClassName, L"", WS_POPUP, position.left(), position.top(), position.width(), position.height(), nullptr, nullptr, hinstance, owner);
                Logger::info("Creating new ZonesOverlay window, hWnd = {}", (void*)window);
                FancyZonesWindowUtils::MakeWindowTransparent(window);

                // According to ShowWindow docs, we must call it with SW_SHOWNORMAL the first time
                ShowWindow(window, SW_SHOWNORMAL);
                ShowWindow(window, SW_HIDE);
                return window;
            }
            else
            {
                Logger::info("Reusing ZonesOverlay window from pool, hWnd = {}", (void*)windowFromPool);
                SetWindowLongPtrW(windowFromPool, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(owner));
                MoveWindow(windowFromPool, position.left(), position.top(), position.width(), position.height(), TRUE);
                return windowFromPool;
            }
        }

        void FreeZonesOverlayWindow(HWND window)
        {
            Logger::info("Freeing ZonesOverlay window into pool, hWnd = {}", (void*)window);
            SetWindowLongPtrW(window, GWLP_USERDATA, 0);
            ShowWindow(window, SW_HIDE);

            std::unique_lock lock(m_mutex);
            m_pool.push_back(window);
        }

        ~WindowPool()
        {
            for (HWND window : m_pool)
            {
                DestroyWindow(window);
            }
        }
    };

    WindowPool windowPool;
}

WorkArea::WorkArea(HINSTANCE hinstance, const FancyZonesDataTypes::WorkAreaId& uniqueId) :
    m_uniqueId(uniqueId)
{
    WNDCLASSEXW wcex{};
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.lpfnWndProc = s_WndProc;
    wcex.hInstance = hinstance;
    wcex.lpszClassName = NonLocalizable::ToolWindowClassName;
    wcex.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    RegisterClassExW(&wcex);
}

WorkArea::~WorkArea()
{
    windowPool.FreeZonesOverlayWindow(m_window);
}

HRESULT WorkArea::MoveSizeEnter(HWND window) noexcept
{
    m_windowMoveSize = window;
    m_highlightZone = {};
    m_initialHighlightZone = {};
    ShowZonesOverlay();
    Trace::WorkArea::MoveOrResizeStarted(m_layout.get(), m_layoutWindows.get());
    return S_OK;
}

HRESULT WorkArea::MoveSizeUpdate(POINT const& ptScreen, bool dragEnabled, bool selectManyZones) noexcept
{
    if (!m_layout)
    {
        return -1;
    }

    bool redraw = false;
    POINT ptClient = ptScreen;
    MapWindowPoints(nullptr, m_window, &ptClient, 1);

    if (dragEnabled)
    {
        auto highlightZone = ZonesFromPoint(ptClient);

        if (selectManyZones)
        {
            if (m_initialHighlightZone.empty())
            {
                // first time
                m_initialHighlightZone = highlightZone;
            }
            else
            {
                highlightZone = m_layout->GetCombinedZoneRange(m_initialHighlightZone, highlightZone);
            }
        }
        else
        {
            m_initialHighlightZone = {};
        }

        redraw = (highlightZone != m_highlightZone);
        m_highlightZone = std::move(highlightZone);
    }
    else if (m_highlightZone.size())
    {
        m_highlightZone = {};
        redraw = true;
    }

    if (redraw && m_zonesOverlay)
    {
        m_zonesOverlay->DrawActiveZoneSet(m_layout->Zones(), m_highlightZone, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
    }

    return S_OK;
}

HRESULT WorkArea::MoveSizeEnd(HWND window) noexcept
{
    if (m_windowMoveSize != window)
    {
        return E_INVALIDARG;
    }

    MoveWindowIntoZoneByIndexSet(window, m_highlightZone);

    Trace::WorkArea::MoveOrResizeEnd(m_layout.get(), m_layoutWindows.get());

    HideZonesOverlay();
    m_windowMoveSize = nullptr;
    return S_OK;
}

void WorkArea::MoveWindowIntoZoneByIndex(HWND window, ZoneIndex index) noexcept
{
    MoveWindowIntoZoneByIndexSet(window, { index });
}

void WorkArea::MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet, bool updatePosition /* = true*/) noexcept
{
    if (!m_layout || !m_layoutWindows || m_layout->Zones().empty() || indexSet.empty())
    {
        return;
    }

    FancyZonesWindowUtils::SaveWindowSizeAndOrigin(window);

    if (updatePosition)
    {
        auto rect = m_layout->GetCombinedZonesRect(indexSet);
        auto adjustedRect = FancyZonesWindowUtils::AdjustRectForSizeWindowToRect(window, rect, m_window);
        FancyZonesWindowUtils::SizeWindowToRect(window, adjustedRect);
    }

    m_layoutWindows->Assign(window, indexSet);
    FancyZonesWindowProperties::StampZoneIndexProperty(window, indexSet);

    SaveWindowProcessToZoneIndex(window);
}

bool WorkArea::MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle) noexcept
{
    if (!m_layout || !m_layoutWindows || m_layout->Zones().empty())
    {
        return false;
    }

    auto zoneIndexes = m_layoutWindows->GetZoneIndexSetFromWindow(window);
    auto numZones = m_layout->Zones().size();

    // The window was not assigned to any zone here
    if (zoneIndexes.size() == 0)
    {
        MoveWindowIntoZoneByIndex(window, vkCode == VK_LEFT ? numZones - 1 : 0);
    }
    else
    {
        ZoneIndex oldId = zoneIndexes[0];

        // We reached the edge
        if ((vkCode == VK_LEFT && oldId == 0) || (vkCode == VK_RIGHT && oldId == static_cast<int64_t>(numZones) - 1))
        {
            if (!cycle)
            {
                return false;
            }

            MoveWindowIntoZoneByIndex(window, vkCode == VK_LEFT ? numZones - 1 : 0);
        }
        else
        {
            // We didn't reach the edge
            if (vkCode == VK_LEFT)
            {
                MoveWindowIntoZoneByIndex(window, oldId - 1);
            }
            else
            {
                MoveWindowIntoZoneByIndex(window, oldId + 1);
            }
        }
    }

    if (!FancyZonesWindowUtils::HasVisibleOwner(window))
    {
        SaveWindowProcessToZoneIndex(window);
    }

    return true;
}

bool WorkArea::MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle) noexcept
{
    if (!m_layout || !m_layoutWindows || m_layout->Zones().empty())
    {
        return false;
    }

    const auto& zones = m_layout->Zones();
    std::vector<bool> usedZoneIndices(zones.size(), false);
    auto windowZones = m_layoutWindows->GetZoneIndexSetFromWindow(window);

    for (ZoneIndex id : windowZones)
    {
        usedZoneIndices[id] = true;
    }

    std::vector<RECT> zoneRects;
    ZoneIndexSet freeZoneIndices;

    for (const auto& [zoneId, zone] : zones)
    {
        if (!usedZoneIndices[zoneId])
        {
            zoneRects.emplace_back(zones.at(zoneId).GetZoneRect());
            freeZoneIndices.emplace_back(zoneId);
        }
    }

    RECT windowRect;
    if (!GetWindowRect(window, &windowRect))
    {
        Logger::error(L"GetWindowRect failed, {}", get_last_error_or_default(GetLastError()));
        return false;
    }

    // Move to coordinates relative to windowZone
    windowRect.top -= m_workAreaRect.top();
    windowRect.bottom -= m_workAreaRect.top();
    windowRect.left -= m_workAreaRect.left();
    windowRect.right -= m_workAreaRect.left();

    auto result = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);
    if (result < zoneRects.size())
    {
        MoveWindowIntoZoneByIndex(window, freeZoneIndices[result]);
        SaveWindowProcessToZoneIndex(window);
        Trace::FancyZones::KeyboardSnapWindowToZone(m_layout.get(), m_layoutWindows.get());
        return true;
    }
    else if (cycle)
    {
        // Try again from the position off the screen in the opposite direction to vkCode
        // Consider all zones as available
        zoneRects.resize(zones.size());
        std::transform(zones.begin(), zones.end(), zoneRects.begin(), [](auto zone) { return zone.second.GetZoneRect(); });
        windowRect = FancyZonesUtils::PrepareRectForCycling(windowRect, RECT(m_workAreaRect.left(), m_workAreaRect.top(), m_workAreaRect.right(), m_workAreaRect.bottom()), vkCode);
        result = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);

        if (result < zoneRects.size())
        {
            MoveWindowIntoZoneByIndex(window, result);
            SaveWindowProcessToZoneIndex(window);
            Trace::FancyZones::KeyboardSnapWindowToZone(m_layout.get(), m_layoutWindows.get());
            return true;
        }
    }

    return false;
}

bool WorkArea::ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode) noexcept
{
    if (!m_layout || !m_layoutWindows || m_layout->Zones().empty())
    {
        return false;
    }

    RECT windowRect;
    if (!GetWindowRect(window, &windowRect))
    {
        Logger::error(L"GetWindowRect failed, {}", get_last_error_or_default(GetLastError()));
        return false;
    }

    const auto& zones = m_layout->Zones();
    auto appliedZones = m_layoutWindows->GetZoneIndexSetFromWindow(window);
    const auto& extendModeData = m_layoutWindows->ExtendWindowData();

    std::vector<bool> usedZoneIndices(zones.size(), false);
    std::vector<RECT> zoneRects;
    ZoneIndexSet freeZoneIndices;

    // If selectManyZones = true for the second time, use the last zone into which we moved
    // instead of the window rect and enable moving to all zones except the old one
    auto finalIndexIt = extendModeData->windowFinalIndex.find(window);
    if (finalIndexIt != extendModeData->windowFinalIndex.end())
    {
        usedZoneIndices[finalIndexIt->second] = true;
        windowRect = zones.at(finalIndexIt->second).GetZoneRect();
    }
    else
    {
        for (ZoneIndex idx : appliedZones)
        {
            usedZoneIndices[idx] = true;
        }
        // Move to coordinates relative to windowZone
        windowRect.top -= m_workAreaRect.top();
        windowRect.bottom -= m_workAreaRect.top();
        windowRect.left -= m_workAreaRect.left();
        windowRect.right -= m_workAreaRect.left();
    }

    for (size_t i = 0; i < zones.size(); i++)
    {
        if (!usedZoneIndices[i])
        {
            zoneRects.emplace_back(zones.at(i).GetZoneRect());
            freeZoneIndices.emplace_back(i);
        }
    }

    auto result = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);
    if (result < zoneRects.size())
    {
        ZoneIndex targetZone = freeZoneIndices[result];
        ZoneIndexSet resultIndexSet;

        // First time with selectManyZones = true for this window?
        if (finalIndexIt == extendModeData->windowFinalIndex.end())
        {
            // Already zoned?
            if (appliedZones.size())
            {
                extendModeData->windowInitialIndexSet[window] = appliedZones;
                extendModeData->windowFinalIndex[window] = targetZone;
                resultIndexSet = m_layout->GetCombinedZoneRange(appliedZones, { targetZone });
            }
            else
            {
                extendModeData->windowInitialIndexSet[window] = { targetZone };
                extendModeData->windowFinalIndex[window] = targetZone;
                resultIndexSet = { targetZone };
            }
        }
        else
        {
            auto deletethis = extendModeData->windowInitialIndexSet[window];
            extendModeData->windowFinalIndex[window] = targetZone;
            resultIndexSet = m_layout->GetCombinedZoneRange(extendModeData->windowInitialIndexSet[window], { targetZone });
        }

        auto rect = m_layout->GetCombinedZonesRect(resultIndexSet);
        auto adjustedRect = FancyZonesWindowUtils::AdjustRectForSizeWindowToRect(window, rect, m_window);
        FancyZonesWindowUtils::SizeWindowToRect(window, adjustedRect);

        m_layoutWindows->Extend(window, resultIndexSet);
        FancyZonesWindowProperties::StampZoneIndexProperty(window, resultIndexSet);

        SaveWindowProcessToZoneIndex(window);

        return true;
    }

    return false;
}

void WorkArea::SaveWindowProcessToZoneIndex(HWND window) noexcept
{
    if (m_layout && m_layoutWindows)
    {
        auto zoneIndexSet = m_layoutWindows->GetZoneIndexSetFromWindow(window);
        if (zoneIndexSet.size())
        {
            auto guidStr = FancyZonesUtils::GuidToString(m_layout->Id());
            if (guidStr.has_value())
            {
                AppZoneHistory::instance().SetAppLastZones(window, m_uniqueId, guidStr.value(), zoneIndexSet);
            }
        }
    }
}

ZoneIndexSet WorkArea::GetWindowZoneIndexes(HWND window) const noexcept
{
    if (m_layout)
    {
        auto guidStr = FancyZonesUtils::GuidToString(m_layout->Id());
        if (guidStr.has_value())
        {
            return AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_uniqueId, guidStr.value());
        }
        else
        {
            Logger::error(L"Failed to convert to string layout GUID on the requested work area");
        }
    }
    else
    {
        Logger::error(L"No layout initialized on the requested work area");
    }

    return {};
}

void WorkArea::ShowZonesOverlay() noexcept
{
    if (m_window && m_layout)
    {
        SetAsTopmostWindow();
        m_zonesOverlay->DrawActiveZoneSet(m_layout->Zones(), m_highlightZone, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
        m_zonesOverlay->Show();
    }
}

void WorkArea::HideZonesOverlay() noexcept
{
    if (m_window)
    {
        m_zonesOverlay->Hide();
        m_keyLast = 0;
        m_windowMoveSize = nullptr;
        m_highlightZone = {};
    }
}

void WorkArea::UpdateActiveZoneSet() noexcept
{
    bool isLayoutAlreadyApplied = AppliedLayouts::instance().IsLayoutApplied(m_uniqueId);
    if (!isLayoutAlreadyApplied)
    {
        AppliedLayouts::instance().ApplyDefaultLayout(m_uniqueId);
    }

    CalculateZoneSet();
    if (m_window && m_layout)
    {
        m_highlightZone.clear();
        m_zonesOverlay->DrawActiveZoneSet(m_layout->Zones(), m_highlightZone, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
    }
}

void WorkArea::CycleWindows(HWND window, bool reverse) noexcept
{
    if (m_layoutWindows)
    {
        m_layoutWindows->CycleWindows(window, reverse);
    }
}

void WorkArea::ClearSelectedZones() noexcept
{
    if (m_highlightZone.size() && m_layout)
    {
        m_highlightZone.clear();
        m_zonesOverlay->DrawActiveZoneSet(m_layout->Zones(), m_highlightZone, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
    }
}

void WorkArea::FlashZones() noexcept
{
    if (m_window && m_layout)
    {
        SetAsTopmostWindow();
        m_zonesOverlay->DrawActiveZoneSet(m_layout->Zones(), {}, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
        m_zonesOverlay->Flash();
    }
}

#pragma region private

bool WorkArea::InitWindow(HINSTANCE hinstance) noexcept
{
    m_window = windowPool.NewZonesOverlayWindow(m_workAreaRect, hinstance, this);
    if (!m_window)
    {
        Logger::error(L"No work area window");
        return false;
    }

    m_zonesOverlay = std::make_unique<ZonesOverlay>(m_window);
    return true;
}

void WorkArea::InitLayout(const FancyZonesDataTypes::WorkAreaId& parentUniqueId) noexcept
{
    Logger::info(L"Initialize layout on {}", m_uniqueId.toString());

    bool isLayoutAlreadyApplied = AppliedLayouts::instance().IsLayoutApplied(m_uniqueId);
    if (!isLayoutAlreadyApplied)
    {
        if (parentUniqueId.virtualDesktopId != GUID_NULL)
        {
            AppliedLayouts::instance().CloneLayout(parentUniqueId, m_uniqueId);
        }
        else
        {
            AppliedLayouts::instance().ApplyDefaultLayout(m_uniqueId);
        }
    }

    CalculateZoneSet();
}

void WorkArea::CalculateZoneSet() noexcept
{
    const auto appliedLayout = AppliedLayouts::instance().GetDeviceLayout(m_uniqueId);
    if (!appliedLayout.has_value())
    {
        Logger::error(L"Layout wasn't applied. Can't init layout on work area {}x{}", m_workAreaRect.width(), m_workAreaRect.height());
        return;
    }

    m_layout = std::make_unique<Layout>(appliedLayout.value());
    m_layout->Init(m_workAreaRect, m_monitor);

    if (!m_layoutWindows)
    {
        m_layoutWindows = std::make_unique<LayoutAssignedWindows>();
    }
}

LRESULT WorkArea::WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    switch (message)
    {
    case WM_NCDESTROY:
    {
        ::DefWindowProc(m_window, message, wparam, lparam);
        SetWindowLongPtr(m_window, GWLP_USERDATA, 0);
    }
    break;

    case WM_ERASEBKGND:
        return 1;

    default:
    {
        return DefWindowProc(m_window, message, wparam, lparam);
    }
    }
    return 0;
}

ZoneIndexSet WorkArea::ZonesFromPoint(POINT pt) noexcept
{
    if (m_layout)
    {
        return m_layout->ZonesFromPoint(pt);
    }

    return {};
}

void WorkArea::SetAsTopmostWindow() noexcept
{
    if (!m_window)
    {
        return;
    }

    UINT flags = SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE;

    HWND windowInsertAfter = m_windowMoveSize;
    if (windowInsertAfter == nullptr)
    {
        windowInsertAfter = HWND_TOPMOST;
    }

    SetWindowPos(m_window, windowInsertAfter, 0, 0, 0, 0, flags);
}

void WorkArea::LogInitializationError()
{
    Logger::error(L"Unable to get monitor info, {}", get_last_error_or_default(GetLastError()));
}

#pragma endregion

LRESULT CALLBACK WorkArea::s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    auto thisRef = reinterpret_cast<WorkArea*>(GetWindowLongPtr(window, GWLP_USERDATA));
    if ((thisRef == nullptr) && (message == WM_CREATE))
    {
        auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
        thisRef = reinterpret_cast<WorkArea*>(createStruct->lpCreateParams);
        SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
    }

    return (thisRef != nullptr) ? thisRef->WndProc(message, wparam, lparam) :
                                  DefWindowProc(window, message, wparam, lparam);
}
