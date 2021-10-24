#include "pch.h"
#include "WorkArea.h"

#include <common/logger/logger.h>

#include "FancyZonesData.h"
#include "FancyZonesDataTypes.h"
#include "ZoneWindowDrawing.h"
#include "trace.h"
#include "util.h"
#include "on_thread_executor.h"
#include "Settings.h"
#include "CallTracer.h"

#include <ShellScalingApi.h>
#include <mutex>
#include <fileapi.h>

#include <gdiplus.h>

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t ToolWindowClassName[] = L"SuperFancyZones_ZoneWindow";
}

using namespace FancyZonesUtils;

struct WorkArea;

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
            _TRACER_;
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

        HWND NewZoneWindow(Rect position, HINSTANCE hinstance, WorkArea* owner)
        {
            HWND windowFromPool = ExtractWindow();
            if (windowFromPool == NULL)
            {
                HWND window = CreateWindowExW(WS_EX_TOOLWINDOW, NonLocalizable::ToolWindowClassName, L"", WS_POPUP, position.left(), position.top(), position.width(), position.height(), nullptr, nullptr, hinstance, owner);
                Logger::info("Creating new zone window, hWnd = {}", (void*)window);
                MakeWindowTransparent(window);

                // According to ShowWindow docs, we must call it with SW_SHOWNORMAL the first time
                ShowWindow(window, SW_SHOWNORMAL);
                ShowWindow(window, SW_HIDE);
                return window;
            }
            else
            {
                Logger::info("Reusing zone window from pool, hWnd = {}", (void*)windowFromPool);
                SetWindowLongPtrW(windowFromPool, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(owner));
                MoveWindow(windowFromPool, position.left(), position.top(), position.width(), position.height(), TRUE);
                return windowFromPool;
            }
        }

        void FreeZoneWindow(HWND window)
        {
            _TRACER_;
            Logger::info("Freeing zone window, hWnd = {}", (void*)window);
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

struct WorkArea : public winrt::implements<WorkArea, IWorkArea>
{
public:
    WorkArea(HINSTANCE hinstance);
    ~WorkArea();

    bool Init(HINSTANCE hinstance, HMONITOR monitor, const FancyZonesDataTypes::DeviceIdData& uniqueId, const FancyZonesDataTypes::DeviceIdData& parentUniqueId, const ZoneColors& zoneColors, OverlappingZonesAlgorithm overlappingAlgorithm);

    IFACEMETHODIMP MoveSizeEnter(HWND window) noexcept;
    IFACEMETHODIMP MoveSizeUpdate(POINT const& ptScreen, bool dragEnabled, bool selectManyZones) noexcept;
    IFACEMETHODIMP MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept;
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByIndex(HWND window, ZoneIndex index) noexcept;
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet) noexcept;
    IFACEMETHODIMP_(bool)
    MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle) noexcept;
    IFACEMETHODIMP_(bool)
    MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle) noexcept;
    IFACEMETHODIMP_(bool)
    ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode) noexcept;
    IFACEMETHODIMP_(FancyZonesDataTypes::DeviceIdData)
    UniqueId() const noexcept { return { m_uniqueId }; }
    IFACEMETHODIMP_(void)
    SaveWindowProcessToZoneIndex(HWND window) noexcept;
    IFACEMETHODIMP_(IZoneSet*)
    ActiveZoneSet() const noexcept { return m_activeZoneSet.get(); }
    IFACEMETHODIMP_(ZoneIndexSet)
    GetWindowZoneIndexes(HWND window) const noexcept;
    IFACEMETHODIMP_(void)
    ShowZoneWindow() noexcept;
    IFACEMETHODIMP_(void)
    HideZoneWindow() noexcept;
    IFACEMETHODIMP_(void)
    UpdateActiveZoneSet() noexcept;
    IFACEMETHODIMP_(void)
    ClearSelectedZones() noexcept;
    IFACEMETHODIMP_(void)
    FlashZones() noexcept;
    IFACEMETHODIMP_(void)
    SetZoneColors(const ZoneColors& colors) noexcept;
    IFACEMETHODIMP_(void)
    SetOverlappingZonesAlgorithm(OverlappingZonesAlgorithm overlappingAlgorithm) noexcept;

protected:
    static LRESULT CALLBACK s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept;

