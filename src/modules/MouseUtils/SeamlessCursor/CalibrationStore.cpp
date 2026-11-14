// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CalibrationStore.h"
#include "ModuleConstants.h"

#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/json.h>

#include <cmath>
#include <string>
#include <string_view>
#include <winrt/base.h>

namespace seamless_cursor
{
    namespace
    {
        constexpr wchar_t fileName[] = L"\\calibration.json";
        constexpr wchar_t temporaryFileName[] = L"\\calibration.json.tmp";
        constexpr wchar_t backupFileName[] = L"\\calibration.json.bak";
        constexpr wchar_t preferencesFileName[] = L"\\overlay.json";
        constexpr wchar_t jsonTryWhileAdjusting[] = L"tryWhileAdjusting";
        constexpr wchar_t jsonShowEdgeMap[] = L"showEdgeMap";
        constexpr wchar_t jsonShowTrail[] = L"showTrail";
        constexpr double fileVersion = 1;

        constexpr wchar_t jsonVersion[] = L"version";
        constexpr wchar_t jsonBorders[] = L"borders";
        constexpr wchar_t jsonMonitorA[] = L"monitorA";
        constexpr wchar_t jsonMonitorB[] = L"monitorB";
        constexpr wchar_t jsonEdgeOfA[] = L"edgeOfA";
        constexpr wchar_t jsonEnabled[] = L"enabled";
        constexpr wchar_t jsonLandscapeA[] = L"landscapeA";
        constexpr wchar_t jsonLandscapeB[] = L"landscapeB";
        constexpr wchar_t jsonGlue[] = L"glue";
        constexpr wchar_t jsonPairs[] = L"pairs";
        constexpr wchar_t jsonA[] = L"a";
        constexpr wchar_t jsonB[] = L"b";

        constexpr std::array<const wchar_t*, edgeCount> edgeNames{ L"left", L"right", L"top", L"bottom" };

        constexpr int fileAttempts = 5;
        constexpr int longFileAttempts = 20; // About a second.
        constexpr DWORD fileRetryDelayMs = 50;
        constexpr LONGLONG maxFileSize = 1024 * 1024; // Far more than any real calibration.

        // Another program (Settings, an antivirus scanner) may hold a file for a moment, which makes opening, copying,
        // replacing or deleting it fail; those are tried again briefly. GetLastError() keeps the last attempt's error.
        template<typename Operation>
        bool RetryWhileInUse(Operation operation, int attempts = fileAttempts)
        {
            for (int attempt = 1;; ++attempt)
            {
                if (operation())
                {
                    return true;
                }

                const DWORD error = GetLastError();
                if ((error != ERROR_SHARING_VIOLATION && error != ERROR_ACCESS_DENIED) || attempt >= attempts)
                {
                    return false;
                }

                Sleep(fileRetryDelayMs);
            }
        }

        std::wstring FolderPath()
        {
            return PTSettingsHelper::get_module_save_folder_location(moduleKey);
        }

        std::optional<Edge> ParseEdge(std::wstring_view name)
        {
            for (size_t i = 0; i < edgeNames.size(); ++i)
            {
                if (name == edgeNames[i])
                {
                    return static_cast<Edge>(i);
                }
            }

            return std::nullopt;
        }

        constexpr bool IsFraction(double value) noexcept
        {
            return value >= 0.0 && value <= 1.0; // False for NaN too.
        }

        std::optional<BorderCalibration> ParseBorder(const json::JsonObject& border)
        {
            const auto edge = ParseEdge(border.GetNamedString(jsonEdgeOfA).c_str());
            const auto pairs = border.GetNamedArray(jsonPairs);
            if (!edge || pairs.Size() != 2)
            {
                return std::nullopt;
            }

            BorderCalibration calibration;
            calibration.monitorA = border.GetNamedString(jsonMonitorA).c_str();
            calibration.monitorB = border.GetNamedString(jsonMonitorB).c_str();
            calibration.edgeOfA = *edge;
            calibration.enabled = border.GetNamedBoolean(jsonEnabled, true);
            calibration.landscapeA = border.GetNamedBoolean(jsonLandscapeA, true);
            calibration.landscapeB = border.GetNamedBoolean(jsonLandscapeB, true);
            calibration.glue = border.GetNamedBoolean(jsonGlue, false);
            for (uint32_t i = 0; i < pairs.Size(); ++i)
            {
                const auto pair = pairs.GetObjectAt(i);
                calibration.pairs[i] = { pair.GetNamedNumber(jsonA), pair.GetNamedNumber(jsonB) };

                // Positions off the edge (a hand-edited file) would put lines where they cannot be grabbed.
                if (!IsFraction(calibration.pairs[i].a) || !IsFraction(calibration.pairs[i].b))
                {
                    return std::nullopt;
                }
            }

            return calibration;
        }

