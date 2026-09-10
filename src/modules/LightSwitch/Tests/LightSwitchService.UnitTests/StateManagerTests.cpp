// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <CppUnitTest.h>
#include "LightSwitchStateManager.h"
#include <future>
#include <iterator>
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
            bool failSystemRead = false;
            bool failAppsRead = false;
            bool failSystemWrite = false;
            bool failAppsWrite = false;
            int writes = 0;
            int settingsReads = 0;
            int sunCalculations = 0;
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
                    if ((systemTarget ? failSystemRead : failAppsRead) || !value)
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
                    ++sunCalculations;
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
            environment.system = environment.apps = false;
            LightSwitchStateManager manager(environment.Dependencies());
            manager.SyncInitialThemeState();
            environment.system = true;
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

        TEST_METHOD (ToggleAfterClockBoundaryStartsANewOverride)
        {
            for (const bool hadOverride : { false, true })
            {
                Environment environment;
                environment.now.wHour = 19;
                environment.now.wMinute = 59;
                LightSwitchStateManager manager(environment.Dependencies());
                Assert::IsTrue(manager.OnSettingsChanged().success);
                if (hadOverride)
                    Assert::IsTrue(manager.SetTheme(false).status.manualOverride);
                else
                    environment.system = environment.apps = false;

                environment.now.wHour = 20;
                environment.now.wMinute = 0;
                const auto first = manager.ToggleTheme();
                Assert::IsTrue(first.success);
                Assert::IsTrue(first.status.manualOverride);
                Assert::IsTrue(*first.status.systemLight);
                Assert::IsTrue(*first.status.appsLight);
                manager.OnTick();
                Assert::IsTrue(manager.GetState().isManualOverride);
                Assert::IsTrue(*environment.system);

                const auto second = manager.ToggleTheme();
                Assert::IsTrue(second.success);
                Assert::IsFalse(second.status.manualOverride);
                Assert::IsFalse(*second.status.systemLight);
                Assert::IsFalse(*second.status.appsLight);
            }
        }

        TEST_METHOD (ToggleToTheNewClockPlanDoesNotCancelTheFollowingToggle)
        {
            Environment environment;
            environment.now.wHour = 19;
            environment.now.wMinute = 59;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.OnSettingsChanged().success);
            environment.now.wHour = 20;
            environment.now.wMinute = 0;
            const auto first = manager.ToggleTheme();
            Assert::IsTrue(first.success);
            Assert::IsFalse(first.status.manualOverride);
            Assert::IsFalse(*first.status.systemLight);
            const auto second = manager.ToggleTheme();
            Assert::IsTrue(second.success);
            Assert::IsTrue(second.status.manualOverride);
            Assert::IsTrue(*second.status.systemLight);
            Assert::IsTrue(*second.status.appsLight);
        }

        TEST_METHOD (ToggleAfterSolarBoundaryAcrossMidnightUsesANewBaseline)
        {
            for (const bool hadOverride : { false, true })
            {
                Environment environment;
                environment.config.scheduleMode = ScheduleMode::SunsetToSunrise;
                environment.config.latitude = L"22";
                environment.config.longitude = L"114";
                environment.config.darkTime = 23 * 60 + 50;
                environment.config.sunset_offset = 20;
                environment.now.wHour = 23;
                environment.now.wMinute = 59;
                LightSwitchStateManager manager(environment.Dependencies());
                Assert::IsTrue(manager.OnSettingsChanged().success);
                if (hadOverride)
                    Assert::IsTrue(manager.SetTheme(false).status.manualOverride);

                ++environment.now.wDay;
                environment.now.wHour = 0;
                environment.now.wMinute = 10;
                const auto first = manager.ToggleTheme();
                Assert::IsTrue(first.success);
                Assert::AreEqual(hadOverride, first.status.manualOverride);
                Assert::AreEqual(hadOverride, *first.status.systemLight);
                manager.OnTick();
                Assert::AreEqual(hadOverride, manager.GetState().isManualOverride);
                const auto second = manager.ToggleTheme();
                Assert::IsTrue(second.success);
                Assert::AreEqual(!hadOverride, second.status.manualOverride);
                Assert::AreEqual(!hadOverride, *second.status.systemLight);
                Assert::AreEqual(!hadOverride, *second.status.appsLight);
            }
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

        TEST_METHOD (UnchangedSettingsPreserveExternalThemeBeforeMinuteDetection)
        {
            Environment environment;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.OnSettingsChanged().success);
            environment.system = environment.apps = false;
            manager.GetStatusSnapshot();
            const auto result = manager.OnSettingsChanged();
            Assert::IsTrue(result.success);
            Assert::IsTrue(result.status.manualOverride);
            Assert::IsFalse(*result.status.systemLight);
            Assert::IsFalse(*result.status.appsLight);
            Assert::AreEqual(0, environment.writes);
        }

        TEST_METHOD (FailedSettingsCommandDoesNotConsumeAnExternalThemeChange)
        {
            Environment environment;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.OnSettingsChanged().success);
            environment.system = environment.apps = false;
            environment.readableSettings = false;
            Assert::IsFalse(manager.SetTheme(true).success);
            environment.readableSettings = true;
            const auto result = manager.OnSettingsChanged();
            Assert::IsTrue(result.success);
            Assert::IsTrue(result.status.manualOverride);
            Assert::IsFalse(*result.status.systemLight);
            Assert::AreEqual(0, environment.writes);
        }

        TEST_METHOD (UnchangedSettingsAtANaturalBoundaryApplyThePlan)
        {
            Environment environment;
            environment.now.wHour = 19;
            environment.now.wMinute = 59;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.OnSettingsChanged().success);
            environment.now.wHour = 20;
            environment.now.wMinute = 0;
            const auto result = manager.OnSettingsChanged();
            Assert::IsTrue(result.success);
            Assert::IsFalse(result.status.manualOverride);
            Assert::IsFalse(*result.status.systemLight);
            Assert::IsFalse(*result.status.appsLight);
            Assert::AreEqual(2, environment.writes);
            Assert::AreEqual(size_t{ 1 }, environment.notifications.size());
        }

        TEST_METHOD (MinuteDetectionDoesNotMistakeANaturalBoundaryForAnExternalEdit)
        {
            Environment environment;
            environment.now.wHour = 19;
            environment.now.wMinute = 59;
            LightSwitchStateManager manager(environment.Dependencies());
            manager.SyncInitialThemeState();
            environment.now.wHour = 20;
            environment.now.wMinute = 0;
            manager.DetectExternalThemeChange();
            Assert::IsFalse(manager.GetState().isManualOverride);
            Assert::IsTrue(environment.notifications.empty());
            manager.OnTick();
            Assert::IsFalse(*environment.system);
            Assert::AreEqual(size_t{ 1 }, environment.notifications.size());
        }

        TEST_METHOD (ARealSettingsChangeAppliesBeforePendingExternalThemeDetection)
        {
            Environment environment;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.OnSettingsChanged().success);
            environment.system = environment.apps = false;
            environment.config.darkTime = 23 * 60;
            const auto result = manager.OnSettingsChanged();
            Assert::IsTrue(result.success);
            Assert::IsFalse(result.status.manualOverride);
            Assert::IsTrue(*result.status.systemLight);
            Assert::IsTrue(*result.status.appsLight);
        }

        TEST_METHOD (ExternalChangesToUnselectedTargetsDoNotCreateAnOverride)
        {
            Environment environment;
            environment.config.changeApps = false;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.OnSettingsChanged().success);
            environment.apps = false;
            const auto result = manager.OnSettingsChanged();
            Assert::IsTrue(result.success);
            Assert::IsFalse(result.status.manualOverride);
            Assert::IsTrue(*result.status.systemLight);
            Assert::IsFalse(*result.status.appsLight);
            Assert::AreEqual(0, environment.writes);
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

        TEST_METHOD (ExplicitThemeAfterNightLightBoundarySurvivesDelayedNotification)
        {
            for (const bool initiallyOn : { false, true })
            {
                Environment environment;
                environment.config.scheduleMode = ScheduleMode::FollowNightLight;
                environment.nightLight = initiallyOn;
                LightSwitchStateManager manager(environment.Dependencies());
                Assert::IsTrue(manager.OnSettingsChanged().success);

                // The boundary occurs before the command, but the observer is delayed.
                environment.nightLight = !initiallyOn;
                const auto result = manager.SetTheme(!initiallyOn);
                Assert::IsTrue(result.success);
                Assert::IsTrue(result.status.manualOverride);
                manager.DetectExternalThemeChange();
                manager.OnTick();
                Assert::IsTrue(manager.OnSettingsChanged().status.manualOverride);
                manager.OnNightLightChange();
                Assert::IsTrue(manager.GetState().isManualOverride);
                Assert::AreEqual(!initiallyOn, *environment.system);
                Assert::AreEqual(!initiallyOn, *environment.apps);

                environment.nightLight = initiallyOn;
                manager.DetectExternalThemeChange();
                manager.OnTick();
                Assert::IsFalse(manager.GetState().isManualOverride);
            }
        }

        TEST_METHOD (ExplicitThemeSamplesNightLightAfterThemeWritesFinish)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::FollowNightLight;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.OnSettingsChanged().success);
            environment.system = environment.apps = false;
            environment.afterWrite = [&](bool) { environment.nightLight = true; };
            Assert::IsTrue(manager.SetTheme(true).status.manualOverride);
            manager.OnNightLightChange();
            Assert::IsTrue(manager.GetState().isManualOverride);
            Assert::IsTrue(*environment.system);
            Assert::IsTrue(*environment.apps);
        }

        TEST_METHOD (ToggleAfterNightLightBoundaryStartsANewOverride)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::FollowNightLight;
            environment.nightLight = true;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.OnSettingsChanged().success);
            Assert::IsTrue(manager.SetTheme(true).status.manualOverride);
            environment.nightLight = false;
            const auto result = manager.ToggleTheme();
            Assert::IsTrue(result.success);
            Assert::IsTrue(result.status.manualOverride);
            Assert::IsFalse(*result.status.systemLight);
            manager.DetectExternalThemeChange();
            manager.OnTick();
            Assert::IsTrue(manager.OnSettingsChanged().status.manualOverride);
            manager.OnNightLightChange();
            Assert::IsTrue(manager.GetState().isManualOverride);
            Assert::IsFalse(*environment.system);

            // With no further transition, the next toggle releases that override.
            const auto release = manager.ToggleTheme();
            Assert::IsTrue(release.success);
            Assert::IsFalse(release.status.manualOverride);
            Assert::IsTrue(*release.status.systemLight);
        }

        TEST_METHOD (ToggleToTheNewNightLightPlanDoesNotCancelTheFollowingToggle)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::FollowNightLight;
            environment.nightLight = true;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.OnSettingsChanged().success);
            environment.nightLight = false;
            const auto first = manager.ToggleTheme();
            Assert::IsTrue(first.success);
            Assert::IsFalse(first.status.manualOverride);
            Assert::IsTrue(*first.status.systemLight);
            manager.OnNightLightChange();
            const auto second = manager.ToggleTheme();
            Assert::IsTrue(second.success);
            Assert::IsTrue(second.status.manualOverride);
            Assert::IsFalse(*second.status.systemLight);
            Assert::IsFalse(*second.status.appsLight);
        }

        TEST_METHOD (DuplicateNightLightNotificationPreservesAnExternalThemeChoice)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::FollowNightLight;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.OnSettingsChanged().success);
            environment.system = environment.apps = false;
            manager.OnNightLightChange();
            Assert::IsTrue(manager.GetState().isManualOverride);
            Assert::IsFalse(*environment.system);
            Assert::AreEqual(0, environment.writes);

            environment.nightLight = true;
            manager.OnNightLightChange();
            Assert::IsFalse(manager.GetState().isManualOverride);
            environment.nightLight = false;
            manager.OnNightLightChange();
            Assert::IsTrue(*environment.system);
            Assert::IsTrue(*environment.apps);
        }

        TEST_METHOD (ExternalThemeDetectionUsesTheCurrentNightLightBaseline)
        {
            for (const bool settingsNotificationFirst : { false, true })
            {
                Environment environment;
                environment.config.scheduleMode = ScheduleMode::FollowNightLight;
                environment.nightLight = true;
                environment.failSystemWrite = environment.failAppsWrite = true;
                LightSwitchStateManager manager(environment.Dependencies());
                Assert::IsFalse(manager.OnSettingsChanged().success);

                // Windows recovers, Night Light changes, and an external editor chooses
                // dark before the delayed Night Light callback acquires the state lock.
                environment.failSystemWrite = environment.failAppsWrite = false;
                environment.nightLight = false;
                environment.system = environment.apps = false;
                if (settingsNotificationFirst)
                    Assert::IsTrue(manager.OnSettingsChanged().success);
                else
                {
                    manager.DetectExternalThemeChange();
                    manager.OnTick();
                }
                Assert::IsTrue(manager.GetState().isManualOverride);
                Assert::IsTrue(manager.OnSettingsChanged().status.manualOverride);
                manager.OnNightLightChange();
                Assert::IsTrue(manager.GetState().isManualOverride);
                Assert::IsFalse(*environment.system);
                Assert::IsFalse(*environment.apps);
                environment.nightLight = true;
                manager.DetectExternalThemeChange();
                manager.OnTick();
                Assert::IsFalse(manager.GetState().isManualOverride);
            }
        }

        TEST_METHOD (NightLightPlanRetriesAWriteFailureWithoutAnotherNotification)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::FollowNightLight;
            environment.nightLight = true;
            environment.failSystemWrite = environment.failAppsWrite = true;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsFalse(manager.OnSettingsChanged().success);
            Assert::AreEqual(2, environment.writes);

            environment.failSystemWrite = environment.failAppsWrite = false;
            ++environment.now.wMinute;
            manager.DetectExternalThemeChange();
            manager.OnTick();

            Assert::IsFalse(manager.GetState().isManualOverride);
            Assert::IsFalse(*environment.system);
            Assert::IsFalse(*environment.apps);
            Assert::AreEqual(4, environment.writes);
            Assert::AreEqual(size_t{ 1 }, environment.notifications.size());
        }

        TEST_METHOD (NightLightTransitionRecoversFromATemporarySettingsReadFailure)
        {
            for (const bool hadOverride : { false, true })
            {
                Environment environment;
                environment.config.scheduleMode = ScheduleMode::FollowNightLight;
                LightSwitchStateManager manager(environment.Dependencies());
                manager.SyncInitialThemeState();
                if (hadOverride)
                    Assert::IsTrue(manager.SetTheme(false).status.manualOverride);

                // An exclusive file lock need not produce a settings-change event.
                environment.readableSettings = false;
                environment.nightLight = true;
                manager.OnNightLightChange();
                Assert::IsFalse(manager.GetState().isNightLightActive);
                environment.readableSettings = true;
                ++environment.now.wMinute;
                manager.DetectExternalThemeChange();
                manager.OnTick();

                Assert::IsTrue(manager.GetState().isNightLightActive);
                Assert::IsFalse(manager.GetState().isManualOverride);
                Assert::IsFalse(*environment.system);
                Assert::IsFalse(*environment.apps);
            }
        }

        TEST_METHOD (UnchangedSettingsReconcileAnUnreportedNightLightTransition)
        {
            for (const bool hadOverride : { false, true })
            {
                Environment environment;
                environment.config.scheduleMode = ScheduleMode::FollowNightLight;
                LightSwitchStateManager manager(environment.Dependencies());
                manager.SyncInitialThemeState();
                if (hadOverride)
                    Assert::IsTrue(manager.SetTheme(false).status.manualOverride);

                environment.nightLight = true;
                const auto result = manager.OnSettingsChanged();

                Assert::IsTrue(result.success);
                Assert::IsTrue(manager.GetState().isNightLightActive);
                Assert::IsFalse(result.status.manualOverride);
                Assert::IsFalse(*result.status.systemLight);
                Assert::IsFalse(*result.status.appsLight);
            }
        }

        TEST_METHOD (MinuteDetectionLeavesANightLightTransitionForTheObserver)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::FollowNightLight;
            environment.nightLight = true;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.OnSettingsChanged().success);
            environment.nightLight = false;
            manager.DetectExternalThemeChange();
            Assert::IsFalse(manager.GetState().isManualOverride);
            manager.OnNightLightChange();
            Assert::IsFalse(manager.GetState().isManualOverride);
            Assert::IsTrue(*environment.system);
            Assert::IsTrue(*environment.apps);
        }

        TEST_METHOD (NightLightNotificationWithASettingsChangeAppliesTheNewPlan)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::FollowNightLight;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.OnSettingsChanged().success);
            environment.system = environment.apps = false;
            environment.config.scheduleMode = ScheduleMode::FixedHours;
            manager.OnNightLightChange();
            Assert::IsFalse(manager.GetState().isManualOverride);
            Assert::IsTrue(*environment.system);
            Assert::IsTrue(*environment.apps);
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

        TEST_METHOD (OverrideExpiresAcrossADayOfMissedBoundaries)
        {
            Environment environment;
            environment.now.wHour = 21;
            LightSwitchStateManager manager(environment.Dependencies());
            manager.SyncInitialThemeState();
            Assert::IsTrue(manager.SetTheme(true).status.manualOverride);
            ++environment.now.wDay;
            environment.now.wHour = 22;
            manager.DetectExternalThemeChange();
            manager.OnTick();
            Assert::IsFalse(manager.GetState().isManualOverride);
            Assert::IsFalse(*environment.system);
            Assert::IsFalse(*environment.apps);
        }

        TEST_METHOD (MissedBoundariesIncludeMonthYearAndLeapDayTransitions)
        {
            const SYSTEMTIME previousDates[] = {
                { 2026, 9, 0, 30, 21, 0, 0, 0 },
                { 2026, 12, 0, 31, 21, 0, 0, 0 },
                { 2028, 2, 0, 28, 21, 0, 0, 0 },
                { 2028, 2, 0, 29, 21, 0, 0, 0 },
            };
            const SYSTEMTIME followingDates[] = {
                { 2026, 10, 0, 1, 22, 0, 0, 0 },
                { 2027, 1, 0, 1, 22, 0, 0, 0 },
                { 2028, 2, 0, 29, 22, 0, 0, 0 },
                { 2028, 3, 0, 1, 22, 0, 0, 0 },
            };
            for (size_t index = 0; index < std::size(previousDates); ++index)
            {
                Environment environment;
                environment.now = previousDates[index];
                LightSwitchStateManager manager(environment.Dependencies());
                Assert::IsTrue(manager.SetTheme(true).status.manualOverride);
                environment.now = followingDates[index];
                manager.OnTick();
                Assert::IsFalse(manager.GetState().isManualOverride);
                Assert::IsFalse(*environment.system);
            }
        }

        TEST_METHOD (BackwardClockAdjustmentPreservesOverrideAndRebasesNextBoundary)
        {
            Environment environment;
            environment.now.wHour = 1;
            environment.now.wMinute = 59;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.SetTheme(true).status.manualOverride);
            environment.now.wMinute = 0;
            manager.DetectExternalThemeChange();
            manager.OnTick();
            Assert::IsTrue(manager.GetState().isManualOverride);
            Assert::IsTrue(*environment.system);
            environment.now.wHour = 8;
            manager.OnTick();
            Assert::IsFalse(manager.GetState().isManualOverride);
        }

        TEST_METHOD (MovingTheCalendarBackwardDoesNotInventAForwardBoundary)
        {
            Environment environment;
            environment.now.wHour = 21;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.SetTheme(true).status.manualOverride);
            --environment.now.wDay;
            environment.now.wHour = 22;
            manager.OnTick();
            Assert::IsTrue(manager.GetState().isManualOverride);
            ++environment.now.wDay;
            environment.now.wHour = 8;
            manager.OnTick();
            Assert::IsFalse(manager.GetState().isManualOverride);
        }

        TEST_METHOD (MidnightWithoutANewBoundaryDoesNotClearAnOverride)
        {
            Environment environment;
            environment.now.wHour = 20;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.SetTheme(true).status.manualOverride);
            ++environment.now.wDay;
            environment.now.wHour = 0;
            manager.OnTick();
            Assert::IsTrue(manager.GetState().isManualOverride);
            environment.now.wHour = 8;
            manager.OnTick();
            Assert::IsFalse(manager.GetState().isManualOverride);
        }

        TEST_METHOD (AConfiguredMidnightBoundaryEndsAnOverride)
        {
            Environment environment;
            environment.config.darkTime = 0;
            environment.now.wHour = 23;
            environment.now.wMinute = 59;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.SetTheme(false).status.manualOverride);
            ++environment.now.wDay;
            environment.now.wHour = 0;
            environment.now.wMinute = 0;
            manager.OnTick();
            Assert::IsFalse(manager.GetState().isManualOverride);
            Assert::IsFalse(*environment.system);
        }

        TEST_METHOD (SunCacheUsesTheCompleteDate)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::SunsetToSunrise;
            environment.config.latitude = L"22";
            environment.config.longitude = L"114";
            LightSwitchStateManager manager(environment.Dependencies());
            manager.SyncInitialThemeState();
            Assert::AreEqual(1, environment.sunCalculations);
            manager.OnTick();
            Assert::AreEqual(1, environment.sunCalculations);
            ++environment.now.wMonth;
            manager.OnTick();
            Assert::AreEqual(2, environment.sunCalculations);
            ++environment.now.wYear;
            manager.OnTick();
            Assert::AreEqual(3, environment.sunCalculations);
        }

        TEST_METHOD (SolarBoundariesUseTheirOwnDatesAcrossMidnight)
        {
            for (const bool hadRemainingBoundary : { false, true })
            {
                Environment environment;
                environment.config.scheduleMode = ScheduleMode::SunsetToSunrise;
                environment.config.latitude = L"22";
                environment.config.longitude = L"114";
                environment.config.darkTime = (hadRemainingBoundary ? 23 : 21) * 60;
                environment.now.wHour = 22;
                LightSwitchStateManager manager(environment.Dependencies());
                Assert::IsTrue(manager.SetTheme(!hadRemainingBoundary).status.manualOverride);
                ++environment.now.wDay;
                environment.now.wHour = 0;
                environment.config.darkTime = (hadRemainingBoundary ? 21 : 23) * 60;
                manager.OnTick();
                Assert::AreEqual(!hadRemainingBoundary, manager.GetState().isManualOverride);
                Assert::AreEqual(2, environment.sunCalculations);
            }
        }

        TEST_METHOD (SunsetOffsetPastMidnightExpiresAtTheNormalizedBoundary)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::SunsetToSunrise;
            environment.config.latitude = L"22";
            environment.config.longitude = L"114";
            environment.config.darkTime = 21 * 60;
            environment.config.sunset_offset = 4 * 60;
            environment.now.wHour = 0;
            environment.now.wMinute = 59;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.SetTheme(false).status.manualOverride);
            Assert::AreEqual(60, manager.GetState().effectiveDarkMinutes);
            environment.now.wHour = 1;
            environment.now.wMinute = 0;
            manager.OnTick();
            Assert::IsFalse(manager.GetState().isManualOverride);
            Assert::IsFalse(*environment.system);
        }

        TEST_METHOD (SunriseOffsetBeforeMidnightExpiresAtTheNormalizedBoundary)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::SunsetToSunrise;
            environment.config.latitude = L"22";
            environment.config.longitude = L"114";
            environment.config.lightTime = 0;
            environment.config.sunrise_offset = -1;
            environment.now.wHour = 23;
            environment.now.wMinute = 58;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.SetTheme(true).status.manualOverride);
            Assert::AreEqual(1439, manager.GetState().effectiveLightMinutes);
            environment.now.wMinute = 59;
            manager.OnTick();
            Assert::IsFalse(manager.GetState().isManualOverride);
            Assert::IsTrue(*environment.system);
        }

        TEST_METHOD (InvalidDatesDoNotInventBoundariesOrSunCalculations)
        {
            Environment environment;
            environment.now = SYSTEMTIME{};
            environment.now.wHour = 1;
            environment.now.wMinute = 59;
            LightSwitchStateManager manager(environment.Dependencies());
            Assert::IsTrue(manager.SetTheme(true).status.manualOverride);
            environment.now.wMinute = 0;
            manager.OnTick();
            Assert::IsTrue(manager.GetState().isManualOverride);
            environment.config.scheduleMode = ScheduleMode::SunsetToSunrise;
            environment.config.latitude = L"22";
            environment.config.longitude = L"114";
            manager.OnTick();
            Assert::AreEqual(0, environment.sunCalculations);
            environment.now.wYear = 2026;
            environment.now.wMonth = 9;
            environment.now.wDay = 8;
            manager.OnTick();
            Assert::AreEqual(1, environment.sunCalculations);
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

        TEST_METHOD (RecoveringThemeAccessDoesNotInventAnExternalChange)
        {
            for (const bool appsInitiallyUnreadable : { false, true })
            {
                Environment environment;
                environment.system = environment.apps = false;
                environment.failSystemRead = true;
                environment.failAppsRead = appsInitiallyUnreadable;
                environment.failSystemWrite = environment.failAppsWrite = true;
                LightSwitchStateManager manager(environment.Dependencies());
                manager.SyncInitialThemeState();
                Assert::AreEqual(2, environment.writes);

                environment.failSystemRead = environment.failAppsRead = false;
                environment.failSystemWrite = environment.failAppsWrite = false;
                ++environment.now.wMinute;
                manager.DetectExternalThemeChange();
                manager.OnTick();

                Assert::IsFalse(manager.GetState().isManualOverride);
                Assert::IsTrue(*environment.system);
                Assert::IsTrue(*environment.apps);
                Assert::AreEqual(4, environment.writes);
                Assert::AreEqual(size_t{ 1 }, environment.notifications.size());
            }
        }

        TEST_METHOD (UnverifiedScheduledWritesAreNotLaterMistakenForExternalChanges)
        {
            Environment environment;
            environment.config.scheduleMode = ScheduleMode::FollowNightLight;
            LightSwitchStateManager manager(environment.Dependencies());
            manager.SyncInitialThemeState();

            environment.afterWrite = [&](bool) {
                environment.failSystemRead = environment.failAppsRead = true;
            };
            environment.nightLight = true;
            manager.OnNightLightChange();
            Assert::IsFalse(*environment.system);
            Assert::IsFalse(*environment.apps);
            Assert::AreEqual(2, environment.writes);

            environment.afterWrite = nullptr;
            environment.failSystemRead = environment.failAppsRead = false;
            environment.nightLight = false;
            manager.DetectExternalThemeChange();
            manager.OnTick();

            Assert::IsFalse(manager.GetState().isManualOverride);
            Assert::IsTrue(*environment.system);
            Assert::IsTrue(*environment.apps);
            Assert::AreEqual(4, environment.writes);
        }

        TEST_METHOD (UnverifiedToggleWritesDoNotInventAnExternalOverride)
        {
            Environment environment;
            LightSwitchStateManager manager(environment.Dependencies());
            manager.SyncInitialThemeState();

            environment.afterWrite = [&](bool) {
                environment.failSystemRead = environment.failAppsRead = true;
            };
            const auto result = manager.ToggleTheme();
            Assert::IsFalse(result.success);
            Assert::IsFalse(*environment.system);
            Assert::IsFalse(*environment.apps);
            Assert::AreEqual(2, environment.writes);

            environment.afterWrite = nullptr;
            environment.failSystemRead = environment.failAppsRead = false;
            manager.DetectExternalThemeChange();
            manager.OnTick();

            Assert::IsFalse(manager.GetState().isManualOverride);
            Assert::IsTrue(*environment.system);
            Assert::IsTrue(*environment.apps);
            Assert::AreEqual(4, environment.writes);
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

            // The service's partial write is not an external manual override.
            environment.failAppsWrite = false;
            manager.DetectExternalThemeChange();
            manager.OnTick();
            const auto retry = manager.OnSettingsChanged();
            Assert::IsTrue(retry.success);
            Assert::IsFalse(retry.status.manualOverride);
            Assert::IsFalse(*retry.status.systemLight);
            Assert::IsFalse(*retry.status.appsLight);
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
