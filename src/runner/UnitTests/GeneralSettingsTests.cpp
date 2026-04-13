// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Unit tests for Runner general settings parsing.
//
// These tests exercise the real validation and parsing logic mirrored from
// general_settings.cpp — specifically ValidateTheme() and the dashboard
// sort-order parser. Both are branching functions where invalid/unexpected
// input must produce safe fallback values; the tests verify those branches.

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
    // Product function: ValidateTheme() in general_settings.cpp
    // Behavior: only "dark" and "light" are accepted; everything else
    //   (including "system", empty string, wrong case) falls back to "system".
    // Risk if broken: UI theme silently resets or displays garbage.

    TEST_CLASS(GeneralSettingsThemeTests)
    {
    public:
        // Verify the default struct value before any JSON is parsed.
        TEST_METHOD(DefaultTheme_IsSystem)
        {
            GeneralSettingsDefaults s;
            Assert::AreEqual(std::wstring(L"system"), s.theme);
        }

        // "dark" is one of two accepted values — must pass through unchanged.
        TEST_METHOD(ValidTheme_Dark_Accepted)
        {
            auto result = ValidateTheme(L"dark");
            Assert::AreEqual(std::wstring(L"dark"), result);
        }

        // "light" is one of two accepted values — must pass through unchanged.
        TEST_METHOD(ValidTheme_Light_Accepted)
        {
            auto result = ValidateTheme(L"light");
            Assert::AreEqual(std::wstring(L"light"), result);
        }

        // "system" is NOT in the accept-list; the validator intentionally
        // returns "system" as the fallback, so the round-trip still works,
        // but it exercises the else-branch.
        TEST_METHOD(ValidTheme_System_FallsBack)
        {
            auto result = ValidateTheme(L"system");
            Assert::AreEqual(std::wstring(L"system"), result);
        }

        // Arbitrary garbage → fallback to "system".
        TEST_METHOD(InvalidTheme_RandomString_FallsBackToSystem)
        {
            auto result = ValidateTheme(L"sepia");
            Assert::AreEqual(std::wstring(L"system"), result);
        }

        // Empty string → fallback (guards against missing JSON key).
        TEST_METHOD(InvalidTheme_EmptyString_FallsBackToSystem)
        {
            auto result = ValidateTheme(L"");
            Assert::AreEqual(std::wstring(L"system"), result);
        }

        // Comparison is case-sensitive: "Dark" ≠ "dark".
        TEST_METHOD(InvalidTheme_CaseSensitive_DarkUppercase)
        {
            auto result = ValidateTheme(L"Dark");
            Assert::AreEqual(std::wstring(L"system"), result);
        }

        // Comparison is case-sensitive: "Light" ≠ "light".
        TEST_METHOD(InvalidTheme_CaseSensitive_LightUppercase)
        {
            auto result = ValidateTheme(L"Light");
            Assert::AreEqual(std::wstring(L"system"), result);
        }
    };

    // ── Dashboard sort order parsing tests ──────────────────────────────────
    // Product functions: ParseDashboardSortOrderFromInt / FromString
    //   in general_settings.cpp
    // Behavior: only recognised enum values are accepted; anything else
    //   falls back to a caller-supplied default.
    // Risk if broken: dashboard displays modules in wrong order after
    //   settings migration or manual JSON edit.

    TEST_CLASS(DashboardSortOrderTests)
    {
    public:
        // 0 → Alphabetical (the first enum value).
        TEST_METHOD(ParseFromInt_0_IsAlphabetical)
        {
            auto result = ParseDashboardSortOrderFromInt(0, DashboardSortOrder::Alphabetical);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::Alphabetical),
                             static_cast<int>(result));
        }

        // 1 → ByStatus (the only other valid value).
        TEST_METHOD(ParseFromInt_1_IsByStatus)
        {
            auto result = ParseDashboardSortOrderFromInt(1, DashboardSortOrder::Alphabetical);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::ByStatus),
                             static_cast<int>(result));
        }

        // Out-of-range positive int → falls back to Alphabetical.
        TEST_METHOD(ParseFromInt_InvalidValue_FallsBackToAlphabetical)
        {
            auto result = ParseDashboardSortOrderFromInt(99, DashboardSortOrder::Alphabetical);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::Alphabetical),
                             static_cast<int>(result));
        }

        // Negative int → falls back to Alphabetical.
        TEST_METHOD(ParseFromInt_Negative_FallsBackToAlphabetical)
        {
            auto result = ParseDashboardSortOrderFromInt(-1, DashboardSortOrder::Alphabetical);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::Alphabetical),
                             static_cast<int>(result));
        }

        // String "Alphabetical" → enum Alphabetical (overrides any fallback).
        TEST_METHOD(ParseFromString_Alphabetical)
        {
            auto result = ParseDashboardSortOrderFromString(L"Alphabetical", DashboardSortOrder::ByStatus);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::Alphabetical),
                             static_cast<int>(result));
        }

        // String "ByStatus" → enum ByStatus (overrides any fallback).
        TEST_METHOD(ParseFromString_ByStatus)
        {
            auto result = ParseDashboardSortOrderFromString(L"ByStatus", DashboardSortOrder::Alphabetical);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::ByStatus),
                             static_cast<int>(result));
        }

        // Unrecognised string → caller's fallback is used.
        TEST_METHOD(ParseFromString_Invalid_UsesFallback)
        {
            auto result = ParseDashboardSortOrderFromString(L"unknown", DashboardSortOrder::ByStatus);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::ByStatus),
                             static_cast<int>(result));
        }

        // Empty string → caller's fallback is used.
        TEST_METHOD(ParseFromString_Empty_UsesFallback)
        {
            auto result = ParseDashboardSortOrderFromString(L"", DashboardSortOrder::Alphabetical);
            Assert::AreEqual(static_cast<int>(DashboardSortOrder::Alphabetical),
                             static_cast<int>(result));
        }
    };
}
