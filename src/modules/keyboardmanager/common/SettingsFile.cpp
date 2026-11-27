#include "pch.h"
#include "SettingsFile.h"

#include <fstream>

#include "KeyboardManagerConstants.h"

namespace KeyboardManagerSettingsFile
{
    bool WriteJsonAtomically(const std::wstring& filePath, const json::JsonObject& object)
    {
        const std::wstring temporaryPath = filePath + L"." + std::to_wstring(GetCurrentProcessId()) + L"." + std::to_wstring(GetCurrentThreadId()) + L".tmp";

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

            // A reader that opened the target without delete sharing (the editor and the Settings app
            // read it that way) fails the rename with a sharing violation until it closes the file.
            constexpr int Attempts = 10;
            constexpr DWORD RetryDelayMs = 50;
            for (int attempt = 1;; ++attempt)
            {
                if (MoveFileExW(temporaryPath.c_str(), filePath.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
                {
                    return true;
                }

                const DWORD error = GetLastError();
                if (attempt >= Attempts || (error != ERROR_SHARING_VIOLATION && error != ERROR_ACCESS_DENIED))
                {
                    break;
                }

                Sleep(RetryDelayMs);
            }
        }
        catch (...)
        {
        }

        DeleteFileW(temporaryPath.c_str());
        return false;
    }

    bool WriteActiveConfiguration(const std::wstring& settingsPath, const std::wstring& profile)
    {
        try
        {
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

            return WriteJsonAtomically(settingsPath, root);
        }
        catch (...)
        {
            return false;
        }
    }
}