        json::JsonObject SerializeBorder(const BorderCalibration& calibration)
        {
            json::JsonArray pairs;
            for (const auto& pair : calibration.pairs)
            {
                json::JsonObject value;
                value.SetNamedValue(jsonA, json::value(pair.a));
                value.SetNamedValue(jsonB, json::value(pair.b));
                pairs.Append(value);
            }

            json::JsonObject border;
            border.SetNamedValue(jsonMonitorA, json::value(calibration.monitorA));
            border.SetNamedValue(jsonMonitorB, json::value(calibration.monitorB));
            border.SetNamedValue(jsonEdgeOfA, json::value(edgeNames[static_cast<size_t>(calibration.edgeOfA)]));
            border.SetNamedValue(jsonEnabled, json::value(calibration.enabled));
            border.SetNamedValue(jsonLandscapeA, json::value(calibration.landscapeA));
            border.SetNamedValue(jsonLandscapeB, json::value(calibration.landscapeB));
            border.SetNamedValue(jsonGlue, json::value(calibration.glue));
            border.SetNamedValue(jsonPairs, pairs);
            return border;
        }
    }

    namespace
    {
        enum class ReadState
        {
            Read, // Also when there is no file.
            InUse, // Cannot be opened or read now; usually passes.
            Corrupt, // Cannot be parsed; does not pass by itself.
        };

        struct ReadResult
        {
            ReadState state = ReadState::Read;
            std::vector<BorderCalibration> calibrations;
        };

        // Reads through one handle, waiting only while another program holds the file, and parses what it read once.
        ReadResult ReadCalibrations(int attempts)
        {
            ReadResult result;
            std::string content;
            try
            {
                const auto path = FolderPath() + fileName;
                winrt::file_handle file;
                if (!RetryWhileInUse(
                        [&]() {
                            file.attach(CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
                            return static_cast<bool>(file);
                        },
                        attempts))
                {
                    const DWORD error = GetLastError();
                    if (error != ERROR_FILE_NOT_FOUND && error != ERROR_PATH_NOT_FOUND)
                    {
                        Logger::error(L"Failed to open the Seamless Multi-Screen Cursor calibration file. Error: {}", error);
                        result.state = ReadState::InUse;
                    }

                    return result;
                }

                LARGE_INTEGER size{};
                if (!GetFileSizeEx(file.get(), &size))
                {
                    Logger::error(L"Failed to read the Seamless Multi-Screen Cursor calibration file. Error: {}", GetLastError());
                    result.state = ReadState::InUse;
                    return result;
                }

                if (size.QuadPart > maxFileSize)
                {
                    Logger::error("The Seamless Multi-Screen Cursor calibration file is too large.");
                    result.state = ReadState::Corrupt;
                    return result;
                }

                content.resize(static_cast<size_t>(size.QuadPart));
                DWORD read = 0;
                if (!content.empty())
                {
                    const BOOL readDone = ReadFile(file.get(), content.data(), static_cast<DWORD>(content.size()), &read, nullptr);
                    if (!readDone || read != content.size())
                    {
                        const DWORD error = readDone ? ERROR_SUCCESS : GetLastError();
                        Logger::error(L"Failed to read the Seamless Multi-Screen Cursor calibration file ({} of {} bytes). Error: {}", read, content.size(), error);
                        result.state = ReadState::InUse;
                        return result;
                    }
                }

                // Editors may save UTF-8 with a byte order mark, which the JSON parser rejects.
                constexpr std::string_view byteOrderMark = "\xEF\xBB\xBF";
                if (content.starts_with(byteOrderMark))
                {
                    content.erase(0, byteOrderMark.size());
                }
            }
            catch (...)
            {
                Logger::error("Failed to read the Seamless Multi-Screen Cursor calibration file.");
                result.state = ReadState::InUse;
                return result;
            }

            try
            {
                json::JsonObject root;
                if (!json::JsonObject::TryParse(winrt::to_hstring(content), root))
                {
                    Logger::error("Failed to parse the Seamless Multi-Screen Cursor calibration file.");
                    result.state = ReadState::Corrupt;
                    return result;
                }

                const auto borders = root.GetNamedArray(jsonBorders);
                for (uint32_t i = 0; i < borders.Size(); ++i)
                {
                    // One malformed entry must not cost the others.
                    try
                    {
                        if (auto calibration = ParseBorder(borders.GetObjectAt(i)))
                        {
                            result.calibrations.push_back(std::move(*calibration));
                            continue;
                        }
                    }
                    catch (...)
                    {
                    }

                    Logger::warn(L"Skipped an invalid entry in the Seamless Multi-Screen Cursor calibration file.");
                }
            }
            catch (...)
            {
                Logger::error("Failed to parse the Seamless Multi-Screen Cursor calibration file.");
                result.state = ReadState::Corrupt;
                result.calibrations.clear();
            }

            return result;
        }

        std::optional<std::vector<BorderCalibration>> LoadIfAvailable(int attempts)
        {
            auto result = ReadCalibrations(attempts);
            if (result.state == ReadState::InUse)
            {
                return std::nullopt;
            }

            return std::move(result.calibrations);
        }
    }

    std::optional<std::vector<BorderCalibration>> LoadCalibrations()
    {
        return LoadIfAvailable(longFileAttempts);
    }

    std::optional<std::vector<BorderCalibration>> TryLoadCalibrations()
    {
        return LoadIfAvailable(1);
    }

