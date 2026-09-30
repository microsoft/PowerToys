#include "PowerScriptsRunner.h"

#include <algorithm>
#include <filesystem>
#include <map>
#include <optional>
#include <string>
#include <vector>
#include <windows.h>

#include <logger.h>

namespace
{
    constexpr auto HostExecutableName = L"PowerScripts.Host.exe";
    constexpr auto HostEnvironmentVariable = L"POWERSCRIPTS_HOST";
    constexpr char Base64Alphabet[] =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    bool valid_script_id(const std::wstring& scriptId)
    {
        return !scriptId.empty() &&
               std::ranges::all_of(scriptId, [](const wchar_t character) {
                   return (character >= L'A' && character <= L'Z') ||
                          (character >= L'a' && character <= L'z') ||
                          (character >= L'0' && character <= L'9') ||
                          character == L'.' ||
                          character == L'_' ||
                          character == L'-';
               });
    }

    std::optional<std::filesystem::path> existing_file(const std::filesystem::path& path)
    {
        std::error_code error;
        if (std::filesystem::is_regular_file(path, error))
        {
            return path;
        }

        return std::nullopt;
    }

    std::optional<std::filesystem::path> environment_path(const wchar_t* name)
    {
        const auto length = GetEnvironmentVariableW(name, nullptr, 0);
        if (length == 0)
        {
            return std::nullopt;
        }

        std::wstring value(length, L'\0');
        const auto copied = GetEnvironmentVariableW(name, value.data(), length);
        if (copied == 0 || copied >= length)
        {
            return std::nullopt;
        }

        value.resize(copied);
        return std::filesystem::path{ value };
    }

    std::optional<std::filesystem::path> executable_directory()
    {
        std::wstring path(MAX_PATH, L'\0');
        const auto length = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
        if (length == 0 || length >= path.size())
        {
            return std::nullopt;
        }

        path.resize(length);
        return std::filesystem::path{ path }.parent_path();
    }

    std::optional<std::filesystem::path> resolve_host_path()
    {
        if (const auto overridePath = environment_path(HostEnvironmentVariable))
        {
            if (const auto found = existing_file(*overridePath))
            {
                return found;
            }
        }

        std::vector<std::filesystem::path> candidates;
        if (const auto directory = executable_directory())
        {
            candidates.push_back(*directory / HostExecutableName);
            candidates.push_back(*directory / L"PowerScripts" / HostExecutableName);

            auto ancestor = *directory;
            while (!ancestor.empty())
            {
                for (const auto configuration : { L"Debug", L"Release" })
                {
                    candidates.push_back(
                        ancestor / L"src" / L"modules" / L"PowerScripts" / L"PowerScripts.Host" /
                        L"bin" / configuration / L"net10.0-windows" / HostExecutableName);
                }

                const auto parent = ancestor.parent_path();
                if (parent == ancestor)
                {
                    break;
                }

                ancestor = parent;
            }
        }

        if (const auto localAppData = environment_path(L"LOCALAPPDATA"))
        {
            candidates.push_back(
                *localAppData / L"Microsoft" / L"PowerToys" / L"PowerScripts" / HostExecutableName);
        }

        for (const auto& candidate : candidates)
        {
            if (const auto found = existing_file(candidate))
            {
                return found;
            }
        }

        return std::nullopt;
    }

    std::string to_utf8(const std::wstring& value)
    {
        if (value.empty())
        {
            return {};
        }

        const auto length = WideCharToMultiByte(
            CP_UTF8,
            WC_ERR_INVALID_CHARS,
            value.data(),
            static_cast<int>(value.size()),
            nullptr,
            0,
            nullptr,
            nullptr);
        if (length == 0)
        {
            return {};
        }

        std::string result(length, '\0');
        if (WideCharToMultiByte(
                CP_UTF8,
                WC_ERR_INVALID_CHARS,
                value.data(),
                static_cast<int>(value.size()),
                result.data(),
                length,
                nullptr,
                nullptr) == 0)
        {
            return {};
        }

        return result;
    }

    std::string base64_encode(const std::string& value)
    {
        std::string encoded;
        encoded.reserve(((value.size() + 2) / 3) * 4);

        for (size_t index = 0; index < value.size(); index += 3)
        {
            const auto first = static_cast<unsigned char>(value[index]);
            const auto second = index + 1 < value.size()
                                    ? static_cast<unsigned char>(value[index + 1])
                                    : 0;
            const auto third = index + 2 < value.size()
                                   ? static_cast<unsigned char>(value[index + 2])
                                   : 0;
            const auto block = (static_cast<unsigned int>(first) << 16) |
                               (static_cast<unsigned int>(second) << 8) |
                               third;

            encoded.push_back(Base64Alphabet[(block >> 18) & 0x3f]);
            encoded.push_back(Base64Alphabet[(block >> 12) & 0x3f]);
            encoded.push_back(index + 1 < value.size() ? Base64Alphabet[(block >> 6) & 0x3f] : '=');
            encoded.push_back(index + 2 < value.size() ? Base64Alphabet[block & 0x3f] : '=');
        }

        return encoded;
    }
}

void PowerScriptsRunner::RunAction(
    const std::wstring& scriptId,
    const std::map<std::wstring, std::wstring>& parameters)
{
    if (!valid_script_id(scriptId))
    {
        Logger::warn(L"[LightSwitchStateManager] Ignoring an invalid PowerScript id.");
        return;
    }

    const auto hostPath = resolve_host_path();
    if (!hostPath)
    {
        Logger::warn(L"[LightSwitchStateManager] PowerScripts host is unavailable.");
        return;
    }

    std::wstring commandLine =
        L"\"" + hostPath->wstring() + L"\" run \"" + scriptId + L"\" --no-consent";
    for (const auto& [name, value] : parameters)
    {
        auto payload = to_utf8(name);
        payload.push_back('\0');
        payload.append(to_utf8(value));

        const auto encoded = base64_encode(payload);
        commandLine.append(L" --set-base64 ");
        commandLine.append(encoded.begin(), encoded.end());
    }

    STARTUPINFOW startupInfo{ sizeof(startupInfo) };
    PROCESS_INFORMATION processInfo{};

    if (!CreateProcessW(
            hostPath->c_str(),
            commandLine.data(),
            nullptr,
            nullptr,
            FALSE,
            CREATE_NO_WINDOW,
            nullptr,
            hostPath->parent_path().c_str(),
            &startupInfo,
            &processInfo))
    {
        Logger::error(
            L"[LightSwitchStateManager] Failed to start PowerScript '{}' (error: {}).",
            scriptId,
            GetLastError());
        return;
    }

    CloseHandle(processInfo.hThread);
    CloseHandle(processInfo.hProcess);
    Logger::info(L"[LightSwitchStateManager] Started PowerScript '{}'.", scriptId);
}
