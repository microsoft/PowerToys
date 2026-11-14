// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <Windows.h>
#include <array>
#include <common/utils/json.h>
#include <cwchar>
#include <filesystem>
#include <fstream>
#include <objbase.h>
#include <regex>
#include <string>
#include <string_view>
#include <utility>
#include <wil/resource.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>

namespace diagnostic_privacy
{
    constexpr std::wstring_view report_prefix = L"PowerToys-BugReport-";
    constexpr std::string_view report_owner_signature = "PowerToys.BugReportTool.Stage.v1";

    struct report_directory
    {
        std::filesystem::path directory;
        std::filesystem::path ownerFile;
        wil::unique_handle ownership;
    };

    inline bool remove_report_directory(report_directory& report)
    {
        std::error_code error;
        std::filesystem::remove_all(report.directory, error);
        report.ownership.reset();
        if (error)
        {
            return false;
        }
        std::filesystem::remove(report.ownerFile, error);
        return !error;
    }

    inline void reclaim_abandoned_reports(const std::filesystem::path& temporaryDirectory)
    {
        for (const auto& entry : std::filesystem::directory_iterator{ temporaryDirectory })
        {
            if (entry.path().extension() != L".owner")
            {
                continue;
            }
            const auto name = entry.path().stem().wstring();
            if (!name.starts_with(report_prefix))
            {
                continue;
            }
            try
            {
                static_cast<void>(winrt::guid{ std::wstring_view{ name }.substr(report_prefix.size()) });
            }
            catch (...)
            {
                continue;
            }
            const auto attributes = GetFileAttributesW(entry.path().c_str());
            if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT)))
            {
                continue;
            }
            // An exclusive marker handle stays open for the entire collection, including ZIP creation.
            wil::unique_handle ownership{ CreateFileW(entry.path().c_str(), GENERIC_READ, 0, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr) };
            if (!ownership)
            {
                continue;
            }
            LARGE_INTEGER length{};
            std::array<char, report_owner_signature.size()> signature{};
            DWORD read{};
            if (!GetFileSizeEx(ownership.get(), &length) || length.QuadPart != static_cast<LONGLONG>(signature.size()) ||
                !::ReadFile(ownership.get(), signature.data(), static_cast<DWORD>(signature.size()), &read, nullptr) ||
                read != signature.size() || std::string_view{ signature.data(), signature.size() } != report_owner_signature)
            {
                continue;
            }
            auto directory = entry.path();
            directory.replace_extension();
            const auto directoryAttributes = GetFileAttributesW(directory.c_str());
            if (directoryAttributes != INVALID_FILE_ATTRIBUTES && (directoryAttributes & FILE_ATTRIBUTE_REPARSE_POINT))
            {
                continue;
            }
            report_directory abandoned{ std::move(directory), entry.path(), std::move(ownership) };
            remove_report_directory(abandoned);
        }
    }

    inline report_directory create_report_directory(const std::filesystem::path& temporaryDirectory, const std::filesystem::path& settingsDirectory)
    {
        try
        {
            reclaim_abandoned_reports(temporaryDirectory);
        }
        catch (const std::filesystem::filesystem_error&)
        {
            // Recovery is best effort; a directory-listing failure must not block a new collection.
        }
        GUID id{};
        winrt::check_hresult(CoCreateGuid(&id));
        const auto reportDirectory = temporaryDirectory / (std::wstring{ report_prefix } + winrt::to_hstring(winrt::guid{ id }).c_str());
        // Existing report writers concatenate filenames and require a trailing separator.
        auto directory = reportDirectory / L"";
        std::filesystem::path ownerFile{ reportDirectory.wstring() + L".owner" };
        wil::unique_handle ownership{ CreateFileW(ownerFile.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr) };
        winrt::check_bool(ownership.is_valid());
        const auto cleanupMarker = wil::scope_exit([&]() {
            if (ownership)
            {
                ownership.reset();
                std::error_code error;
                std::filesystem::remove(ownerFile, error);
            }
        });
        DWORD written{};
        winrt::check_bool(::WriteFile(ownership.get(), report_owner_signature.data(), static_cast<DWORD>(report_owner_signature.size()), &written, nullptr));
        if (written != report_owner_signature.size())
        {
            throw std::ios_base::failure{ "Incomplete report ownership marker" };
        }
        if (!std::filesystem::create_directory(reportDirectory, settingsDirectory))
        {
            throw std::filesystem::filesystem_error{ "Report directory already exists", reportDirectory, std::make_error_code(std::errc::file_exists) };
        }
        return { std::move(directory), std::move(ownerFile), std::move(ownership) };
    }

    enum class log_source
    {
        none,
        runner,
        mwb
    };

    inline bool is_utf8_text(const std::string& contents)
    {
        return contents.find('\0') == std::string::npos &&
               MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, contents.c_str(), -1, nullptr, 0) != 0;
    }

    inline bool redact_security_keys(std::string& contents)
    {
        if (!is_utf8_text(contents))
        {
            return false;
        }

        const bool hasBom = contents.starts_with("\xef\xbb\xbf");
        winrt::Windows::Data::Json::JsonValue value{ nullptr };
        if (!winrt::Windows::Data::Json::JsonValue::TryParse(
                winrt::to_hstring(contents.substr(hasBom ? 3 : 0)), value) ||
            value.ValueType() !=
                winrt::Windows::Data::Json::JsonValueType::Object)
        {
            return false;
        }
        if (json::redact_security_keys(value))
        {
            contents = (hasBom ? "\xef\xbb\xbf" : "") +
                       winrt::to_string(value.Stringify());
        }
        return true;
    }

    inline bool redact_log_security_keys(std::string& contents, log_source source)
    {
        size_t lineStart = contents.starts_with("\xef\xbb\xbf") ? 3 : 0;
        if (lineStart == contents.size())
        {
            return true;
        }

        static const std::regex runnerHeader{ R"(^\[[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{6}\] \[p-[0-9]+\] \[t-[0-9]+\] \[(trace|debug|info|warning|error|critical|off)\] )" };
        static const std::regex managedHeader{ R"(^\[[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?\] \[(Trace|Debug|Info|Warning|Error)\] )" };
        constexpr std::string_view dispatchPrefix = "dispatch_received_json:";
        const auto startsRecord = [&](std::string_view line) {
            return std::regex_search(line.begin(), line.end(), runnerHeader, std::regex_constants::match_continuous) ||
                   std::regex_search(line.begin(), line.end(), managedHeader, std::regex_constants::match_continuous);
        };

        if (contents.back() != '\n')
        {
            size_t lastRecordStart = std::string::npos;
            for (auto start = lineStart; start < contents.size();)
            {
                const auto end = contents.find('\n', start);
                const auto line = std::string_view{ contents }.substr(start, end == std::string::npos ? std::string::npos : end - start);
                if (startsRecord(line))
                {
                    lastRecordStart = start;
                }
                if (end == std::string::npos)
                {
                    break;
                }
                start = end + 1;
            }
            if (lastRecordStart == std::string::npos || lastRecordStart == lineStart)
            {
                return false;
            }
            // A snapshot can stop mid-message or mid-character. Drop its final header and body, not the complete prefix.
            contents.resize(lastRecordStart);
        }
        if (!is_utf8_text(contents))
        {
            return false;
        }
        if (source == log_source::mwb)
        {
            // Managed message bodies are free-form; reject known key-bearing markers without parsing their layout.
            return contents.find("GETSECKEY:") == std::string::npos &&
                   contents.find(dispatchPrefix) == std::string::npos;
        }

        std::string sanitized;
        size_t copyFrom = 0;
        bool sawHeader = false;
        while (lineStart < contents.size())
        {
            const auto lineEnd = contents.find('\n', lineStart);
            auto line = std::string_view{ contents }.substr(lineStart, lineEnd - lineStart);
            if (line.ends_with('\r'))
            {
                line.remove_suffix(1);
            }
            std::match_results<std::string_view::const_iterator> header;
            const bool isRunnerRecord = std::regex_search(line.begin(), line.end(), header, runnerHeader, std::regex_constants::match_continuous);
            const bool isRecord = isRunnerRecord || startsRecord(line);
            const auto firstText = line.find_first_not_of(" \t\r");
            const bool isBlank = firstText == std::string_view::npos;
            const bool isContinuation = sawHeader && !isBlank &&
                                        (((line.starts_with(' ') || line.starts_with('\t')) && line[firstText] != '[') ||
                                         line == "--- End of stack trace from previous location ---");
            // Message and stack-trace continuations are valid, but an unknown header is not.
            if (!isRecord && !isBlank && !isContinuation)
            {
                return false;
            }
            sawHeader = sawHeader || isRecord;

            auto message = isRunnerRecord ? line.substr(header.length()) : line;
            if (isRunnerRecord && message.starts_with(dispatchPrefix))
            {
                if (message == "dispatch_received_json: received settings message" ||
                    message == "dispatch_received_json: got malformed json")
                {
                    lineStart = lineEnd + 1;
                    continue;
                }
                if (message.starts_with("dispatch_received_json: got malformed json:"))
                {
                    return false;
                }

                const auto payloadStart = lineStart + header.length() + dispatchPrefix.size();
                auto recordEnd = lineEnd + 1;
                while (recordEnd < contents.size())
                {
                    const auto nextEnd = contents.find('\n', recordEnd);
                    const auto nextLine = std::string_view{ contents }.substr(recordEnd, nextEnd - recordEnd);
                    if (startsRecord(nextLine))
                    {
                        break;
                    }
                    recordEnd = nextEnd + 1;
                }
                // A candidate header alone is not a safe boundary: the entire Runner payload must be a JSON object.
                winrt::Windows::Data::Json::JsonObject json{ nullptr };
                if (!winrt::Windows::Data::Json::JsonObject::TryParse(winrt::to_hstring(contents.substr(payloadStart, recordEnd - payloadStart)), json))
                {
                    return false;
                }
                sanitized.append(contents, copyFrom, lineStart - copyFrom);
                copyFrom = recordEnd;
                lineStart = recordEnd;
                continue;
            }
            else if (line.find(dispatchPrefix) != std::string_view::npos ||
                     line.find("GETSECKEY:") != std::string_view::npos)
            {
                return false;
            }
            lineStart = lineEnd + 1;
        }
        if (copyFrom != 0)
        {
            sanitized.append(contents, copyFrom, std::string::npos);
            contents = std::move(sanitized);
        }
        return true;
    }

    inline log_source get_log_source(const std::filesystem::path& relativePath)
    {
        auto source = log_source::none;
        for (auto directory = relativePath.parent_path(); !directory.empty(); directory = directory.parent_path())
        {
            const auto name = directory.filename().wstring();
            if (_wcsicmp(name.c_str(), L"RunnerLogs") == 0)
            {
                return log_source::runner;
            }
            const auto moduleName = directory.parent_path().filename().wstring();
            if (_wcsicmp(name.c_str(), L"Logs") == 0 &&
                (_wcsicmp(moduleName.c_str(), L"MouseWithoutBorders") == 0 ||
                 _wcsicmp(moduleName.c_str(), L"MouseWithoutBorders.backup") == 0))
            {
                source = log_source::mwb;
            }
        }
        return source;
    }

    inline bool is_mwb_settings_path(const std::filesystem::path& relativePath)
    {
        if (_wcsicmp(relativePath.filename().c_str(), L"settings.json") != 0)
        {
            return false;
        }
        for (auto directory = relativePath.parent_path(); !directory.empty(); directory = directory.parent_path())
        {
            const auto name = directory.filename().wstring();
            if (_wcsicmp(name.c_str(), L"MouseWithoutBorders") == 0 ||
                _wcsicmp(name.c_str(), L"MouseWithoutBorders.backup") == 0)
            {
                return true;
            }
        }
        return false;
    }

    inline void sanitize_report(const std::filesystem::path& reportDirectory)
    {
        for (const auto& entry : std::filesystem::recursive_directory_iterator{ reportDirectory })
        {
            if (!entry.is_regular_file())
            {
                continue;
            }

            const auto relativePath = entry.path().lexically_relative(reportDirectory);
            const auto source = get_log_source(relativePath);
            const bool isSettings = is_mwb_settings_path(relativePath);
            if (source == log_source::none && !isSettings)
            {
                continue;
            }

            std::ifstream input{ entry.path(), std::ios::binary | std::ios::ate };
            input.exceptions(std::ios::failbit | std::ios::badbit);
            const auto length = static_cast<std::streamoff>(input.tellg());
            if (length < 0 || !std::in_range<size_t>(length) || !std::in_range<std::streamsize>(length))
            {
                throw std::ios_base::failure{ "Invalid report file length" };
            }
            input.seekg(0);
            std::string contents(static_cast<size_t>(length), '\0');
            // Checked reads must not turn a mid-file I/O failure into an apparently complete safe prefix.
            input.read(contents.data(), static_cast<std::streamsize>(length));
            input.close();

            const auto originalContents = contents;
            const bool sanitizeLog = source == log_source::runner || (source == log_source::mwb && !isSettings);
            const bool safe = (!sanitizeLog || redact_log_security_keys(contents, source)) &&
                              (!isSettings || redact_security_keys(contents));
            if (safe && contents == originalContents)
            {
                continue;
            }

            // Copies retain source attributes; only the staged files needing changes must be writable.
            const auto attributes = GetFileAttributesW(entry.path().c_str());
            winrt::check_bool(attributes != INVALID_FILE_ATTRIBUTES);
            if (attributes & FILE_ATTRIBUTE_READONLY)
            {
                winrt::check_bool(SetFileAttributesW(entry.path().c_str(), attributes & ~FILE_ATTRIBUTE_READONLY));
            }
            if (!safe)
            {
                std::filesystem::remove(entry.path());
                continue;
            }

            std::ofstream output{ entry.path(), std::ios::binary | std::ios::trunc };
            output.exceptions(std::ios::failbit | std::ios::badbit);
            output << contents;
            output.close();
        }
    }
}