private:
    void InitializeZoneSets(const FancyZonesDataTypes::DeviceIdData& parentUniqueId) noexcept;
    void CalculateZoneSet(OverlappingZonesAlgorithm overlappingAlgorithm) noexcept;
    void UpdateActiveZoneSet(_In_opt_ IZoneSet* zoneSet) noexcept;
    LRESULT WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept;
    ZoneIndexSet ZonesFromPoint(POINT pt) noexcept;
    void SetAsTopmostWindow() noexcept;

    HMONITOR m_monitor{};
    FancyZonesDataTypes::DeviceIdData m_uniqueId;
    HWND m_window{}; // Hidden tool window used to represent current monitor desktop work area.
    HWND m_windowMoveSize{};
    winrt::com_ptr<IZoneSet> m_activeZoneSet;
    std::vector<winrt::com_ptr<IZoneSet>> m_zoneSets;
    ZoneIndexSet m_initialHighlightZone;
    ZoneIndexSet m_highlightZone;
    WPARAM m_keyLast{};
    size_t m_keyCycle{};
    std::unique_ptr<ZoneWindowDrawing> m_zoneWindowDrawing;
    ZoneColors m_zoneColors;
    OverlappingZonesAlgorithm m_overlappingAlgorithm;
};

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
    windowPool.FreeZoneWindow(m_window);
}

bool WorkArea::Init(HINSTANCE hinstance, HMONITOR monitor, const FancyZonesDataTypes::DeviceIdData& uniqueId, const FancyZonesDataTypes::DeviceIdData& parentUniqueId, const ZoneColors& zoneColors, OverlappingZonesAlgorithm overlappingAlgorithm)
{
    m_zoneColors = zoneColors;
    m_overlappingAlgorithm = overlappingAlgorithm;

    Rect workAreaRect;
    m_monitor = monitor;
    if (monitor)
    {
        MONITORINFO mi{};
        mi.cbSize = sizeof(mi);
        if (!GetMonitorInfoW(monitor, &mi))
        {
            return false;
        }
        workAreaRect = Rect(mi.rcWork);
    }
    else
    {
        workAreaRect = GetAllMonitorsCombinedRect<&MONITORINFO::rcWork>();
    }

    m_uniqueId = uniqueId;
    InitializeZoneSets(parentUniqueId);

    m_window = windowPool.NewZoneWindow(workAreaRect, hinstance, this);

    if (!m_window)
    {
        return false;
    }

    m_zoneWindowDrawing = std::make_unique<ZoneWindowDrawing>(m_window);

    return true;
}

IFACEMETHODIMP WorkArea::MoveSizeEnter(HWND window) noexcept
{
    m_windowMoveSize = window;
    m_highlightZone = {};
    m_initialHighlightZone = {};
    ShowZoneWindow();
    return S_OK;
}

IFACEMETHODIMP WorkArea::MoveSizeUpdate(POINT const& ptScreen, bool dragEnabled, bool selectManyZones) noexcept
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
                highlightZone = m_activeZoneSet->GetCombinedZoneRange(m_initialHighlightZone, highlightZone);
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
        m_zoneWindowDrawing->DrawActiveZoneSet(m_activeZoneSet->GetZones(), m_highlightZone, m_zoneColors);
    }

    return S_OK;
}

IFACEMETHODIMP WorkArea::MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept
{
    if (m_windowMoveSize != window)
    {
        return E_INVALIDARG;
    }

    if (m_activeZoneSet)
    {
        POINT ptClient = ptScreen;
        MapWindowPoints(nullptr, m_window, &ptClient, 1);
        m_activeZoneSet->MoveWindowIntoZoneByIndexSet(window, m_window, m_highlightZone);

        if (FancyZonesUtils::HasNoVisibleOwner(window))
        {
            SaveWindowProcessToZoneIndex(window);
        }
    }
    Trace::WorkArea::MoveSizeEnd(m_activeZoneSet);

    HideZoneWindow();
    m_windowMoveSize = nullptr;
    return S_OK;
}

IFACEMETHODIMP_(void)
WorkArea::MoveWindowIntoZoneByIndex(HWND window, ZoneIndex index) noexcept
{
    MoveWindowIntoZoneByIndexSet(window, { index });
}

