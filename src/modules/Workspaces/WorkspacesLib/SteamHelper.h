#pragma once

#include "pch.h"

namespace Utils
{
    namespace NonLocalizable
    {
        // When steam is running, window process's exe is this.
        const std::wstring SteamWindowExePath = L"steamwebhelper.exe";
        const std::wstring AcfFileNameTemplate = L"appmanifest_<gameid>.acfs";
    }

    namespace Steam
    {
        struct SteamGame
        {
            std::wstring gameId;
            std::wstring gameInstallationPath;
        };

        std::unique_ptr<SteamGame> GetSteamGameInfoFromAcfFile(const std::wstring& gameId);

        std::wstring GetGameIdFromUrlProtocolPath(const std::wstring& urlPath);
    }
}
