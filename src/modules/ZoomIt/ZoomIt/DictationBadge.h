//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Small overlay that tells the user dictation is listening and shows what has
// been transcribed so far. The window is excluded from capture so it never ends
// up inside the snip itself.
//
//==============================================================================
#pragma once

#include "pch.h"
#include <mutex>
#include <string>

class DictationBadge
{
public:
    ~DictationBadge() { Hide(); }

    //
    // Creates and shows the badge centered along the top edge of the supplied
    // selection rectangle, in screen coordinates, moving outside it when the
    // selection is too short to hold the badge.
    //
    void Show( HWND owner, const RECT& anchor );

    //
    // Replaces the transcription line. Safe to call from any thread.
    //
    void SetText( const std::wstring& text );

    //
    // Replaces the leading status line, for example an error explanation.
    //
    void SetStatus( const std::wstring& status );

    //
    // Moves the badge to follow a changing selection.
    //
    void Reposition( const RECT& anchor );

    void Hide();

    bool IsVisible() const { return m_window != nullptr; }

private:
    void Refresh();
    static LRESULT CALLBACK WindowProcThunk( HWND window, UINT message, WPARAM wordParam, LPARAM longParam );
    LRESULT WindowProc( HWND window, UINT message, WPARAM wordParam, LPARAM longParam );

    void Paint( HDC deviceContext, const RECT& client );
    SIZE MeasureContent() const;

    const wchar_t* m_className = L"ZoomitDictationBadge";
    wil::unique_hwnd m_window;
    wil::unique_hfont m_font;
    UINT m_dpi{ USER_DEFAULT_SCREEN_DPI };
    RECT m_anchor{};
    std::wstring m_text;
    std::wstring m_status;
    mutable std::mutex m_textLock;
};