IFACEMETHODIMP_(void)
WorkArea::MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet) noexcept
{
    if (m_activeZoneSet)
    {
        m_activeZoneSet->MoveWindowIntoZoneByIndexSet(window, m_window, indexSet);
    }
}

IFACEMETHODIMP_(bool)
WorkArea::MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle) noexcept
{
    if (m_activeZoneSet)
    {
        if (m_activeZoneSet->MoveWindowIntoZoneByDirectionAndIndex(window, m_window, vkCode, cycle))
        {
            if (FancyZonesUtils::HasNoVisibleOwner(window))
            {
                SaveWindowProcessToZoneIndex(window);
            }
            return true;
        }
    }
    return false;
}

IFACEMETHODIMP_(bool)
WorkArea::MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle) noexcept
{
    if (m_activeZoneSet)
    {
        if (m_activeZoneSet->MoveWindowIntoZoneByDirectionAndPosition(window, m_window, vkCode, cycle))
        {
            SaveWindowProcessToZoneIndex(window);
            return true;
        }
    }
    return false;
}

IFACEMETHODIMP_(bool)
WorkArea::ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode) noexcept
{
    if (m_activeZoneSet)
    {
        if (m_activeZoneSet->ExtendWindowByDirectionAndPosition(window, m_window, vkCode))
        {
            SaveWindowProcessToZoneIndex(window);
            return true;
        }
    }
    return false;
}

IFACEMETHODIMP_(void)
WorkArea::SaveWindowProcessToZoneIndex(HWND window) noexcept
{
    if (m_activeZoneSet)
    {
        auto zoneIndexSet = m_activeZoneSet->GetZoneIndexSetFromWindow(window);
        if (zoneIndexSet.size())
        {
            OLECHAR* guidString;
            if (StringFromCLSID(m_activeZoneSet->Id(), &guidString) == S_OK)
            {
                FancyZonesDataInstance().SetAppLastZones(window, m_uniqueId, guidString, zoneIndexSet);
            }

            CoTaskMemFree(guidString);
        }
    }
}

IFACEMETHODIMP_(ZoneIndexSet)
WorkArea::GetWindowZoneIndexes(HWND window) const noexcept
{
    if (m_activeZoneSet)
    {
        wil::unique_cotaskmem_string zoneSetId;
        if (SUCCEEDED(StringFromCLSID(m_activeZoneSet->Id(), &zoneSetId)))
        {
            return FancyZonesDataInstance().GetAppLastZoneIndexSet(window, m_uniqueId, zoneSetId.get());
        }
    }
    return {};
}

IFACEMETHODIMP_(void)
WorkArea::ShowZoneWindow() noexcept
{
    if (m_window)
    {
        SetAsTopmostWindow();
        m_zoneWindowDrawing->DrawActiveZoneSet(m_activeZoneSet->GetZones(), m_highlightZone, m_zoneColors);
        m_zoneWindowDrawing->Show();
    }
}

IFACEMETHODIMP_(void)
WorkArea::HideZoneWindow() noexcept
{
    if (m_window)
    {
        m_zoneWindowDrawing->Hide();
        m_keyLast = 0;
        m_windowMoveSize = nullptr;
        m_highlightZone = {};
    }
}

IFACEMETHODIMP_(void)
WorkArea::UpdateActiveZoneSet() noexcept
{
    CalculateZoneSet(m_overlappingAlgorithm);
    if (m_window)
    {
        m_highlightZone.clear();
        m_zoneWindowDrawing->DrawActiveZoneSet(m_activeZoneSet->GetZones(), m_highlightZone, m_zoneColors);
    }
}

IFACEMETHODIMP_(void)
WorkArea::ClearSelectedZones() noexcept
{
    if (m_highlightZone.size())
    {
        m_highlightZone.clear();
        m_zoneWindowDrawing->DrawActiveZoneSet(m_activeZoneSet->GetZones(), m_highlightZone, m_zoneColors);
    }
}

IFACEMETHODIMP_(void)
WorkArea::FlashZones() noexcept
{
    if (m_window)
    {
        SetAsTopmostWindow();
        m_zoneWindowDrawing->DrawActiveZoneSet(m_activeZoneSet->GetZones(), {}, m_zoneColors);
        m_zoneWindowDrawing->Flash();
    }
}

IFACEMETHODIMP_(void)
WorkArea::SetZoneColors(const ZoneColors& colors) noexcept
{
    m_zoneColors = colors;
}

