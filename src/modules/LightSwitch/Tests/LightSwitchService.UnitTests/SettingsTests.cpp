// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <CppUnitTest.h>
#include "LightSwitchSettings.h"
#include "TestSupport.h"
#include <fstream>

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
        };
    }

    TEST_CLASS (SettingsTests)
    {
    public:
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
