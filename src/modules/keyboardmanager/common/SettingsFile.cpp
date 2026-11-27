#include "pch.h"
#include "SettingsFile.h"

#include <fstream>

#include "KeyboardManagerConstants.h"

namespace
{
    enum class ReplaceResult
    {
        Done,

        // Another program has the target open (or it is denied for the moment): worth trying again.
        Busy,

        Failed,
    };

    constexpr int ReplaceAttempts = 10;
    constexpr DWORD ReplaceRetryDelayMs = 50;

    // One attempt. Leaves no temporary file behind unless it returns Done (then it is the new file).
    ReplaceResult TryReplaceJsonOnce(const std::wstring& filePath, const json::JsonObject& object) noexcept
    {
        const std::wstring temporaryPath = filePath + L"." + std::to_wstring(GetCurrentProcessId()) + L"." + std::to_wstring(GetCurrentThreadId()) + L".tmp";
        ReplaceResult result = ReplaceResult::Failed;

        try
        {
            {
                std::ofstream stream{ temporaryPath, std::ios::binary | std::ios::trunc };
                stream.exceptions(std::ios::failbit | std::ios::badbit);

                const std::string serialized = winrt::to_string(object.Stringify());
                stream.write(serialized.data(), static_cast<std::streamsize>(serialized.size()));
                stream.flush();
                stream.close();
            }

            if (MoveFileExW(temporaryPath.c_str(), filePath.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
            {
                return ReplaceResult::Done;
            }

            // A reader that opened the target without delete sharing (the editor and the Settings app
            // read it that way) fails the rename with a sharing violation until it closes the file.
            // Access denied can be the same thing, but for a read-only target it is permanent.
            const DWORD error = GetLastError();
            if (error == ERROR_SHARING_VIOLATION)
            {
                result = ReplaceResult::Busy;
            }
            else if (error == ERROR_ACCESS_DENIED)
            {
                const DWORD attributes = GetFileAttributesW(filePath.c_str());
                const bool readOnly = attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_READONLY) != 0;
                result = readOnly ? ReplaceResult::Failed : ReplaceResult::Busy;
            }
        }
        catch (...)
        {
            result = ReplaceResult::Failed;
        }

        DeleteFileW(temporaryPath.c_str());
        return result;
    }
}

namespace KeyboardManagerSettingsFile
{
    bool WriteJsonAtomically(const std::wstring& filePath, const json::JsonObject& object)
    {
        for (int attempt = 1;; ++attempt)
        {
            const ReplaceResult result = TryReplaceJsonOnce(filePath, object);
            if (result != ReplaceResult::Busy || attempt >= ReplaceAttempts)
            {
                return result == ReplaceResult::Done;
            }

            Sleep(ReplaceRetryDelayMs);
        }
    }

    bool WriteActiveConfiguration(const std::wstring& settingsPath, const std::wstring& profile)
    {
        try
        {
            for (int attempt = 1;; ++attempt)
            {
                // Read again on every attempt: what is written must be the file as it is now, with only
                // the active profile changed.
                const auto parsed = json::from_file(settingsPath);
                if (!parsed.has_value())
                {
                    return false;
                }

                json::JsonObject root = parsed.value();
                json::JsonObject properties = root.HasKey(L"properties") ? root.GetNamedObject(L"properties") : json::JsonObject{};

                json::JsonObject activeConfiguration;
                activeConfiguration.SetNamedValue(L"value", json::JsonValue::CreateStringValue(profile));
                properties.SetNamedValue(KeyboardManagerConstants::ActiveConfigurationSettingName, activeConfiguration);
                root.SetNamedValue(L"properties", properties);

                const ReplaceResult result = TryReplaceJsonOnce(settingsPath, root);
                if (result != ReplaceResult::Busy || attempt >= ReplaceAttempts)
                {
                    return result == ReplaceResult::Done;
                }

                Sleep(ReplaceRetryDelayMs);
            }
        }
        catch (...)
        {
            return false;
        }
    }

    bool IsSameProfile(const std::wstring& left, const std::wstring& right) noexcept
    {
        return CompareStringOrdinal(left.c_str(), static_cast<int>(left.size()), right.c_str(), static_cast<int>(right.size()), TRUE) == CSTR_EQUAL;
    }
}
