#include "pch.h"
#include "NativeSnap.h"

#include <cstdlib>
#include <roapi.h>
#include <winstring.h>

namespace
{
    // WindowsUdk.UI.Shell.SnapLayoutManager is a Shell runtime class that ships with Windows but
    // is not part of the public API: there is no header, no IDL and no compatibility promise.
    // It is invoked through its ABI, declared below.
    // Registration:
    //   HKLM\SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId\WindowsUdk.UI.Shell.SnapLayoutManager
    //   DllPath = %SystemRoot%\system32\windowsudk.shellcommon.dll
    constexpr wchar_t SnapLayoutManagerClassName[] = L"WindowsUdk.UI.Shell.SnapLayoutManager";
    constexpr UINT32 SnapLayoutManagerClassNameLength = ARRAYSIZE(SnapLayoutManagerClassName) - 1;

    // ISnapLayoutManagerStatics, retrieved with RoGetActivationFactory.
    constexpr GUID IID_SnapLayoutManagerStatics{
        0x3d7594b4,
        0x9974,
        0x547f,
        { 0xbd, 0x9a, 0x9b, 0x40, 0x4e, 0x98, 0xd6, 0x99 }
    };

    // ISnapLayoutManagerStatics2, queried from the statics object, creates a manager per work area.
    constexpr GUID IID_SnapLayoutManagerStatics2{
        0x8afd7659,
        0x7b86,
        0x5a2f,
        { 0x95, 0xfe, 0xbd, 0x4d, 0xab, 0xea, 0x8e, 0xb1 }
    };

    // ABI layout of Windows.Foundation.Rect. Coordinates are absolute screen coordinates, not
    // relative to the work area; verified on monitors with a non-zero origin and with negative
    // coordinates.
    struct RectF
    {
        float x;
        float y;
        float width;
        float height;
    };

    static_assert(sizeof(RectF) == 16, "Windows.Foundation.Rect must be 16 bytes");

    // Only the slots that are used are declared. The slot order follows the WinRT ABI:
    // IUnknown(0-2) + IInspectable(3-5) + interface methods starting at 6.
    // A wrong slot index calls a different method with a mismatched signature, which crashes the
    // host process instead of returning an error.
    struct IUnknownVtbl
    {
        HRESULT(__stdcall* QueryInterface)(void* self, const GUID& iid, void** result);
        ULONG(__stdcall* AddRef)(void* self);
        ULONG(__stdcall* Release)(void* self);
    };

    // ISnapLayoutManagerStatics2: CreateForWorkAreaRect is at slot 6.
    struct ISnapLayoutManagerStatics2Vtbl
    {
        void* QueryInterface;
        void* AddRef;
        void* Release;
        void* GetIids;
        void* GetRuntimeClassName;
        void* GetTrustLevel;
        HRESULT(__stdcall* CreateForWorkAreaRect)(void* self, const RectF& workArea, void** result);
    };

    // ISnapLayoutManager: slot 6 is an unused method, SnapWindow is at slot 7.
    // The signature of slot 6 is unknown, so it is only reserved here and never called.
    struct ISnapLayoutManagerVtbl
    {
        void* QueryInterface;
        void* AddRef;
        void* Release;
        void* GetIids;
        void* GetRuntimeClassName;
        void* GetTrustLevel;
        void* UnusedSlot6;
        HRESULT(__stdcall* SnapWindow)(void* self, UINT64 windowId, const RectF& position, UINT32 positionCount, const RectF* positions);
    };

    void ReleaseInterface(void* object) noexcept
    {
        if (object)
        {
            static_cast<IUnknownVtbl*>(*static_cast<void**>(object))->Release(object);
        }
    }

    RectF ToRectF(const RECT& rect) noexcept
    {
        return RectF{
            static_cast<float>(rect.left),
            static_cast<float>(rect.top),
            static_cast<float>(rect.right - rect.left),
            static_cast<float>(rect.bottom - rect.top),
        };
    }

    // The Shell accepts a request whose rectangle does not touch the work area but then does
    // nothing, so the geometry is validated here to tell "silently ignored" from "snapped".
    bool TouchesEnoughWorkAreaEdges(const RECT& workArea, const RECT& rect) noexcept
    {
        int edges = 0;
        if (std::abs(rect.left - workArea.left) <= NativeSnap::EdgeTolerance)
        {
            ++edges;
        }
        if (std::abs(rect.top - workArea.top) <= NativeSnap::EdgeTolerance)
        {
            ++edges;
        }
        if (std::abs(rect.right - workArea.right) <= NativeSnap::EdgeTolerance)
        {
            ++edges;
        }
        if (std::abs(rect.bottom - workArea.bottom) <= NativeSnap::EdgeTolerance)
        {
            ++edges;
        }
        return edges >= 2;
    }
}

