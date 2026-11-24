#include "pch.h"
#include "PwaHelper.h"
#include "WindowUtils.h"

#include <filesystem>
#include <string_view>

#include <appmodel.h>
#include <shellapi.h>
#include <ShlObj.h>
#include <shobjidl.h>
#include <tlhelp32.h>
#include <wrl.h>
#include <propkey.h>

#include <wil/com.h>

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

#include <WorkspacesLib/AppUtils.h>
#include <WorkspacesLib/CommandLineArgsHelper.h>
#include <WorkspacesLib/StringUtils.h>

namespace Utils
{
    namespace NonLocalizable
    {
        const std::wstring EdgeAppIdIdentifier = L"--app-id=";
        const std::wstring ChromeBase = L"Google\\Chrome\\User Data";
        const std::wstring EdgeBase = L"Microsoft\\Edge\\User Data\\Default\\Web Applications";
        const std::wstring ChromeDirPrefix = L"_crx_";
        const std::wstring EdgeDirPrefix = L"_crx__";
        const std::wstring IcoExtension = L".ico";
    }

    static const std::wstring& GetLocalAppDataFolder()
    {
        static std::wstring localFolder{};

        if (localFolder.empty())
        {
            wil::unique_cotaskmem_string folderPath;
            HRESULT hres = SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, NULL, &folderPath);
            if (SUCCEEDED(hres))
            {
                localFolder = folderPath.get();
            }
            else
            {
                Logger::error(L"Failed to get the local app data folder path: {}", get_last_error_or_default(hres));
                localFolder = L""; // Ensure it is explicitly set to empty on failure
            }
        }

