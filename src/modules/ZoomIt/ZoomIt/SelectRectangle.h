//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Class to select a recording rectangle and show it while recording
//
//==============================================================================
#pragma once

#include "pch.h"

class SelectRectangle
{
public:
    ~SelectRectangle() { Stop(); };

    void Alpha( BYTE alpha ) { m_alpha = alpha; }
    BYTE Alpha() const { return m_alpha; }
    void MinSize( int minSize ) { m_minSize = minSize; }
    int MinSize() const { return m_minSize; }
    RECT SelectedRect() const { return m_selectedRect; }

    bool Start( HWND ownerWindow = nullptr, bool fullMonitor = false );
    void Stop();
    void UpdateOwner( HWND window );

private:
    BYTE m_alpha = 176;
    int m_minSize = 34;
    RECT m_selectedRect{};

    bool m_cancel = false;
    const wchar_t* m_className = L"ZoomitSelectRectangle";
    UINT m_dpi{};
    RECT m_oldClipRect{};
    bool m_selected{ false };
    bool m_setClip{ false };
    POINT m_startPoint{};
    wil::unique_hwnd m_window;

    void ShowSelected();
    LRESULT WindowProc( HWND window, UINT message, WPARAM wordParam, LPARAM longParam );
};