RECT NativeSnap::ExtendToWorkAreaEdges(const RECT& rect, const RECT& workArea, LONG maxEdgeInset) noexcept
{
    RECT extended = rect;
    if (maxEdgeInset < 0)
    {
        return extended;
    }

    if (std::abs(rect.left - workArea.left) <= maxEdgeInset)
    {
        extended.left = workArea.left;
    }
    if (std::abs(rect.top - workArea.top) <= maxEdgeInset)
    {
        extended.top = workArea.top;
    }
    if (std::abs(rect.right - workArea.right) <= maxEdgeInset)
    {
        extended.right = workArea.right;
    }
    if (std::abs(rect.bottom - workArea.bottom) <= maxEdgeInset)
    {
        extended.bottom = workArea.bottom;
    }

    if (extended.right <= extended.left || extended.bottom <= extended.top)
    {
        return rect;
    }

    return extended;
}

bool NativeSnap::SnapWindowToRect(HWND window, const RECT& snapRect) noexcept
{
    if (!window)
    {
        return false;
    }

    // Minimized and hidden windows stay on the SetWindowPlacement path: that path keeps the show
    // command and only changes the restore position, while native snapping would restore the
    // window, which is not what the user expects.
    if (::IsIconic(window) || !::IsWindowVisible(window))
    {
        return false;
    }

    // Non-resizable windows stay on the old path as well: AdjustRectForSizeWindowToRect keeps their
    // size and only moves them, while SnapWindow resizes unconditionally.
    if ((::GetWindowLong(window, GWL_STYLE) & WS_SIZEBOX) == 0)
    {
        return false;
    }

    // The work area is derived from the target rectangle instead of being passed in: for layouts
    // that span multiple monitors the caller's rectangle is the whole virtual screen, which the
    // Shell does not accept as a work area. Zones that span monitors do not touch the edges of a
    // single monitor, so the check below sends them to the fallback path.
    const HMONITOR monitor = ::MonitorFromRect(&snapRect, MONITOR_DEFAULTTONEAREST);
    MONITORINFO monitorInfo{ .cbSize = sizeof(MONITORINFO) };
    if (!monitor || !::GetMonitorInfoW(monitor, &monitorInfo))
    {
        return false;
    }

    if (!TouchesEnoughWorkAreaEdges(monitorInfo.rcWork, snapRect))
    {
        return false;
    }

    // The rectangle has to fit the work area of the monitor it was matched to. A zone that spans
    // several monitors has no matching work area, and the Shell would either ignore it or place
    // the window somewhere unexpected.
    const bool fitsWorkArea =
        snapRect.left >= monitorInfo.rcWork.left - EdgeTolerance &&
        snapRect.top >= monitorInfo.rcWork.top - EdgeTolerance &&
        snapRect.right <= monitorInfo.rcWork.right + EdgeTolerance &&
        snapRect.bottom <= monitorInfo.rcWork.bottom + EdgeTolerance;
    if (!fitsWorkArea)
    {
        return false;
    }

    HSTRING className = nullptr;
    if (FAILED(::WindowsCreateString(SnapLayoutManagerClassName, SnapLayoutManagerClassNameLength, &className)) || !className)
    {
        return false;
    }

    void* statics = nullptr;
    const HRESULT getStaticsResult = ::RoGetActivationFactory(className, IID_SnapLayoutManagerStatics, &statics);
    ::WindowsDeleteString(className);
    if (FAILED(getStaticsResult) || !statics)
    {
        return false;
    }

    void* statics2 = nullptr;
    const HRESULT queryResult = static_cast<IUnknownVtbl*>(*static_cast<void**>(statics))
                                    ->QueryInterface(statics, IID_SnapLayoutManagerStatics2, &statics2);
    ReleaseInterface(statics);
    if (FAILED(queryResult) || !statics2)
    {
        return false;
    }

    void* manager = nullptr;
    const RectF workAreaRect = ToRectF(monitorInfo.rcWork);
    const HRESULT createResult = static_cast<const ISnapLayoutManagerStatics2Vtbl*>(*static_cast<void**>(statics2))
                                     ->CreateForWorkAreaRect(statics2, workAreaRect, &manager);
    ReleaseInterface(statics2);
    if (FAILED(createResult) || !manager)
    {
        return false;
    }

    RectF position = ToRectF(snapRect);
    const HRESULT snapResult = static_cast<const ISnapLayoutManagerVtbl*>(*static_cast<void**>(manager))
                                   ->SnapWindow(
                                       manager,
                                       static_cast<UINT64>(reinterpret_cast<UINT_PTR>(window)),
                                       position,
                                       1,
                                       &position);
    ReleaseInterface(manager);

    return SUCCEEDED(snapResult);
}
