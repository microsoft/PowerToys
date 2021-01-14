#include "username.h"

#include <Windows.h>
#include <wtsapi32.h>

std::optional<std::wstring> ObtainActiveUserName()
{
    const DWORD sessionId = WTSGetActiveConsoleSessionId();
    WCHAR* pUserName;
    DWORD _ = 0;

    if (!WTSQuerySessionInformationW(WTS_CURRENT_SERVER_HANDLE, sessionId, WTSUserName, &pUserName, &_))
    {
        return std::nullopt;
    }
    WTSGetActiveConsoleSessionId();
    std::wstring result{ pUserName };
    WTSFreeMemory(pUserName);
    return result;
}
