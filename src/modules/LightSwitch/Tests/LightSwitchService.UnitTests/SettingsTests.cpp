// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <CppUnitTest.h>
#include "LightSwitchSettings.h"
#include "LightSwitchStateManager.h"
#include "TestSupport.h"
#include <fstream>
#include <future>
#include <vector>
#include <wil/resource.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace LightSwitchServiceUnitTests
{
    namespace
    {
        constexpr wchar_t ValidSettings[] = LR"({"name":"LightSwitch","version":"1.0","properties":{"scheduleMode":{"value":"FixedHours","retained":42},"changeSystem":{"value":true},"changeApps":{"value":false},"lightTime":{"value":480},"darkTime":{"value":1200},"futureProperty":{"value":"keep"}},"metadata":"retained"})";

        struct TemporarySettings
        {
            std::wstring path;
            TemporarySettings()
            {
                wchar_t directory[MAX_PATH]{};
                wchar_t name[MAX_PATH]{};
                Assert::IsTrue(GetTempPathW(MAX_PATH, directory) != 0);
                Assert::IsTrue(GetTempFileNameW(directory, L"LSC", 0, name) != 0);
                path = name;
            }
            ~TemporarySettings()
            {
                DeleteFileW(path.c_str());
            }
            void Write(const std::wstring& text)
            {
                std::ofstream stream(path.c_str(), std::ios::binary | std::ios::trunc);
                stream << winrt::to_string(text);
                stream.close();
                Assert::IsFalse(stream.fail());
            }

            std::string Read() const
            {
                std::ifstream stream(path.c_str(), std::ios::binary);
                return { std::istreambuf_iterator<char>(stream), std::istreambuf_iterator<char>() };
            }
        };
    }

    TEST_CLASS (SettingsTests)
    {
    public:
        TEST_METHOD (StartupDefaultsMatchSettingsUIAndAllowTheFirstHotkey)
        {
            Apartment apartment;
            TemporarySettings file;
            Assert::IsTrue(DeleteFileW(file.path.c_str()));
            LightSwitchConfig config;
            std::wstring error;
            Assert::IsTrue(TryInitializeLightSwitchSettings(file.path, config, error), error.c_str());
            Assert::IsTrue(config.scheduleMode == ScheduleMode::Off);
            Assert::IsTrue(config.changeSystem);
            Assert::IsTrue(config.changeApps);
            Assert::AreEqual(480, config.lightTime);
            Assert::AreEqual(1200, config.darkTime);
            Assert::AreEqual(0, config.sunrise_offset);
            Assert::AreEqual(0, config.sunset_offset);
            Assert::AreEqual(L"0.0", config.latitude.c_str());
            Assert::AreEqual(L"0.0", config.longitude.c_str());

            const auto document = json::from_file(file.path);
            Assert::IsTrue(document.has_value());
            Assert::AreEqual(L"LightSwitch", document->GetNamedString(L"name").c_str());
            Assert::IsFalse(document->GetNamedString(L"version").empty());
            const auto properties = document->GetNamedObject(L"properties");
            Assert::AreEqual(16u, properties.Size());
            const auto hotkey = properties.GetNamedObject(L"toggle-theme-hotkey").GetNamedObject(L"value");
            Assert::IsTrue(hotkey.GetNamedBoolean(L"win"));
            Assert::IsTrue(hotkey.GetNamedBoolean(L"ctrl"));
            Assert::IsTrue(hotkey.GetNamedBoolean(L"shift"));
            Assert::IsFalse(hotkey.GetNamedBoolean(L"alt"));
            Assert::AreEqual(68.0, hotkey.GetNamedNumber(L"code"));
            Assert::IsFalse(properties.GetNamedObject(L"enableDarkModeProfile").GetNamedBoolean(L"value"));
            Assert::IsFalse(properties.GetNamedObject(L"enableLightModeProfile").GetNamedBoolean(L"value"));
            Assert::IsTrue(properties.GetNamedObject(L"darkModeProfile").GetNamedString(L"value").empty());
            Assert::IsTrue(properties.GetNamedObject(L"lightModeProfile").GetNamedString(L"value").empty());
            Assert::AreEqual(0.0, properties.GetNamedObject(L"darkModeProfileId").GetNamedNumber(L"value"));
            Assert::AreEqual(0.0, properties.GetNamedObject(L"lightModeProfileId").GetNamedNumber(L"value"));

            bool systemLight = true;
            bool appsLight = true;
            int writes = 0;
            LightSwitchStateManagerDependencies dependencies;
            dependencies.loadSettings = [&](LightSwitchConfig& loaded, std::wstring& loadError) {
                const auto saved = json::from_file(file.path);
                return saved && TryParseLightSwitchConfig(*saved, loaded, loadError);
            };
            dependencies.readTheme = [&](bool system, bool& light) -> LSTATUS {
                light = system ? systemLight : appsLight;
                return ERROR_SUCCESS;
            };
            dependencies.writeTheme = [&](bool system, bool light) -> LSTATUS {
                (system ? systemLight : appsLight) = light;
                ++writes;
                return ERROR_SUCCESS;
            };
            dependencies.localTime = []() { return SYSTEMTIME{}; };
            dependencies.readNightLight = []() { return false; };
            dependencies.notifyThemeChanged = [](bool) {};
            dependencies.updateSunTimes = [](const LightSwitchConfig&, const SYSTEMTIME&) { return std::pair{ 480, 1200 }; };
            LightSwitchStateManager manager(std::move(dependencies));
            manager.SyncInitialThemeState();
            Assert::AreEqual(0, writes);
            Assert::IsTrue(manager.ToggleTheme().success);
            Assert::AreEqual(2, writes);
            Assert::IsFalse(systemLight);
            Assert::IsFalse(appsLight);
        }

        TEST_METHOD (StartupPreservesExistingSettingsIncludingUnknownFields)
        {
            TemporarySettings file;
            file.Write(ValidSettings);
            const auto original = file.Read();
            LightSwitchConfig config;
            std::wstring error;
            Assert::IsTrue(TryInitializeLightSwitchSettings(file.path, config, error), error.c_str());
            Assert::IsTrue(config.scheduleMode == ScheduleMode::FixedHours);
            Assert::IsTrue(config.changeSystem);
            Assert::IsFalse(config.changeApps);
            Assert::AreEqual(original, file.Read());
        }

        TEST_METHOD (StartupDoesNotReplaceUnreadableSettings)
        {
            TemporarySettings file;
            file.Write(ValidSettings);
            const auto original = file.Read();
            wil::unique_hfile locked(CreateFileW(file.path.c_str(), GENERIC_READ, 0, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
            Assert::IsTrue(static_cast<bool>(locked));
            LightSwitchConfig config;
            config.lightTime = 123;
            std::wstring error;
            Assert::IsFalse(TryInitializeLightSwitchSettings(file.path, config, error));
            Assert::IsFalse(error.empty());
            Assert::AreEqual(123, config.lightTime);
            locked.reset();
            Assert::AreEqual(original, file.Read());
        }

        TEST_METHOD (StartupDoesNotTreatAMissingParentAsAMissingSettingsFile)
        {
            TemporarySettings file;
            Assert::IsTrue(DeleteFileW(file.path.c_str()));
            LightSwitchConfig config;
            std::wstring error;
            Assert::IsFalse(TryInitializeLightSwitchSettings(file.path + L"\\settings.json", config, error));
            Assert::IsFalse(error.empty());
            Assert::AreEqual(INVALID_FILE_ATTRIBUTES, GetFileAttributesW(file.path.c_str()));
        }

        TEST_METHOD (ConcurrentStartupPublishesACompleteSettingsFile)
        {
            Apartment apartment;
            TemporarySettings file;
            Assert::IsTrue(DeleteFileW(file.path.c_str()));
            std::promise<void> start;
            auto ready = start.get_future().share();
            std::vector<std::future<bool>> operations;
            for (int index = 0; index < 12; ++index)
            {
                operations.push_back(std::async(std::launch::async, [&, ready]() {
                    ready.wait();
                    LightSwitchConfig config;
                    std::wstring error;
                    return TryInitializeLightSwitchSettings(file.path, config, error) &&
                           config.scheduleMode == ScheduleMode::Off && config.changeSystem && config.changeApps;
                }));
            }
            start.set_value();
            for (auto& operation : operations)
                Assert::IsTrue(operation.get());
            Assert::IsTrue(json::from_file(file.path).has_value());
            WIN32_FIND_DATAW entry{};
            const auto leftover = FindFirstFileW((file.path + L".init.*").c_str(), &entry);
            if (leftover != INVALID_HANDLE_VALUE)
                FindClose(leftover);
            Assert::IsTrue(leftover == INVALID_HANDLE_VALUE);
        }

        TEST_METHOD (ParsesExistingSettingsWithoutChangingTheSchema)
        {
            Apartment apartment;
            LightSwitchConfig config;
            std::wstring error;
            Assert::IsTrue(TryParseLightSwitchConfig(json::JsonObject::Parse(ValidSettings), config, error));
            Assert::IsTrue(config.scheduleMode == ScheduleMode::FixedHours);
            Assert::IsTrue(config.changeSystem);
            Assert::IsFalse(config.changeApps);
            Assert::AreEqual(480, config.lightTime);
            Assert::AreEqual(1200, config.darkTime);
        }

        TEST_METHOD (InvalidSettingsDoNotProduceAPartialConfiguration)
        {
            Apartment apartment;
            auto document = json::JsonObject::Parse(ValidSettings);
            document.GetNamedObject(L"properties").GetNamedObject(L"darkTime").SetNamedValue(L"value", json::value(1.5));
            LightSwitchConfig config;
            config.lightTime = 123;
            std::wstring error;
            Assert::IsFalse(TryParseLightSwitchConfig(document, config, error));
            Assert::AreEqual(123, config.lightTime);
            Assert::IsFalse(error.empty());
            document.GetNamedObject(L"properties").GetNamedObject(L"scheduleMode").SetNamedValue(L"value", json::value(L"unknown"));
            Assert::IsFalse(TryParseLightSwitchConfig(document, config, error));
        }

        TEST_METHOD (MissingThemeTargetsAreNotGuessed)
        {
            Apartment apartment;
            auto document = json::JsonObject::Parse(ValidSettings);
            document.GetNamedObject(L"properties").Remove(L"changeApps");
            LightSwitchConfig config;
            std::wstring error;
            Assert::IsFalse(TryParseLightSwitchConfig(document, config, error));
            TemporarySettings file;
            file.Write(document.Stringify().c_str());
            const auto original = file.Read();
            Assert::IsFalse(TryInitializeLightSwitchSettings(file.path, config, error));
            Assert::AreEqual(original, file.Read());
        }

        TEST_METHOD (SchedulePatchRetainsUnrelatedFieldsAndCanBeReadBack)
        {
            Apartment apartment;
            TemporarySettings file;
            file.Write(ValidSettings);
            LightSwitchConfig config;
            std::wstring error;
            Assert::IsTrue(TryPatchLightSwitchScheduleMode(file.path, ScheduleMode::Off, config, error), error.c_str());
            Assert::IsTrue(config.scheduleMode == ScheduleMode::Off);
            const auto saved = json::from_file(file.path);
            Assert::IsTrue(saved.has_value());
            Assert::AreEqual(L"retained", saved->GetNamedString(L"metadata").c_str());
            const auto properties = saved->GetNamedObject(L"properties");
            Assert::AreEqual(L"keep", properties.GetNamedObject(L"futureProperty").GetNamedString(L"value").c_str());
            Assert::AreEqual(42.0, properties.GetNamedObject(L"scheduleMode").GetNamedNumber(L"retained"));
            Assert::IsTrue(TryPatchLightSwitchScheduleMode(file.path, ScheduleMode::Off, config, error));
            Assert::AreEqual(saved->Stringify().c_str(), json::from_file(file.path)->Stringify().c_str());
        }

        TEST_METHOD (MalformedSettingsAreNotReplacedByDefaults)
        {
            TemporarySettings file;
            file.Write(L"not json");
            LightSwitchConfig config;
            std::wstring error;
            Assert::IsFalse(TryInitializeLightSwitchSettings(file.path, config, error));
            Assert::AreEqual("not json", file.Read().c_str());
            Assert::IsFalse(TryPatchLightSwitchScheduleMode(file.path, ScheduleMode::Off, config, error));
            std::ifstream input(file.path.c_str(), std::ios::binary);
            const std::string remaining{ std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>() };
            Assert::AreEqual("not json", remaining.c_str());
        }

        TEST_METHOD (EffectiveSettingsIgnoreOnlyDerivedSunTimes)
        {
            LightSwitchConfig left;
            left.scheduleMode = ScheduleMode::SunsetToSunrise;
            auto right = left;
            ++right.lightTime;
            Assert::IsTrue(HasSameEffectiveLightSwitchSettings(left, right));
            ++right.sunrise_offset;
            Assert::IsFalse(HasSameEffectiveLightSwitchSettings(left, right));
            left.scheduleMode = ScheduleMode::FixedHours;
            right = left;
            ++right.lightTime;
            Assert::IsFalse(HasSameEffectiveLightSwitchSettings(left, right));
        }
    };
}
