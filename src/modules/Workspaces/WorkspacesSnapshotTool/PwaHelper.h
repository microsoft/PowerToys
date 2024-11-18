#pragma once

namespace SnapshotUtils
{
    class PwaHelper
    {
    public:
        void InitAumidToAppId(DWORD pid);
        BOOL GetAppId(HWND hWnd, std::wstring* result);
        BOOL GetPwaAppId(std::wstring windowAumid, std::wstring* result);
        BOOL SearchPwaName(std::wstring pwaAppId, std::wstring windowAumid, std::wstring* pwaName);
        void InitChromeAppIds();
        BOOL SearchPwaAppId(std::wstring windowAumid, std::wstring* pwaAppId);

    private:
    };
}
