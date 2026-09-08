// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <CppUnitTest.h>
#include "LightSwitchStateManager.h"
#include <future>
#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace LightSwitchServiceUnitTests
{
    namespace
    {
        struct Environment
        {
            LightSwitchConfig config;
            std::optional<bool> system = true;
            std::optional<bool> apps = true;
            SYSTEMTIME now{};
            bool nightLight = false;
            bool readableSettings = true;
            bool failSystemWrite = false;
            bool failAppsWrite = false;
            int writes = 0;
            int settingsReads = 0;
            std::vector<bool> notifications;
            std::function<void(bool)> afterWrite;

            Environment()
            {
                config.changeSystem = true;
                config.changeApps = true;
                now.wYear = 2026;
                now.wMonth = 9;
                now.wDay = 8;
                now.wHour = 12;
            }

            LightSwitchStateManagerDependencies Dependencies()
            {
                LightSwitchStateManagerDependencies dependencies;
                dependencies.loadSettings = [this](LightSwitchConfig& value, std::wstring& error) {
                    ++settingsReads;
                    if (!readableSettings)
                    {
                        error = L"Settings are unavailable.";
                        return false;
                    }
                    value = config;
                    return true;
                };
                dependencies.readTheme = [this](bool systemTarget, bool& light) -> LSTATUS {
                    const auto value = systemTarget ? system : apps;
                    if (!value)
                        return ERROR_ACCESS_DENIED;
                    light = *value;
                    return ERROR_SUCCESS;
                };
                dependencies.writeTheme = [this](bool systemTarget, bool light) -> LSTATUS {
                    ++writes;
                    if (systemTarget ? failSystemWrite : failAppsWrite)
                        return ERROR_ACCESS_DENIED;
                    (systemTarget ? system : apps) = light;
                    if (afterWrite)
                        afterWrite(systemTarget);
                    return ERROR_SUCCESS;
                };
                dependencies.readNightLight = [this]() { return nightLight; };
                dependencies.localTime = [this]() { return now; };
                dependencies.notifyThemeChanged = [this](bool light) { notifications.push_back(light); };
                dependencies.updateSunTimes = [this](const LightSwitchConfig&, const SYSTEMTIME&) {
                    return std::pair{ config.lightTime, config.darkTime };
                };
                return dependencies;
            }
        };
    }

    TEST_CLASS (StateManagerTests)
    {
    public:
        TEST_METHOD (ExplicitSetIsIdempotentAndHoldsUntilTheNextBoundary)
        {
            Environment environment;
            LightSwitchStateManager manager(environment.Dependencies());
            const auto first = manager.SetTheme(false);
            Assert::IsTrue(first.success);
            Assert::IsTrue(first.status.manualOverride);
            Assert::AreEqual(2, environment.writes);
            Assert::IsTrue(manager.SetTheme(false).success);
            Assert::AreEqual(2, environment.writes);
            Assert::AreEqual(size_t{ 1 }, environment.notifications.size());
            environment.now.wMinute = 1;
            manager.OnTick();
            Assert::IsTrue(manager.GetState().isManualOverride);
            Assert::IsFalse(*environment.system);
            environment.now.wHour = 20;
            environment.now.wMinute = 0;
            manager.OnTick();
            Assert::IsFalse(manager.GetState().isManualOverride);
            Assert::IsFalse(*environment.system);
        }

        TEST_METHOD (SettingTheScheduledThemeDoesNotCancelTheNextToggle)
        {
            Environment environment;
            environment.system = false;
            environment.apps = false;
            LightSwitchStateManager manager(environment.Dependencies());
            const auto set = manager.SetTheme(true);
            Assert::IsTrue(set.success);
            Assert::IsFalse(set.status.manualOverride);
            const auto toggle = manager.ToggleTheme();
            Assert::IsTrue(toggle.success);
            Assert::IsTrue(toggle.status.manualOverride);
            Assert::IsFalse(*toggle.status.systemLight);
            Assert::IsFalse(*toggle.status.appsLight);
        }

        TEST_METHOD (TogglePreservesLegacyMixedThemeTransition)
        {
            Environment environment;
            environment.now.wHour = 21;
            environment.apps = false;
            LightSwitchStateManager manager(environment.Dependencies());
            manager.DetectExternalThemeChange();
            Assert::IsTrue(manager.GetState().isManualOverride);
            const auto result = manager.ToggleTheme();
            Assert::IsTrue(result.success);
            Assert::IsFalse(result.status.manualOverride);
            Assert::IsFalse(*result.status.systemLight);
            Assert::IsFalse(*result.status.appsLight);
        }

        TEST_METHOD (ExplicitSetAtABoundaryUsesItsOwnTimeBaseline)
        {
            Environment environment;
            environment.now.wHour = 19;
            environment.now.wMinute = 59;
            LightSwitchStateManager manager(environment.Dependencies());
            manager.OnTick();
            environment.now.wHour = 20;
            environment.now.wMinute = 0;
            Assert::IsTrue(manager.SetTheme(true).status.manualOverride);
            manager.OnTick();
            Assert::IsTrue(manager.GetState().isManualOverride);
            Assert::IsTrue(*environment.system);
        }

        TEST_METHOD (DuplicateSettingsNotificationDoesNotClearManualOverride)
        {
            Environment environment;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.SetTheme(false).success);
            Assert::IsTrue(manager.OnSettingsChanged().success);
            Assert::IsTrue(manager.GetState().isManualOverride);
            Assert::IsFalse(*environment.system);
        }

        TEST_METHOD (SettingsReadBeforeDebounceDoesNotBecomeAManualOverride)
        {
            Environment environment;
            environment.now.wHour = 11;
            environment.now.wMinute = 59;
            LightSwitchStateManager manager(environment.Dependencies());
            manager.OnTick();

            // The minute tick sees the new plan before the settings file watcher fires.
            environment.config.darkTime = 11 * 60;
            environment.now.wHour = 12;
            environment.now.wMinute = 0;
            manager.DetectExternalThemeChange();
            Assert::IsFalse(manager.GetState().isManualOverride);
            manager.OnTick();
            Assert::IsFalse(*environment.system);
            Assert::IsFalse(*environment.apps);

            const auto result = manager.OnSettingsChanged();
            Assert::IsTrue(result.success);
            Assert::IsFalse(result.status.manualOverride);
            Assert::IsFalse(*result.status.systemLight);
            Assert::IsFalse(*result.status.appsLight);
        }

        TEST_METHOD (SunTimeCacheEchoDoesNotClearManualOverride)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::SunsetToSunrise;
            environment.config.latitude = L"22";
            environment.config.longitude = L"114";
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.SetTheme(false).success);
            ++environment.config.lightTime;
            Assert::IsTrue(manager.OnSettingsChanged().success);
            Assert::IsTrue(manager.GetState().isManualOverride);
        }

        TEST_METHOD (NightLightModeReadsTheActualStateBeforeApplyingAndIgnoresDuplicateNotifications)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::FollowNightLight;
            environment.nightLight = true;
            LightSwitchStateManager manager(environment.Dependencies());
            const auto initial = manager.OnSettingsChanged();
            Assert::IsTrue(initial.success);
            Assert::IsFalse(*initial.status.systemLight);
            Assert::IsTrue(manager.SetTheme(true).status.manualOverride);
            manager.OnNightLightChange();
            Assert::IsTrue(manager.GetState().isManualOverride);
            environment.nightLight = false;
            manager.OnNightLightChange();
            Assert::IsFalse(manager.GetState().isManualOverride);
            Assert::IsTrue(*environment.system);
        }

        TEST_METHOD (RepeatedOperationsRetainNewlyCalculatedSunBoundaries)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::SunsetToSunrise;
            environment.config.latitude = L"22";
            environment.config.longitude = L"114";
            int calculations = 0;
            auto dependencies = environment.Dependencies();
            dependencies.updateSunTimes = [&](const LightSwitchConfig&, const SYSTEMTIME&) {
                ++calculations;
                return std::pair{ 900, 1300 };
            };
            LightSwitchStateManager manager(std::move(dependencies));
            Assert::IsTrue(manager.SetTheme(true).status.manualOverride);
            Assert::IsTrue(manager.SetTheme(true).status.manualOverride);
            Assert::IsTrue(manager.OnSettingsChanged().status.manualOverride);
            Assert::AreEqual(900, manager.GetState().effectiveLightMinutes);
            Assert::AreEqual(1, calculations);
        }

        TEST_METHOD (NoTargetsAndUnreadableThemesDoNotMutateWindows)
        {
            Environment environment;
            environment.config.changeSystem = false;
            environment.config.changeApps = false;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::AreEqual(L"NO_TARGETS", manager.SetTheme(false).errorCode.c_str());
            Assert::AreEqual(L"NO_TARGETS", manager.ToggleTheme().errorCode.c_str());
            Assert::AreEqual(0, environment.writes);
            environment.config.changeSystem = true;
            environment.system.reset();
            Assert::AreEqual(L"THEME_READ_FAILED", manager.ToggleTheme().errorCode.c_str());
            Assert::AreEqual(0, environment.writes);
            Assert::IsFalse(manager.GetStatusSnapshot().systemLight.has_value());
        }

        TEST_METHOD (WriteFailureReportsTheActualPartialResult)
        {
            Environment environment;
            environment.failAppsWrite = true;
            LightSwitchStateManager manager(environment.Dependencies());
            const auto result = manager.SetTheme(false);
            Assert::IsFalse(result.success);
            Assert::AreEqual(L"THEME_WRITE_FAILED", result.errorCode.c_str());
            Assert::IsFalse(*result.status.systemLight);
            Assert::IsTrue(*result.status.appsLight);
            Assert::IsTrue(environment.notifications.empty());
        }

        TEST_METHOD (SettingsApplicationReportsAWriteFailure)
        {
            Environment environment;
            environment.now.wHour = 21;
            environment.failAppsWrite = true;
            LightSwitchStateManager manager(environment.Dependencies());
            const auto result = manager.OnSettingsChanged();
            Assert::IsFalse(result.success);
            Assert::AreEqual(L"THEME_WRITE_FAILED", result.errorCode.c_str());
            Assert::IsFalse(*result.status.systemLight);
            Assert::IsTrue(*result.status.appsLight);
        }

        TEST_METHOD (EachCommandUsesOneConfigurationSnapshot)
        {
            Environment environment;
            environment.afterWrite = [&](bool system) {
                if (system)
                    environment.config.changeApps = false;
            };
            LightSwitchStateManager manager(environment.Dependencies());
            const auto result = manager.SetTheme(false);
            Assert::IsTrue(result.success);
            Assert::IsTrue(result.status.config.changeApps);
            Assert::IsFalse(*result.status.appsLight);
        }

        TEST_METHOD (StatusDoesNotReloadSettingsOrChangeThemes)
        {
            Environment environment;
            LightSwitchStateManager manager(environment.Dependencies());
            manager.OnSettingsChanged();
            const auto reads = environment.settingsReads;
            const auto writes = environment.writes;
            manager.GetStatusSnapshot();
            Assert::AreEqual(reads, environment.settingsReads);
            Assert::AreEqual(writes, environment.writes);
        }

        TEST_METHOD (UnreadableSettingsFailWithoutChangingThemes)
        {
            Environment environment;
            environment.readableSettings = false;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::AreEqual(L"SETTINGS_READ_FAILED", manager.SetTheme(false).errorCode.c_str());
            Assert::IsFalse(manager.GetStatusSnapshot().configurationAvailable);
            Assert::AreEqual(0, environment.writes);
        }

        TEST_METHOD (ConcurrentTogglesAreSerialized)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::Off;
            LightSwitchStateManager manager(environment.Dependencies());
            std::vector<std::future<ThemeCommandResult>> operations;
            for (int index = 0; index < 12; ++index)
                operations.push_back(std::async(std::launch::async, [&manager]() { return manager.ToggleTheme(); }));
            for (auto& operation : operations)
                Assert::IsTrue(operation.get().success);
            Assert::AreEqual(24, environment.writes);
            Assert::IsTrue(*environment.system);
            Assert::IsTrue(*environment.apps);
        }
    };
}
