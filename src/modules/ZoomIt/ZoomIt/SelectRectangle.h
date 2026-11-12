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
#include <functional>

class SelectRectangle
{
public:
    ~SelectRectangle() { Stop(); };

    void Alpha( BYTE alpha ) { m_alpha = alpha; }
    BYTE Alpha() const { return m_alpha; }
    void MinSize( int minSize ) { m_minSize = minSize; }
    int MinSize() const { return m_minSize; }
    void AspectRatio( double ratio ) { m_aspectRatio = ratio; }
    double AspectRatio() const { return m_aspectRatio; }
    // Sets the border color Start resets to (default: capture-API yellow).
    void BorderColor( COLORREF color ) { m_configuredBorderColor = color; m_borderColor = color; }
    void RetainDimmedExteriorAfterSelection( bool retain ) { m_retainDimmedExterior = retain; }
    RECT SelectedRect() const { return m_selectedRect; }
    bool IsActive() const { return m_window != nullptr; }

    // Optional notifications raised from the modal Start message loop.
    void OnDragStarted( std::function<void()> callback ) { m_onDragStarted = std::move( callback ); }
    void OnDragCompleted( std::function<void()> callback ) { m_onDragCompleted = std::move( callback ); }
    void OnCancelled( std::function<void()> callback ) { m_onCancelled = std::move( callback ); }
    void OnSelectionChanged( std::function<void( const RECT& )> callback ) { m_onSelectionChanged = std::move( callback ); }
    void OnDictateRequested( std::function<void()> callback ) { m_onDictateRequested = std::move( callback ); }

    // borderColor sets the initial selection border color (defaults to yellow to
    // match the capture API). Panorama capture passes blue.
    bool Start( HWND ownerWindow = nullptr, bool fullMonitor = false, COLORREF borderColor = RGB( 255, 222, 0 ) );
    void Stop();
    void UpdateOwner( HWND window );
    void Hide() { if( m_window ) ShowWindow( m_window.get(), SW_HIDE ); }
    void Show() { if( m_window ) ShowWindow( m_window.get(), SW_SHOWNA ); }
    void SetExcludeFromCapture( bool exclude ) { if( m_window ) SetWindowDisplayAffinity( m_window.get(), exclude ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE ); }

    // Signal that recording is actively capturing frames.
    // Changes the border to a thick red frame so the user can clearly
    // distinguish "waiting to start" (thin yellow) from "recording" (thick red).
    void SetRecordingActive();

    // Diagnostic accessors for debug logging.
    HWND Window() const { return m_window.get(); }
    bool IsSelected() const { return m_selected; }

private:
    BYTE m_alpha = 176;
    int m_minSize = 34;
    double m_aspectRatio = 0.0; // 0 = no constraint, e.g. 16.0/9.0
    RECT m_selectedRect{};
    COLORREF m_borderColor = RGB( 255, 222, 0 ); // default: yellow (matches capture API)
    COLORREF m_configuredBorderColor = RGB( 255, 222, 0 ); // color Start resets to
    bool m_recordingActive = false; // true once first frame is captured
    bool m_fullMonitor = false;       // true when recording full screen

    bool m_cancel = false;
    const wchar_t* m_className = L"ZoomitSelectRectangle";
    UINT m_dpi{};
    RECT m_oldClipRect{};
    bool m_selected{ false };
    bool m_setClip{ false };
    bool m_stopping{ false };
    bool m_dragStarted{ false };
    bool m_retainDimmedExterior{ false };
    POINT m_startPoint{};
    wil::unique_hwnd m_window;

    std::function<void()> m_onDragStarted;
    std::function<void()> m_onDragCompleted;
    std::function<void()> m_onCancelled;
    std::function<void()> m_onDictateRequested;
    std::function<void( const RECT& )> m_onSelectionChanged;

    void NotifyDragStarted();
    void NotifyDragCompleted();
    void NotifyCancelled();
    void NotifyDictateRequested();
    void NotifySelectionChanged();

    void ShowSelected();
    LRESULT WindowProc( HWND window, UINT message, WPARAM wordParam, LPARAM longParam );
};
