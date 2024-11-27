#pragma once

namespace SnapshotUtils
{
    class PwaHelper
    {
    public:
        void InitAumidToAppId();
        BOOL GetAppId(HWND hWnd, std::wstring* result);
        BOOL GetPwaAppId(std::wstring windowAumid, std::wstring* result);
        BOOL SearchPwaName(std::wstring pwaAppId, std::wstring windowAumid, std::wstring* pwaName);
        void InitChromeAppIds();
        BOOL SearchPwaAppId(std::wstring windowAumid, std::wstring* pwaAppId);

    private:
        std::map<std::wstring, std::wstring> pwaAumidToAppId;
        std::vector<std::wstring> chromeAppIds;
        std::map<std::wstring, std::wstring> pwaAppIdsToAppNames;
    };
}
