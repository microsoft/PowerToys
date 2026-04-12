#include "pch.h"
#include "AltDragMove.h"

AltDragMove& AltDragMove::instance()
{
    static AltDragMove s_instance;
    return s_instance;
}

void AltDragMove::Start()
{
    if (m_hook)
        return;

    m_hook = SetWindowsHookExW(
        WH_MOUSE_LL,
        LowLevelMouseProc,
        GetModuleHandleW(nullptr),
        0);
}

void AltDragMove::Stop()
{
    if (m_hook)
    {
        UnhookWindowsHookEx(m_hook);
        m_hook = nullptr;
    }
    m_dragging = false;
    m_dragWindow = nullptr;
}

bool AltDragMove::IsModifierPressed() const
{
    switch (m_modifier)
    {
    case Modifier::Alt:
        return (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
    case Modifier::Ctrl:
        return (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
    case Modifier::Shift:
        return (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
    }
    return false;
}

/// Given a point, walk up the window hierarchy to find the top-level
/// (non-child) window that should be moved.
static HWND GetTopLevelWindowUnderCursor(POINT pt)
{
    HWND hwnd = WindowFromPoint(pt);
    if (!hwnd)
        return nullptr;

    // Walk up to the top-level owner/parent.
    while (HWND parent = GetParent(hwnd))
    {
        hwnd = parent;
    }

    // Skip the desktop window.
    if (hwnd == GetDesktopWindow())
        return nullptr;

    return hwnd;
}

LRESULT CALLBACK AltDragMove::LowLevelMouseProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode >= 0)
    {
        auto& self = instance();
        auto* msll = reinterpret_cast<MSLLHOOKSTRUCT*>(lParam);

        switch (wParam)
        {
        case WM_LBUTTONDOWN:
        {
            if (self.IsModifierPressed())
            {
                HWND target = GetTopLevelWindowUnderCursor(msll->pt);
                if (target)
                {
                    self.m_dragging = true;
                    self.m_dragWindow = target;
                    self.m_dragStartCursor = msll->pt;
                    GetWindowRect(target, &self.m_dragStartRect);

                    // Swallow the click so the target window does not
                    // receive it.
                    return 1;
                }
            }
            break;
        }

        case WM_MOUSEMOVE:
        {
            if (self.m_dragging && self.m_dragWindow)
            {
                int dx = msll->pt.x - self.m_dragStartCursor.x;
                int dy = msll->pt.y - self.m_dragStartCursor.y;

                SetWindowPos(
                    self.m_dragWindow,
                    nullptr,
                    self.m_dragStartRect.left + dx,
                    self.m_dragStartRect.top + dy,
                    0,
                    0,
                    SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

                return 1;
            }
            break;
        }

        case WM_LBUTTONUP:
        {
            if (self.m_dragging)
            {
                self.m_dragging = false;
                self.m_dragWindow = nullptr;
                return 1;
            }
            break;
        }
        }
    }

    return CallNextHookEx(nullptr, nCode, wParam, lParam);
}
