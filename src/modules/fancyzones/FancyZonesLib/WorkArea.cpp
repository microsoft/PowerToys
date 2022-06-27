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

#include <gdiplus.h>

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

WorkArea::WorkArea(HINSTANCE hinstance)
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

bool WorkArea::Init(HINSTANCE hinstance, const FancyZonesDataTypes::WorkAreaId& uniqueId, const FancyZonesDataTypes::WorkAreaId& parentUniqueId)
{
    m_uniqueId = uniqueId;
    InitializeZoneSets(parentUniqueId);

    m_window = windowPool.NewZonesOverlayWindow(m_workAreaRect, hinstance, this);

    if (!m_window)
    {
        Logger::error(L"No work area window");
        return false;
    }

    m_zonesOverlay = std::make_unique<ZonesOverlay>(m_window);

    return true;
}

HRESULT WorkArea::MoveSizeEnter(HWND window) noexcept
{
    m_windowMoveSize = window;
    m_highlightZone = {};
    m_initialHighlightZone = {};
    ShowZonesOverlay();
    Trace::WorkArea::MoveOrResizeStarted(m_zoneSet);
    return S_OK;
}

HRESULT WorkArea::MoveSizeUpdate(POINT const& ptScreen, bool dragEnabled, bool selectManyZones) noexcept
{
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
                highlightZone = m_zoneSet->GetCombinedZoneRange(m_initialHighlightZone, highlightZone);
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

    if (redraw)
    {
        m_zonesOverlay->DrawActiveZoneSet(m_zoneSet->GetZones(), m_highlightZone, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
    }

    return S_OK;
}

HRESULT WorkArea::MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept
{
    if (m_windowMoveSize != window)
    {
        return E_INVALIDARG;
    }

    if (m_zoneSet)
    {
        POINT ptClient = ptScreen;
        MapWindowPoints(nullptr, m_window, &ptClient, 1);
        m_zoneSet->MoveWindowIntoZoneByIndexSet(window, m_window, m_highlightZone);

        if (!FancyZonesWindowUtils::HasVisibleOwner(window))
        {
            SaveWindowProcessToZoneIndex(window);
        }
    }
    Trace::WorkArea::MoveOrResizeEnd(m_zoneSet);

    HideZonesOverlay();
    m_windowMoveSize = nullptr;
    return S_OK;
}

void WorkArea::MoveWindowIntoZoneByIndex(HWND window, ZoneIndex index) noexcept
{
    MoveWindowIntoZoneByIndexSet(window, { index });
}

void WorkArea::MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet) noexcept
{
    if (m_zoneSet)
    {
        m_zoneSet->MoveWindowIntoZoneByIndexSet(window, m_window, indexSet);
    }
}

bool WorkArea::MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle) noexcept
{
    if (m_zoneSet)
    {
        if (m_zoneSet->MoveWindowIntoZoneByDirectionAndIndex(window, m_window, vkCode, cycle))
        {
            if (!FancyZonesWindowUtils::HasVisibleOwner(window))
            {
                SaveWindowProcessToZoneIndex(window);
            }
            return true;
        }
    }
    return false;
}

bool WorkArea::MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle) noexcept
{
    if (m_zoneSet)
    {
        if (m_zoneSet->MoveWindowIntoZoneByDirectionAndPosition(window, m_window, vkCode, cycle))
        {
            SaveWindowProcessToZoneIndex(window);
            return true;
        }
    }
    return false;
}

bool WorkArea::ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode) noexcept
{
    if (m_zoneSet)
    {
        if (m_zoneSet->ExtendWindowByDirectionAndPosition(window, m_window, vkCode))
        {
            SaveWindowProcessToZoneIndex(window);
            return true;
        }
    }
    return false;
}

void WorkArea::SaveWindowProcessToZoneIndex(HWND window) noexcept
{
    if (m_zoneSet)
    {
        auto zoneIndexSet = m_zoneSet->GetZoneIndexSetFromWindow(window);
        if (zoneIndexSet.size())
        {
            OLECHAR* guidString;
            if (StringFromCLSID(m_zoneSet->Id(), &guidString) == S_OK)
            {
                AppZoneHistory::instance().SetAppLastZones(window, m_uniqueId, guidString, zoneIndexSet);
            }

            CoTaskMemFree(guidString);
        }
    }
}

ZoneIndexSet WorkArea::GetWindowZoneIndexes(HWND window) const noexcept
{
    if (m_zoneSet)
    {
        wil::unique_cotaskmem_string zoneSetId;
        if (SUCCEEDED(StringFromCLSID(m_zoneSet->Id(), &zoneSetId)))
        {
            return AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_uniqueId, zoneSetId.get());
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
    if (m_window)
    {
        SetAsTopmostWindow();
        m_zonesOverlay->DrawActiveZoneSet(m_zoneSet->GetZones(), m_highlightZone, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
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

    CalculateZoneSet(FancyZonesSettings::settings().overlappingZonesAlgorithm);
    if (m_window && m_zoneSet)
    {
        m_highlightZone.clear();
        m_zonesOverlay->DrawActiveZoneSet(m_zoneSet->GetZones(), m_highlightZone, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
    }
}

void WorkArea::CycleTabs(HWND window, bool reverse) noexcept
{
    if (m_zoneSet)
    {
        m_zoneSet->CycleTabs(window, reverse);
    }
}

void WorkArea::ClearSelectedZones() noexcept
{
    if (m_highlightZone.size())
    {
        m_highlightZone.clear();
        m_zonesOverlay->DrawActiveZoneSet(m_zoneSet->GetZones(), m_highlightZone, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
    }
}

void WorkArea::FlashZones() noexcept
{
    if (m_window)
    {
        SetAsTopmostWindow();
        m_zonesOverlay->DrawActiveZoneSet(m_zoneSet->GetZones(), {}, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
        m_zonesOverlay->Flash();
    }
}

#pragma region private

void WorkArea::InitializeZoneSets(const FancyZonesDataTypes::WorkAreaId& parentUniqueId) noexcept
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
    
    CalculateZoneSet(FancyZonesSettings::settings().overlappingZonesAlgorithm);
}

void WorkArea::CalculateZoneSet(OverlappingZonesAlgorithm overlappingAlgorithm) noexcept
{
    const auto appliedLayout = AppliedLayouts::instance().GetDeviceLayout(m_uniqueId);
    if (!appliedLayout.has_value())
    {
        Logger::error(L"Layout wasn't applied. Can't init zone set");
        return;
    }

    auto zoneSet = MakeZoneSet(ZoneSetConfig(
        appliedLayout->uuid,
        appliedLayout->type,
        m_monitor,
        appliedLayout->sensitivityRadius,
        overlappingAlgorithm));

    bool showSpacing = appliedLayout->showSpacing;
    int spacing = showSpacing ? appliedLayout->spacing : 0;
    int zoneCount = appliedLayout->zoneCount;

    zoneSet->CalculateZones(m_workAreaRect, zoneCount, spacing);
    UpdateActiveZoneSet(zoneSet.get());
}

void WorkArea::UpdateActiveZoneSet(_In_opt_ IZoneSet* zoneSet) noexcept
{
    m_zoneSet.copy_from(zoneSet);
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
    if (m_zoneSet)
    {
        return m_zoneSet->ZonesFromPoint(pt);
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

