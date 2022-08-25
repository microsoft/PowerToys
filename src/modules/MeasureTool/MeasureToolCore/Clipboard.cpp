#include "pch.h"

#include "Clipboard.h"

void SetClipBoardToText(const std::wstring_view text)
{
    if (!OpenClipboard(nullptr))
    {
        return;
    }

    const wil::unique_hglobal handle{ GlobalAlloc(GMEM_MOVEABLE, static_cast<size_t>((text.length() + 1) * sizeof(wchar_t))) };
    if (!handle)
    {
        CloseClipboard();
        return;
    }

    if (auto* bufPtr = static_cast<wchar_t*>(GlobalLock(handle.get())); bufPtr != nullptr)
    {
        text.copy(bufPtr, text.length());
        GlobalUnlock(handle.get());
    }

    EmptyClipboard();
    SetClipboardData(CF_UNICODETEXT, handle.get());
    CloseClipboard();
}
