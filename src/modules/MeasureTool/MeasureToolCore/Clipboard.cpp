#include "pch.h"

#include "Clipboard.h"

#include <sstream>

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

void SetClipboardToMeasurements(const std::vector<Measurement>& measurements,
                                bool printWidth,
                                bool printHeight,
                                Measurement::Unit units)
{
    if (measurements.empty())
    {
        return;
    }

    std::wostringstream stream;
    bool isFirst = true;

    for (const auto& measurement : measurements)
    {
        measurement.PrintToStream(stream, !isFirst, printWidth, printHeight, units);
        isFirst = false;
    }

    SetClipBoardToText(stream.str());
}
