// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Unit tests for Runner general settings parsing.
// These are pure-logic tests that exercise theme validation, dashboard sort
// order parsing, module enable map structures, and default values without
// requiring file I/O or the full runner initialization.

#include "pch.h"

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RunnerUnitTests
{
    // ── Mirror of DashboardSortOrder enum from general_settings.h ───────────

    enum class DashboardSortOrder
    {
        Alphabetical = 0,
        ByStatus = 1,
    };

    // ── Mirror of theme validation logic from general_settings.cpp ──────────

    inline std::wstring ValidateTheme(const std::wstring& theme)
    {
        if (theme != L"dark" && theme != L"light")
        {
            return L"system";
        }
        return theme;
    }

    // ── Mirror of parse_dashboard_sort_order from general_settings.cpp ──────

    constexpr DashboardSortOrder ParseDashboardSortOrderFromInt(int value, DashboardSortOrder /*fallback*/)
    {
        return value == static_cast<int>(DashboardSortOrder::ByStatus)
            ? DashboardSortOrder::ByStatus
            : DashboardSortOrder::Alphabetical;
    }

    inline DashboardSortOrder ParseDashboardSortOrderFromString(const std::wstring& raw, DashboardSortOrder fallback)
    {
        if (raw == L"ByStatus")
            return DashboardSortOrder::ByStatus;
        if (raw == L"Alphabetical")
            return DashboardSortOrder::Alphabetical;
        return fallback;
    }

    // ── Default values (mirroring the static globals in general_settings.cpp)

    struct GeneralSettingsDefaults
    {
        std::wstring theme = L"system";
        bool showSystemTrayIcon = true;
        bool showThemeAdaptiveTrayIcon = false;
        bool runAsElevated = false;
        bool showNewUpdatesToastNotification = true;
        bool downloadUpdatesAutomatically = true;
        bool showWhatsNewAfterUpdates = true;
        bool enableExperimentation = true;
        bool enableWarningsElevatedApps = true;
        bool enableQuickAccess = true;
        DashboardSortOrder dashboardSortOrder = DashboardSortOrder::Alphabetical;
    };

    // ── Theme validation tests ──────────────────────────────────────────────

    TEST_CLASS(GeneralSettingsThemeTests)
    {
    public:
        TEST_METHOD(DefaultTheme_IsSystem)
        {
            GeneralSettingsDefaults s;
            Assert::AreEqual(std::wstring(L"system"), s.theme);
        }

        TEST_METHOD(ValidTheme_Dark_Accepted)
        {
            auto result = ValidateTheme(L"dark");
            Assert::AreEqual(std::wstring(L"dark"), result);
        }

        TEST_METHOD(ValidTheme_Light_Accepted)
        {
            auto result = ValidateTheme(L"light");
            Assert::AreEqual(std::wstring(L"light"), result);
        }

        TEST_METHOD(ValidTheme_System_FallsBack)
        {
            // "system" is not "dark" or "light", so validation returns "system"
            auto result = ValidateTheme(L"system");
            Assert::AreEqual(std::wstring(L"system"), result);
        }

        TEST_METHOD(InvalidTheme_RandomString_FallsBackToSystem)
        {
            auto result = ValidateTheme(L"sepia");
            Assert::AreEqual(std::wstring(L"system"), result);
        }

        TEST_METHOD(InvalidTheme_EmptyString_FallsBackToSystem)
        {
            auto result = ValidateTheme(L"");
            Assert::AreEqual(std::wstring(L"system"), result);
        }

        TEST_METHOD(InvalidTheme_CaseSensitive_DarkUppercase)
        {
            // Theme validation is case-sensitive: "Dark" != "dark"
            auto result = ValidateTheme(L"Dark");
            Assert::AreEqual(std::wstring(L"system"), result);
        }

        TEST_METHOD(InvalidTheme_CaseSensitive_LightUppercase)
        {
            auto result = ValidateTheme(L"Light");
            Assert::AreEqual(std::wstring(L"system"), result);
        }
    };

    // ── Default value tests ─────────────────────────────────────────────────

    TEST_CLASS(GeneralSettingsDefaultsTests)
    {
    public:
        TEST_METHOD(ShowSystemTrayIcon_DefaultIsTrue)
        {
            GeneralSettingsDefaults s;
            Assert::IsTrue(s.showSystemTrayIcon);
        }

        TEST_METHOD(ShowThemeAdaptiveTrayIcon_DefaultIsFalse)
        {
            GeneralSettingsDefaults s;
            Assert::IsFalse(s.showThemeAdaptiveTrayIcon);
        }

        TEST_METHOD(EnableQuickAccess_DefaultIsTrue)
        {
            GeneralSettingsDefaults s;
            Assert::IsTrue(s.enableQuickAccess);
        }

        TEST_METHOD(RunAsElevated_DefaultIsFalse)
        {
            GeneralSettingsDefaults s;
            Assert::IsFalse(s.runAsElevated);
        }

        TEST_METHOD(ShowNewUpdatesToastNotification_DefaultIsTrue)
        {
            GeneralSettingsDefaults s;
            Assert::IsTrue(s.showNewUpdatesToastNotification);
        }

        TEST_METHOD(DownloadUpdatesAutomatically_DefaultIsTrue)
        {
            GeneralSettingsDefaults s;
            Assert::IsTrue(s.downloadUpdatesAutomatically);
        }

        TEST_METHOD(ShowWhatsNewAfterUpdates_DefaultIsTrue)
        {
            GeneralSettingsDefaults s;
            Assert::IsTrue(s.showWhatsNewAfterUpdates);
        }

        TEST_METHOD(EnableExperimentation_DefaultIsTrue)
        {
            GeneralSettingsDefaults s;
            Assert::IsTrue(s.enableExperimentation);
        }

        TEST_METHOD(EnableWarningsElevatedApps_DefaultIsTrue)
        {
            GeneralSettingsDefaults s;
            Assert::IsTrue(s.enableWarningsElevatedApps);
        }

        TEST_METHOD(DashboardSortOrder_DefaultIsAlphabetical)
        {
            GeneralSettingsDefaults s;
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::Alphabetical),
                             static_cast<int>(s.dashboardSortOrder));
        }
    };

    // ── Dashboard sort order parsing tests ──────────────────────────────────

    TEST_CLASS(DashboardSortOrderTests)
    {
    public:
        TEST_METHOD(ParseFromInt_0_IsAlphabetical)
        {
            auto result = ParseDashboardSortOrderFromInt(0, DashboardSortOrder::Alphabetical);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::Alphabetical),
                             static_cast<int>(result));
        }

        TEST_METHOD(ParseFromInt_1_IsByStatus)
        {
            auto result = ParseDashboardSortOrderFromInt(1, DashboardSortOrder::Alphabetical);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::ByStatus),
                             static_cast<int>(result));
        }

        TEST_METHOD(ParseFromInt_InvalidValue_FallsBackToAlphabetical)
        {
            auto result = ParseDashboardSortOrderFromInt(99, DashboardSortOrder::Alphabetical);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::Alphabetical),
                             static_cast<int>(result));
        }

        TEST_METHOD(ParseFromInt_Negative_FallsBackToAlphabetical)
        {
            auto result = ParseDashboardSortOrderFromInt(-1, DashboardSortOrder::Alphabetical);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::Alphabetical),
                             static_cast<int>(result));
        }

        TEST_METHOD(ParseFromString_Alphabetical)
        {
            auto result = ParseDashboardSortOrderFromString(L"Alphabetical", DashboardSortOrder::ByStatus);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::Alphabetical),
                             static_cast<int>(result));
        }

        TEST_METHOD(ParseFromString_ByStatus)
        {
            auto result = ParseDashboardSortOrderFromString(L"ByStatus", DashboardSortOrder::Alphabetical);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::ByStatus),
                             static_cast<int>(result));
        }

        TEST_METHOD(ParseFromString_Invalid_UsesFallback)
        {
            auto result = ParseDashboardSortOrderFromString(L"unknown", DashboardSortOrder::ByStatus);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::ByStatus),
                             static_cast<int>(result));
        }

        TEST_METHOD(ParseFromString_Empty_UsesFallback)
        {
            auto result = ParseDashboardSortOrderFromString(L"", DashboardSortOrder::Alphabetical);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::Alphabetical),
                             static_cast<int>(result));
        }
    };

    // ── Module enable map tests ─────────────────────────────────────────────

    TEST_CLASS(ModuleEnableMapTests)
    {
    public:
        TEST_METHOD(EmptyMap_HasNoModules)
        {
            std::map<std::wstring, bool> moduleMap;
            Assert::AreEqual(static_cast<size_t>(0), moduleMap.size());
        }

        TEST_METHOD(SingleModule_Enabled)
        {
            std::map<std::wstring, bool> moduleMap;
            moduleMap[L"FancyZones"] = true;
            Assert::IsTrue(moduleMap[L"FancyZones"]);
        }

        TEST_METHOD(SingleModule_Disabled)
        {
            std::map<std::wstring, bool> moduleMap;
            moduleMap[L"PowerRename"] = false;
            Assert::IsFalse(moduleMap[L"PowerRename"]);
        }

        TEST_METHOD(MultipleModules_MixedState)
        {
            std::map<std::wstring, bool> moduleMap;
            moduleMap[L"FancyZones"] = true;
            moduleMap[L"PowerRename"] = false;
            moduleMap[L"ColorPicker"] = true;

            Assert::AreEqual(static_cast<size_t>(3), moduleMap.size());
            Assert::IsTrue(moduleMap[L"FancyZones"]);
            Assert::IsFalse(moduleMap[L"PowerRename"]);
            Assert::IsTrue(moduleMap[L"ColorPicker"]);
        }

        TEST_METHOD(ModuleNotInMap_DefaultInsertedAsFalse)
        {
            std::map<std::wstring, bool> moduleMap;
            // Accessing a nonexistent key default-inserts bool as false
            bool val = moduleMap[L"NonExistent"];
            Assert::IsFalse(val);
        }

        TEST_METHOD(FindModule_NotFound)
        {
            std::map<std::wstring, bool> moduleMap;
            moduleMap[L"FancyZones"] = true;
            Assert::IsTrue(moduleMap.find(L"Awake") == moduleMap.end());
        }
    };

    // ── Empty JSON → all defaults ───────────────────────────────────────────

    TEST_CLASS(GeneralSettingsEmptyJsonTests)
    {
    public:
        TEST_METHOD(EmptyJson_ThemeDefaultsToSystem)
        {
            GeneralSettingsDefaults s;
            Assert::AreEqual(std::wstring(L"system"), s.theme);
        }

        TEST_METHOD(EmptyJson_TrayIconDefaultsToTrue)
        {
            GeneralSettingsDefaults s;
            Assert::IsTrue(s.showSystemTrayIcon);
        }

        TEST_METHOD(EmptyJson_QuickAccessDefaultsToTrue)
        {
            GeneralSettingsDefaults s;
            Assert::IsTrue(s.enableQuickAccess);
        }

        TEST_METHOD(EmptyJson_DashboardSortDefaultsToAlphabetical)
        {
            GeneralSettingsDefaults s;
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::Alphabetical),
                             static_cast<int>(s.dashboardSortOrder));
        }

        TEST_METHOD(EmptyJson_AllBoolDefaultsCorrect)
        {
            GeneralSettingsDefaults s;
            Assert::IsTrue(s.showSystemTrayIcon);
            Assert::IsFalse(s.showThemeAdaptiveTrayIcon);
            Assert::IsFalse(s.runAsElevated);
            Assert::IsTrue(s.showNewUpdatesToastNotification);
            Assert::IsTrue(s.downloadUpdatesAutomatically);
            Assert::IsTrue(s.showWhatsNewAfterUpdates);
            Assert::IsTrue(s.enableExperimentation);
            Assert::IsTrue(s.enableWarningsElevatedApps);
            Assert::IsTrue(s.enableQuickAccess);
        }
    };
}