        return localFolder;
    }

    // Finds all PwaHelper.exe processes with the specified parent process ID
    std::vector<DWORD> FindPwaHelperProcessIds()
    {
        std::vector<DWORD> pwaHelperProcessIds;
        const HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (hSnapshot == INVALID_HANDLE_VALUE)
        {
            Logger::info(L"Invalid handle when creating snapshot for the search for PwaHelper processes");
            return pwaHelperProcessIds;
        }

        PROCESSENTRY32 pe;
        pe.dwSize = sizeof(PROCESSENTRY32);

        if (Process32First(hSnapshot, &pe))
        {
            do
            {
                if (_wcsicmp(pe.szExeFile, L"PwaHelper.exe") == 0)
                {
                    Logger::info(L"Found a PWA process with id {}", pe.th32ProcessID);
                    pwaHelperProcessIds.push_back(pe.th32ProcessID);
                }
            } while (Process32Next(hSnapshot, &pe));
        }

        CloseHandle(hSnapshot);
        return pwaHelperProcessIds;
    }

    PwaHelper::PwaHelper() :
        PwaHelper(GetLocalAppDataFolder())
    {
    }

    PwaHelper::PwaHelper(std::filesystem::path localAppData) :
        m_localAppData(std::move(localAppData))
    {
    }

    namespace
    {
        bool MatchesChromeComponent(std::wstring_view original, std::wstring_view captured, size_t componentCount)
        {
            if (original == captured)
                return true;
            // Chromium's ShellUtil::BuildAppUserModelId splits a 64-character budget
            // among all components and keeps the ends of each overlong component.
            const size_t limit = (64 - (componentCount - 1)) / componentCount;
            const size_t first = limit / 2;
            const size_t last = limit - first;
            return original.size() > limit && captured.size() == limit &&
                   original.starts_with(captured.substr(0, first)) && original.ends_with(captured.substr(first, last));
        }

        std::wstring ChromeProfileId(const std::filesystem::path& directory)
        {
            std::wstring result = L"UserData.";
            for (const wchar_t c : directory.filename().wstring())
            {
                if ((c >= L'a' && c <= L'z') || (c >= L'A' && c <= L'Z') || (c >= L'0' && c <= L'9') || c == L'.')
                    result += c;
            }
            return result;
        }

        std::wstring IconName(const std::filesystem::path& directory)
        {
            std::error_code ec;
            for (std::filesystem::directory_iterator entry(directory, ec), end; !ec && entry != end; entry.increment(ec))
            {
                const auto& file = *entry;
                if (file.is_regular_file(ec) && StringUtils::CaseInsensitiveEquals(file.path().extension(), NonLocalizable::IcoExtension))
                    return file.path().stem().wstring();
            }
            return {};
        }

        bool MatchChromeProfile(const std::filesystem::path& profile, std::wstring_view appPart, size_t components,
                                std::optional<std::wstring>& match, std::wstring& name)
        {
            std::optional<std::wstring> profileMatch;
            const auto webApps = profile / L"Web Applications";
            for (const bool manifest : { false, true })
            {
                const auto folder = manifest ? webApps / L"Manifest Resources" : webApps;
                if (!std::filesystem::is_directory(folder))
                    continue;
                for (const auto& entry : std::filesystem::directory_iterator(folder))
                {
                    const std::wstring leaf = entry.path().filename().wstring();
                    if (!manifest && !leaf.starts_with(NonLocalizable::ChromeDirPrefix))
                        continue;
                    const auto id = manifest ? leaf : leaf.substr(NonLocalizable::ChromeDirPrefix.size());
                    if (id.size() != 32 || id.find_first_not_of(L"abcdefghijklmnop") != std::wstring::npos ||
                        !MatchesChromeComponent(NonLocalizable::ChromeDirPrefix + id, appPart, components) || !entry.is_directory())
                        continue;
                    // The same installation can be represented in both layouts. Different
                    // installed apps or profiles can collide after Chromium shortens their IDs.
                    if (match || (profileMatch && *profileMatch != id))
                        return false;
                    profileMatch = id;
                    if (!manifest)
                        name = IconName(entry.path());
                }
            }
            if (profileMatch)
                match = std::move(profileMatch);
            return true;
        }
    }

    void PwaHelper::InitEdgeAppIds()
    {
        m_edgeInitialized = true;
        CommandLineArgsHelper commandLineArgsHelper{};

        const auto pwaHelperProcessIds = FindPwaHelperProcessIds();
        Logger::info(L"Found {} edge Pwa helper processes", pwaHelperProcessIds.size());

        for (const auto subProcessID : pwaHelperProcessIds)
        {
            std::wstring aumidID = GetAUMIDFromProcessId(subProcessID);
            std::wstring commandLineArg = commandLineArgsHelper.GetCommandLineArgs(subProcessID);
            std::wstring appId = GetAppIdFromCommandLineArgs(commandLineArg);

            m_edgeAppIds.insert({ aumidID, appId });
            Logger::info(L"Found an edge Pwa helper process with AumidID {} and PwaAppId {}", aumidID, appId);
        }

    }

    std::optional<std::wstring> PwaHelper::GetEdgeAppId(const std::wstring& windowAumid)
    {
        if (windowAumid.empty())
            return std::nullopt;
        if (!m_edgeInitialized)
            InitEdgeAppIds();
        const auto pwaIndex = m_edgeAppIds.find(windowAumid);
        if (pwaIndex != m_edgeAppIds.end())
        {
            if (!m_localAppData.empty() && !m_pwaAppIdsToAppNames.contains(pwaIndex->second))
            {
                const auto& id = pwaIndex->second;
                if (!id.empty() && id.find_first_not_of(L"abcdefghijklmnop") == std::wstring::npos)
                {
                    try
                    {
                        const auto folder = m_localAppData / NonLocalizable::EdgeBase / (NonLocalizable::EdgeDirPrefix + id);
                        if (std::filesystem::is_directory(folder))
                            m_pwaAppIdsToAppNames.emplace(id, IconName(folder));
                    }
                    catch (const std::filesystem::filesystem_error& ex)
                    {
                        Logger::info("Could not read an Edge PWA name: {}", ex.what());
                    }
                }
            }
            return pwaIndex->second;
        }

        return std::nullopt;
    }

    std::optional<std::wstring> PwaHelper::GetChromeAppId(const std::wstring& windowAumid)
    {
        constexpr std::wstring_view browser = L"Chrome.";
        if (!windowAumid.starts_with(L"Chrome._crx_") || m_localAppData.empty())
            return std::nullopt;
        if (const auto known = m_chromeAppsByAumid.find(windowAumid); known != m_chromeAppsByAumid.end())
            return known->second.first;

        const std::wstring_view components = std::wstring_view(windowAumid).substr(browser.size());
        const auto split = components.find(L'.');
        const bool hasProfile = split != std::wstring_view::npos;
        const auto appPart = components.substr(0, split);
        const auto profilePart = hasProfile ? components.substr(split + 1) : std::wstring_view{};
        if (hasProfile && profilePart.empty())
            return std::nullopt;

        std::optional<std::wstring> match;
        std::wstring name;
        try
        {
            const auto root = m_localAppData / NonLocalizable::ChromeBase;
            if (!hasProfile)
            {
                if (!MatchChromeProfile(root / L"Default", appPart, 2, match, name))
                    return std::nullopt;
            }
            else if (std::filesystem::is_directory(root))
            {
                for (const auto& profile : std::filesystem::directory_iterator(root))
                {
                    if (!MatchesChromeComponent(ChromeProfileId(profile.path()), profilePart, 3) || !profile.is_directory())
                        continue;
                    if (!MatchChromeProfile(profile.path(), appPart, 3, match, name))
                        return std::nullopt;
                }
            }
        }
        catch (const std::filesystem::filesystem_error& ex)
        {
            Logger::info("Could not resolve a Chrome PWA: {}", ex.what());
            return std::nullopt;
        }
        if (match)
            m_chromeAppsByAumid.emplace(windowAumid, std::make_pair(*match, std::move(name)));
        return match;
    }

    std::wstring PwaHelper::SearchPwaName(const std::wstring& pwaAppId, const std::wstring& windowAumid) const
    {
        if (const auto chrome = m_chromeAppsByAumid.find(windowAumid); chrome != m_chromeAppsByAumid.end())
        {
            if (chrome->second.first == pwaAppId && !chrome->second.second.empty())
                return chrome->second.second;
        }
        else if (const auto edge = m_pwaAppIdsToAppNames.find(pwaAppId); edge != m_pwaAppIdsToAppNames.end() && !edge->second.empty())
            return edge->second;

        std::wstring nameFromAumid{ windowAumid };
        const std::size_t delimiterPos = nameFromAumid.find(L"-");
        if (delimiterPos != std::string::npos)
        {
            return nameFromAumid.substr(0, delimiterPos);
        }

        return nameFromAumid;
    }

    std::wstring PwaHelper::GetAppIdFromCommandLineArgs(const std::wstring& commandLineArgs) const
    {
        auto result = commandLineArgs;

        // remove the prefix
        if (result.find(NonLocalizable::EdgeAppIdIdentifier) == 0)
        {
            result.erase(0, NonLocalizable::EdgeAppIdIdentifier.length());
        }

        // remove the suffix
        auto appIdIndexEnd = result.find(L" ");
        if (appIdIndexEnd != std::wstring::npos)
        {
            result = result.substr(0, appIdIndexEnd);
        }

        return result;
    }
}
