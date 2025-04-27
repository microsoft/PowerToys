#pragma once

#include "pch.h"

namespace Utils
{
    namespace NonLocalizable
    {
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