IFACEMETHODIMP_(void)
WorkArea::SetOverlappingZonesAlgorithm(OverlappingZonesAlgorithm overlappingAlgorithm) noexcept
{
    m_overlappingAlgorithm = overlappingAlgorithm;
}


#pragma region private

void WorkArea::InitializeZoneSets(const FancyZonesDataTypes::DeviceIdData& parentUniqueId) noexcept
{
    wil::unique_cotaskmem_string virtualDesktopId;
    if (SUCCEEDED(StringFromCLSID(m_uniqueId.virtualDesktopId, &virtualDesktopId)))
    {
        Logger::debug(L"Initialize zone sets on the virtual desktop {}", virtualDesktopId.get());
    }
    
    bool deviceAdded = FancyZonesDataInstance().AddDevice(m_uniqueId);
    // If the device has been added, check if it should inherit the parent's layout
    if (deviceAdded && parentUniqueId.virtualDesktopId != GUID_NULL)
    {
        FancyZonesDataInstance().CloneDeviceInfo(parentUniqueId, m_uniqueId);
    }
    CalculateZoneSet(m_overlappingAlgorithm);
}

void WorkArea::CalculateZoneSet(OverlappingZonesAlgorithm overlappingAlgorithm) noexcept
{
    const auto& fancyZonesData = FancyZonesDataInstance();
    const auto deviceInfoData = fancyZonesData.FindDeviceInfo(m_uniqueId);

    if (!deviceInfoData.has_value())
    {
        return;
    }

    const auto& activeZoneSet = deviceInfoData->activeZoneSet;

    if (activeZoneSet.uuid.empty())
    {
        return;
    }

    GUID zoneSetId;
    if (SUCCEEDED_LOG(CLSIDFromString(activeZoneSet.uuid.c_str(), &zoneSetId)))
    {
        int sensitivityRadius = deviceInfoData->sensitivityRadius;

        auto zoneSet = MakeZoneSet(ZoneSetConfig(
            zoneSetId,
            activeZoneSet.type,
            m_monitor,
            sensitivityRadius,
            overlappingAlgorithm));

        RECT workArea;
        if (m_monitor)
        {
            MONITORINFO monitorInfo{};
            monitorInfo.cbSize = sizeof(monitorInfo);
            if (GetMonitorInfoW(m_monitor, &monitorInfo))
            {
                workArea = monitorInfo.rcWork;
            }
            else
            {
                return;
            }
        }
        else
        {
            workArea = GetAllMonitorsCombinedRect<&MONITORINFO::rcWork>();
        }

        bool showSpacing = deviceInfoData->showSpacing;
        int spacing = showSpacing ? deviceInfoData->spacing : 0;
        int zoneCount = deviceInfoData->zoneCount;

        zoneSet->CalculateZones(workArea, zoneCount, spacing);
        UpdateActiveZoneSet(zoneSet.get());
    }
}

void WorkArea::UpdateActiveZoneSet(_In_opt_ IZoneSet* zoneSet) noexcept
{
    m_activeZoneSet.copy_from(zoneSet);

    if (m_activeZoneSet)
    {
        wil::unique_cotaskmem_string zoneSetId;
        if (SUCCEEDED_LOG(StringFromCLSID(m_activeZoneSet->Id(), &zoneSetId)))
        {
            FancyZonesDataTypes::ZoneSetData data{
                .uuid = zoneSetId.get(),
                .type = m_activeZoneSet->LayoutType()
            };
            FancyZonesDataInstance().SetActiveZoneSet(m_uniqueId, data);
        }
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
    if (m_activeZoneSet)
    {
        return m_activeZoneSet->ZonesFromPoint(pt);
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

winrt::com_ptr<IWorkArea> MakeWorkArea(HINSTANCE hinstance, HMONITOR monitor, const FancyZonesDataTypes::DeviceIdData& uniqueId, const FancyZonesDataTypes::DeviceIdData& parentUniqueId, const ZoneColors& zoneColors, OverlappingZonesAlgorithm overlappingAlgorithm) noexcept
{
    auto self = winrt::make_self<WorkArea>(hinstance);
    if (self->Init(hinstance, monitor, uniqueId, parentUniqueId, zoneColors, overlappingAlgorithm))
    {
        return self;
    }

    return nullptr;
}
