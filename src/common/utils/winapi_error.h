#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <optional>
#include <string>
#include <system_error>
#include <strsafe.h>

inline std::optional<std::wstring> get_last_error_message(const DWORD dw)
{
    std::optional<std::wstring> message;
    try
    {
        const auto msg = std::system_category().message(dw);
        message.emplace(begin(msg), end(msg));
    }
    catch (...)
    {
    }
    return message;
}

inline std::wstring get_last_error_or_default(const DWORD dw)
{
    auto message = get_last_error_message(dw);
    return message.has_value() ? *message : L"";
}

inline void show_last_error_message(const wchar_t* functionName, DWORD dw, const wchar_t* errorTitle)
{
    const auto system_message = get_last_error_message(dw);
    if (!system_message.has_value())
    {
        return;
    }
    LPWSTR lpDisplayBuf = static_cast<LPWSTR>(LocalAlloc(LMEM_ZEROINIT, (system_message->size() + lstrlenW(functionName) + 40) * sizeof(WCHAR)));
    if (lpDisplayBuf != NULL)
    {
        StringCchPrintfW(lpDisplayBuf,
                         LocalSize(lpDisplayBuf) / sizeof(WCHAR),
                         L"%s: %s (%d)",
                         functionName,
                         system_message->c_str(),
                         dw);
        MessageBoxW(NULL, lpDisplayBuf, errorTitle, MB_OK | MB_ICONERROR);
        LocalFree(lpDisplayBuf);
    }
}