    std::optional<std::vector<BorderCalibration>> LoadCalibrationsForSave()
    {
        auto result = ReadCalibrations(fileAttempts);
        if (result.state == ReadState::Read)
        {
            return std::move(result.calibrations);
        }

        if (result.state == ReadState::InUse)
        {
            return std::nullopt;
        }

        // The save replaces a file that cannot be parsed, so keep a copy for recovery by hand.
        try
        {
            const auto folder = FolderPath();
            const auto path = folder + fileName;
            const auto backupPath = folder + backupFileName;
            if (RetryWhileInUse([&]() { return CopyFileW(path.c_str(), backupPath.c_str(), FALSE) != FALSE; }))
            {
                return std::vector<BorderCalibration>{};
            }

            Logger::error(L"Failed to back up the corrupt Seamless Multi-Screen Cursor calibration file. Error: {}", GetLastError());
        }
        catch (...)
        {
            Logger::error("Failed to back up the corrupt Seamless Multi-Screen Cursor calibration file.");
        }

        return std::nullopt;
    }

    bool SaveCalibrations(const std::vector<BorderCalibration>& calibrations)
    {
        try
        {
            json::JsonArray borders;
            for (const auto& calibration : calibrations)
            {
                borders.Append(SerializeBorder(calibration));
            }

            json::JsonObject root;
            root.SetNamedValue(jsonVersion, json::value(fileVersion));
            root.SetNamedValue(jsonBorders, borders);

            // Write a temporary file first and replace the calibration only after a complete write, so a failed
            // write never leaves a truncated calibration behind. The data is flushed before the rename, because the
            // rename can reach the disk first, and a power loss in between would leave an empty file.
            const auto folder = FolderPath();
            const auto temporaryPath = folder + temporaryFileName;
            const std::string content = winrt::to_string(root.Stringify());
            winrt::file_handle file;
            if (!RetryWhileInUse([&]() {
                    file.attach(CreateFileW(temporaryPath.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr));
                    return static_cast<bool>(file);
                }))
            {
                Logger::error(L"Failed to create the temporary Seamless Multi-Screen Cursor calibration file. Error: {}", GetLastError());
                return false;
            }

            DWORD written = 0;
            const bool complete = WriteFile(file.get(), content.data(), static_cast<DWORD>(content.size()), &written, nullptr) &&
                                  written == content.size() && FlushFileBuffers(file.get());
            const DWORD writeError = complete ? ERROR_SUCCESS : GetLastError();
            file.close();
            if (!complete)
            {
                DeleteFileW(temporaryPath.c_str());
                Logger::error(L"Failed to write the Seamless Multi-Screen Cursor calibration ({} of {} bytes). Error: {}", written, content.size(), writeError);
                return false;
            }

            const auto path = folder + fileName;
            if (!RetryWhileInUse([&]() { return MoveFileExW(temporaryPath.c_str(), path.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH) != FALSE; }))
            {
                const DWORD moveError = GetLastError();
                DeleteFileW(temporaryPath.c_str());
                Logger::error(L"Failed to save the Seamless Multi-Screen Cursor calibration. Error: {}", moveError);
                return false;
            }

            return true;
        }
        catch (...)
        {
            Logger::error("Failed to save the Seamless Multi-Screen Cursor calibration.");
            return false;
        }
    }

    OverlayPreferences LoadOverlayPreferences()
    {
        OverlayPreferences preferences;
        try
        {
            if (const auto root = json::from_file(FolderPath() + preferencesFileName))
            {
                preferences.tryWhileAdjusting = root->GetNamedBoolean(jsonTryWhileAdjusting, preferences.tryWhileAdjusting);
                preferences.showEdgeMap = root->GetNamedBoolean(jsonShowEdgeMap, preferences.showEdgeMap);
                preferences.showTrail = root->GetNamedBoolean(jsonShowTrail, preferences.showTrail);
            }
        }
        catch (...)
        {
            return {};
        }

        return preferences;
    }

    void SaveOverlayPreferences(const OverlayPreferences& preferences)
    {
        try
        {
            json::JsonObject root;
            root.SetNamedValue(jsonTryWhileAdjusting, json::value(preferences.tryWhileAdjusting));
            root.SetNamedValue(jsonShowEdgeMap, json::value(preferences.showEdgeMap));
            root.SetNamedValue(jsonShowTrail, json::value(preferences.showTrail));
            json::to_file(FolderPath() + preferencesFileName, root);
        }
        catch (...)
        {
            Logger::error("Failed to save the Seamless Multi-Screen Cursor overlay preferences.");
        }
    }

    bool DeleteCalibrations()
    {
        try
        {
            const auto path = FolderPath() + fileName;
            if (!RetryWhileInUse([&]() { return DeleteFileW(path.c_str()) != FALSE; }) && GetLastError() != ERROR_FILE_NOT_FOUND)
            {
                Logger::error(L"Failed to delete the Seamless Multi-Screen Cursor calibration. Error: {}", GetLastError());
                return false;
            }

            return true;
        }
        catch (...)
        {
            Logger::error("Failed to delete the Seamless Multi-Screen Cursor calibration.");
            return false;
        }
    }
}
