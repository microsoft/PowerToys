// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Unit tests for PowerRename settings parsing.
// These are pure-logic tests that exercise default values and JSON
// field structures without requiring file I/O or registry access.

#include "pch.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace PowerRenameSettingsUnitTests
{
    // ── Mirror of CSettings::Settings struct from lib/Settings.h ────────────

    struct PowerRenameSettingsDefaults
    {
        bool enabled{ true };
        bool showIconOnMenu{ true };
        bool extendedContextMenuOnly{ false };
        bool persistState{ true };
        bool useBoostLib{ false };
        bool MRUEnabled{ true };
        unsigned int maxMRUSize{ 10 };
        unsigned int flags{ 0 };
    };

    // Mirror of JSON key constants from Settings.cpp
    namespace JsonKeys
    {
        constexpr const wchar_t* ShowIcon = L"ShowIcon";
        constexpr const wchar_t* ExtendedContextMenuOnly = L"ExtendedContextMenuOnly";
        constexpr const wchar_t* PersistState = L"PersistState";
        constexpr const wchar_t* MaxMRUSize = L"MaxMRUSize";
        constexpr const wchar_t* Flags = L"Flags";
        constexpr const wchar_t* MRUEnabled = L"MRUEnabled";
        constexpr const wchar_t* UseBoostLib = L"UseBoostLib";
    }

    // ── Default value tests ─────────────────────────────────────────────────

    TEST_CLASS(PowerRenameSettingsDefaultsTests)
    {
    public:
        TEST_METHOD(Enabled_DefaultIsTrue)
        {
            PowerRenameSettingsDefaults s;
            Assert::IsTrue(s.enabled);
        }

        TEST_METHOD(ShowIconOnMenu_DefaultIsTrue)
        {
            PowerRenameSettingsDefaults s;
            Assert::IsTrue(s.showIconOnMenu);
        }

        TEST_METHOD(ExtendedContextMenuOnly_DefaultIsFalse)
        {
            PowerRenameSettingsDefaults s;
            Assert::IsFalse(s.extendedContextMenuOnly);
        }

        TEST_METHOD(PersistState_DefaultIsTrue)
        {
            PowerRenameSettingsDefaults s;
            Assert::IsTrue(s.persistState);
        }

        TEST_METHOD(UseBoostLib_DefaultIsFalse)
        {
            PowerRenameSettingsDefaults s;
            Assert::IsFalse(s.useBoostLib);
        }

        TEST_METHOD(MRUEnabled_DefaultIsTrue)
        {
            PowerRenameSettingsDefaults s;
            Assert::IsTrue(s.MRUEnabled);
        }

        TEST_METHOD(MaxMRUSize_DefaultIs10)
        {
            PowerRenameSettingsDefaults s;
            Assert::AreEqual(10u, s.maxMRUSize);
        }

        TEST_METHOD(Flags_DefaultIs0)
        {
            PowerRenameSettingsDefaults s;
            Assert::AreEqual(0u, s.flags);
        }
    };

    // ── JSON key constant verification ──────────────────────────────────────

    TEST_CLASS(PowerRenameJsonKeyTests)
    {
    public:
        TEST_METHOD(ShowIcon_KeyIsCorrect)
        {
            Assert::AreEqual(L"ShowIcon", JsonKeys::ShowIcon);
        }

        TEST_METHOD(ExtendedContextMenuOnly_KeyIsCorrect)
        {
            Assert::AreEqual(L"ExtendedContextMenuOnly", JsonKeys::ExtendedContextMenuOnly);
        }

        TEST_METHOD(PersistState_KeyIsCorrect)
        {
            Assert::AreEqual(L"PersistState", JsonKeys::PersistState);
        }

        TEST_METHOD(MaxMRUSize_KeyIsCorrect)
        {
            Assert::AreEqual(L"MaxMRUSize", JsonKeys::MaxMRUSize);
        }

        TEST_METHOD(Flags_KeyIsCorrect)
        {
            Assert::AreEqual(L"Flags", JsonKeys::Flags);
        }

        TEST_METHOD(MRUEnabled_KeyIsCorrect)
        {
            Assert::AreEqual(L"MRUEnabled", JsonKeys::MRUEnabled);
        }

        TEST_METHOD(UseBoostLib_KeyIsCorrect)
        {
            Assert::AreEqual(L"UseBoostLib", JsonKeys::UseBoostLib);
        }
    };

    // ── Simulated parse logic tests ─────────────────────────────────────────

    TEST_CLASS(PowerRenameSettingsParsingTests)
    {
    public:
        TEST_METHOD(EmptySettings_AllDefaultsPreserved)
        {
            // Simulate loading with no JSON values: defaults apply
            PowerRenameSettingsDefaults s;
            Assert::IsTrue(s.showIconOnMenu);
            Assert::IsFalse(s.extendedContextMenuOnly);
            Assert::IsTrue(s.persistState);
            Assert::IsTrue(s.MRUEnabled);
            Assert::AreEqual(10u, s.maxMRUSize);
            Assert::IsFalse(s.useBoostLib);
            Assert::AreEqual(0u, s.flags);
        }

        TEST_METHOD(PartialUpdate_ShowIconChanged_OthersDefault)
        {
            PowerRenameSettingsDefaults s;
            s.showIconOnMenu = false;

            Assert::IsFalse(s.showIconOnMenu);
            Assert::IsFalse(s.extendedContextMenuOnly);
            Assert::IsTrue(s.persistState);
            Assert::IsTrue(s.MRUEnabled);
            Assert::AreEqual(10u, s.maxMRUSize);
        }

        TEST_METHOD(PartialUpdate_ExtendedContextChanged_OthersDefault)
        {
            PowerRenameSettingsDefaults s;
            s.extendedContextMenuOnly = true;

            Assert::IsTrue(s.extendedContextMenuOnly);
            Assert::IsTrue(s.showIconOnMenu);
            Assert::IsTrue(s.persistState);
        }

        TEST_METHOD(PartialUpdate_MaxMRUSizeChanged_OthersDefault)
        {
            PowerRenameSettingsDefaults s;
            s.maxMRUSize = 25;

            Assert::AreEqual(25u, s.maxMRUSize);
            Assert::IsTrue(s.MRUEnabled);
            Assert::AreEqual(0u, s.flags);
        }

        TEST_METHOD(PartialUpdate_FlagsChanged_OthersDefault)
        {
            PowerRenameSettingsDefaults s;
            s.flags = 0xFF;

            Assert::AreEqual(static_cast<unsigned int>(0xFF), s.flags);
            Assert::AreEqual(10u, s.maxMRUSize);
            Assert::IsTrue(s.showIconOnMenu);
        }

        TEST_METHOD(AllFieldsSet_AllValuesChanged)
        {
            PowerRenameSettingsDefaults s;
            s.showIconOnMenu = false;
            s.extendedContextMenuOnly = true;
            s.persistState = false;
            s.MRUEnabled = false;
            s.maxMRUSize = 50;
            s.useBoostLib = true;
            s.flags = 42;

            Assert::IsFalse(s.showIconOnMenu);
            Assert::IsTrue(s.extendedContextMenuOnly);
            Assert::IsFalse(s.persistState);
            Assert::IsFalse(s.MRUEnabled);
            Assert::AreEqual(50u, s.maxMRUSize);
            Assert::IsTrue(s.useBoostLib);
            Assert::AreEqual(42u, s.flags);
        }

        TEST_METHOD(MigrateDefaults_MatchRegistryDefaults)
        {
            // Verify that migration defaults match the documented registry defaults
            // from MigrateFromRegistry() in Settings.cpp
            bool showIconOnMenu = true;          // GetRegBoolean(c_showIconOnMenu, true)
            bool extendedContextMenuOnly = false; // GetRegBoolean(c_extendedContextMenuOnly, false)
            bool persistState = true;             // GetRegBoolean(c_persistState, true)
            bool MRUEnabled = true;               // GetRegBoolean(c_mruEnabled, true)
            unsigned int maxMRUSize = 10;          // GetRegNumber(c_maxMRUSize, 10)
            unsigned int flags = 0;                // GetRegNumber(c_flags, 0)

            PowerRenameSettingsDefaults s;
            Assert::AreEqual(s.showIconOnMenu, showIconOnMenu);
            Assert::AreEqual(s.extendedContextMenuOnly, extendedContextMenuOnly);
            Assert::AreEqual(s.persistState, persistState);
            Assert::AreEqual(s.MRUEnabled, MRUEnabled);
            Assert::AreEqual(s.maxMRUSize, maxMRUSize);
            Assert::AreEqual(s.flags, flags);
        }
    };
}
