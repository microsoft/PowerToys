#include "pch.h"
#include "SteamHelper.h"
#include <fstream>
#include <sstream>
#include <unordered_map>
#include <filesystem>
#include <regex>
#include <string>

namespace Utils
{

    static std::wstring Utf8ToWide(const std::string& utf8)
    {
        if (utf8.empty())
            return L"";

        int size = MultiByteToWideChar(CP_UTF8, 0, utf8.data(), static_cast<int>(utf8.size()), nullptr, 0);
        if (size <= 0)
            return L"";

        std::wstring wide(size, L'\0');
        MultiByteToWideChar(CP_UTF8, 0, utf8.data(), static_cast<int>(utf8.size()), wide.data(), size);
        return wide;
    }

    namespace Steam
    {
        using namespace std;
        namespace fs = std::filesystem;

        static std::optional<std::wstring> GetSteamExePathFromRegistry()
        {
            static std::optional<std::wstring> cachedPath;
            if (cachedPath.has_value())
            {
                return cachedPath;
            }

            const std::vector<HKEY> roots = { HKEY_CLASSES_ROOT, HKEY_LOCAL_MACHINE, HKEY_USERS };
            const std::vector<std::wstring> subKeys = {
                L"steam\\shell\\open\\command",
                L"Software\\Classes\\steam\\shell\\open\\command",
            };

            for (HKEY root : roots)
            {
                for (const auto& subKey : subKeys)
                {
                    HKEY hKey;
                    if (RegOpenKeyExW(root, subKey.c_str(), 0, KEY_READ, &hKey) == ERROR_SUCCESS)
                    {
                        wchar_t value[512];
                        DWORD size = sizeof(value);
                        DWORD type = 0;

                        if (RegQueryValueExW(hKey, nullptr, nullptr, &type, reinterpret_cast<LPBYTE>(value), &size) == ERROR_SUCCESS &&
                            (type == REG_SZ || type == REG_EXPAND_SZ))
                        {
                            std::wregex exeRegex(LR"delim("([^"]+steam\.exe)")delim");
                            std::wcmatch match;
                            if (std::regex_search(value, match, exeRegex) && match.size() > 1)
                            {
                                RegCloseKey(hKey);
                                cachedPath = match[1].str();
                                return cachedPath;
                            }
                        }

                        RegCloseKey(hKey);
                    }
                }
            }

            cachedPath = std::nullopt;
            return std::nullopt;
        }

        static fs::path GetSteamBasePath()
        {
            auto steamFolderOpt = GetSteamExePathFromRegistry();
            if (!steamFolderOpt)
            {
                return {};
            }

            return fs::path(*steamFolderOpt).parent_path() / L"steamapps";
        }

        static fs::path GetAcfFilePath(const std::wstring& gameId)
        {
            auto steamFolderOpt = GetSteamExePathFromRegistry();
            if (!steamFolderOpt)
            {
                return {};
            }

            return GetSteamBasePath() / (L"appmanifest_" + gameId + L".acf");
        }

        static fs::path GetGameInstallPath(const std::wstring& gameFolderName)
        {
            auto steamFolderOpt = GetSteamExePathFromRegistry();
            if (!steamFolderOpt)
            {
                return {};
            }

            return GetSteamBasePath() / L"common" / gameFolderName;
        }

        static unordered_map<wstring, wstring> ParseAcfFile(const fs::path& acfPath)
        {
            unordered_map<wstring, wstring> result;

            ifstream file(acfPath);
            if (!file.is_open())
                return result;

            string line;
            while (getline(file, line))
            {
                smatch matches;
                static const regex pattern(R"delim("([^"]+)"\s+"([^"]+)")delim");

                if (regex_search(line, matches, pattern) && matches.size() == 3)
                {
                    wstring key = Utf8ToWide(matches[1].str());
                    wstring value = Utf8ToWide(matches[2].str());
                    result[key] = value;
                }
            }

            return result;
        }

        std::unique_ptr<Steam::SteamGame> GetSteamGameInfoFromAcfFile(const std::wstring& gameId)
        {
            fs::path acfPath = Steam::GetAcfFilePath(gameId);

            if (!fs::exists(acfPath))
                return nullptr;

            auto kv = ParseAcfFile(acfPath);
            if (kv.empty() || kv.find(L"installdir") == kv.end())
                return nullptr;

            fs::path gamePath = Steam::GetGameInstallPath(kv[L"installdir"]);
            if (!fs::exists(gamePath))
                return nullptr;

            auto game = std::make_unique<Steam::SteamGame>();
            game->gameId = gameId;
            game->gameInstallationPath = gamePath.wstring();
            return game;
        }

        std::wstring GetGameIdFromUrlProtocolPath(const std::wstring& urlPath)
        {
            const std::wstring steamGamePrefix = L"steam://rungameid/";

            if (urlPath.rfind(steamGamePrefix, 0) == 0)
            {
                return urlPath.substr(steamGamePrefix.length());
            }

            return L"";
        }

    }
}