#include "pch.h"
#include "winapi_error.h"

std::optional<std::wstring> get_last_error_message(DWORD dw)
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

std::wstring get_last_error_or_default(DWORD dw)
{
    auto message = get_last_error_message(dw);
    return message.has_value() ? *message : L"";
}

void show_last_error_message(const wchar_t* functionName, DWORD dw, const wchar_t* errorTitle)
{
    const auto systemMessage = get_last_error_message(dw);
    if (!systemMessage.has_value())
    {
        return;
    }
    LPWSTR lpDisplayBuf = static_cast<LPWSTR>(LocalAlloc(LMEM_ZEROINIT, (systemMessage->size() + lstrlenW(functionName) + 40) * sizeof(WCHAR)));
    if (lpDisplayBuf != nullptr)
    {
        StringCchPrintfW(lpDisplayBuf,
                         LocalSize(lpDisplayBuf) / sizeof(WCHAR),
                         L"%s: %s (%d)",
                         functionName,
                         systemMessage->c_str(),
                         dw);
        MessageBoxW(nullptr, lpDisplayBuf, errorTitle, MB_OK | MB_ICONERROR);
        LocalFree(lpDisplayBuf);
    }
}
