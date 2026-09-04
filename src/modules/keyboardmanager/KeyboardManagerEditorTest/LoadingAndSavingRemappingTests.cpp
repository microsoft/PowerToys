#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <keyboardmanager/common/MappingConfiguration.h>
#include <keyboardmanager/KeyboardManagerEditorLibrary/LoadingAndSavingRemappingHelper.h>
#include <common/interop/shared_constants.h>
#include <keyboardmanager/KeyboardManagerEditorLibrary/ShortcutErrorType.h>

#include <filesystem>
#include <fstream>
#include <string_view>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace
{
    constexpr wchar_t TextExpansionId1[] = L"11111111-1111-4111-8111-111111111111";
    constexpr wchar_t TextExpansionId2[] = L"22222222-2222-4222-8222-222222222222";
    constexpr wchar_t TextExpansionId3[] = L"33333333-3333-4333-8333-333333333333";

    std::filesystem::path CreateUniqueProfileTestPath(const std::wstring_view suffix)
    {
        return std::filesystem::temp_directory_path() /
               (L"PowerToys-KBM-TextExpansion-" + std::to_wstring(GetCurrentProcessId()) + L"-" + std::to_wstring(GetTickCount64()) + L"-" + std::wstring{ suffix });
    }

    struct ScopedProfileTestPath
    {
        explicit ScopedProfileTestPath(const std::wstring_view suffix) :
            path(CreateUniqueProfileTestPath(suffix))
        {
            std::error_code error;
            std::filesystem::remove(path, error);
        }

        ~ScopedProfileTestPath()
        {
            std::error_code error;
            std::filesystem::remove(path, error);
        }

        std::filesystem::path path;
    };

    json::JsonObject CreateEmptyMappingProfile(const bool includeTextExpansions = true)
    {
        json::JsonObject profile;

        json::JsonObject remapKeys;
        remapKeys.SetNamedValue(KeyboardManagerConstants::InProcessRemapKeysSettingName, json::JsonArray{});
        profile.SetNamedValue(KeyboardManagerConstants::RemapKeysSettingName, remapKeys);

        json::JsonObject remapKeysToText;
        remapKeysToText.SetNamedValue(KeyboardManagerConstants::InProcessRemapKeysSettingName, json::JsonArray{});
        profile.SetNamedValue(KeyboardManagerConstants::RemapKeysToTextSettingName, remapKeysToText);

        if (includeTextExpansions)
        {
            json::JsonObject textExpansions;
            textExpansions.SetNamedValue(KeyboardManagerConstants::InProcessRemapKeysSettingName, json::JsonArray{});
            profile.SetNamedValue(KeyboardManagerConstants::TextReplacementsSettingName, textExpansions);
        }

        for (const auto& sectionName : { KeyboardManagerConstants::RemapShortcutsSettingName, KeyboardManagerConstants::RemapShortcutsToTextSettingName })
        {
            json::JsonObject shortcuts;
            shortcuts.SetNamedValue(KeyboardManagerConstants::GlobalRemapShortcutsSettingName, json::JsonArray{});
            shortcuts.SetNamedValue(KeyboardManagerConstants::AppSpecificRemapShortcutsSettingName, json::JsonArray{});
            profile.SetNamedValue(sectionName, shortcuts);
        }

        return profile;
    }

    json::JsonArray CreateActivationJson(std::initializer_list<double> keys)
    {
        json::JsonArray activation;
        for (const auto key : keys)
        {
            activation.Append(json::value(key));
        }

        return activation;
    }

    json::JsonObject CreateTextExpansionJson(
        const std::wstring& id,
        const std::wstring& sourceText,
        const json::JsonArray& activationKeys,
        const std::wstring& replacementText,
        const bool enabled)
    {
        json::JsonObject rule;
        rule.SetNamedValue(KeyboardManagerConstants::TextExpansionIdSettingName, json::value(id));
        rule.SetNamedValue(KeyboardManagerConstants::TextExpansionSourceTextSettingName, json::value(sourceText));
        rule.SetNamedValue(KeyboardManagerConstants::TextExpansionActivationKeysSettingName, activationKeys);
        rule.SetNamedValue(KeyboardManagerConstants::TextExpansionReplacementTextSettingName, json::value(replacementText));
        rule.SetNamedValue(KeyboardManagerConstants::TextExpansionEnabledSettingName, json::value(enabled));
        return rule;
    }

    json::JsonArray GetTextExpansionArray(const json::JsonObject& profile)
    {
        return profile.GetNamedObject(KeyboardManagerConstants::TextReplacementsSettingName)
            .GetNamedArray(KeyboardManagerConstants::InProcessRemapKeysSettingName);
    }

    Shortcut CreateActivation(std::initializer_list<int32_t> keys)
    {
        return Shortcut(std::vector<int32_t>(keys));
    }

    TextExpansionRule CreateTextExpansionRule(
        const std::wstring& id,
        const std::wstring& sourceText = L"brb",
        const Shortcut& activation = Shortcut(VK_SPACE),
        const std::wstring& replacementText = L"be right back",
        const bool enabled = true)
    {
        TextExpansionRule rule;
        rule.id = id;
        rule.sourceText = sourceText;
        rule.activation = activation;
        rule.replacementText = replacementText;
        rule.enabled = enabled;
        return rule;
    }
}

namespace RemappingUITests
{
    // Tests for methods in the LoadingAndSavingRemappingHelper namespace
    TEST_CLASS (LoadingAndSavingRemappingTests)
    {
        std::wstring testApp1 = L"testprocess1.exe";
        std::wstring testApp2 = L"testprocess2.exe";

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
        }

        // Test if the CheckIfRemappingsAreValid method is successful when no remaps are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnNoError_OnPassingNoRemaps)
        {
            RemapBuffer remapBuffer;

            // Assert that remapping set is valid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == ShortcutErrorType::NoError);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is successful when valid key to key remaps are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnNoError_OnPassingValidKeyToKeyRemaps)
        {
            RemapBuffer remapBuffer;

            // Remap A to B and B to C
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x41, (DWORD)0x42 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x42, (DWORD)0x43 }), std::wstring() });

            // Assert that remapping set is valid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == ShortcutErrorType::NoError);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is successful when valid key to shortcut remaps are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnNoError_OnPassingValidKeyToShortcutRemaps)
        {
            RemapBuffer remapBuffer;

            // Remap A to Ctrl+V and B to Alt+Tab
            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(0x56);
            Shortcut s2;
            s2.SetKey(VK_MENU);
            s2.SetKey(VK_TAB);
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x41, s1 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x42, s2 }), std::wstring() });

            // Assert that remapping set is valid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == ShortcutErrorType::NoError);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is successful when valid shortcut to key remaps are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnNoError_OnPassingValidShortcutToKeyRemaps)
        {
            RemapBuffer remapBuffer;

            // Remap Ctrl+V to A and Alt+Tab to B
            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(0x56);
            Shortcut s2;
            s2.SetKey(VK_MENU);
            s2.SetKey(VK_TAB);
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ s1, (DWORD)0x41 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ s2, (DWORD)0x42 }), std::wstring() });

            // Assert that remapping set is valid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == ShortcutErrorType::NoError);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is successful when valid shortcut to shortcut remaps are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnNoError_OnPassingValidShortcutToShortcutRemaps)
        {
            RemapBuffer remapBuffer;

            // Remap Ctrl+V to Ctrl+D and Alt+Tab to Win+A
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x56);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x44);
            Shortcut src2;
            src2.SetKey(VK_MENU);
            src2.SetKey(VK_TAB);
            Shortcut dest2;
            dest2.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            dest2.SetKey(0x41);
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src1, dest1 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src2, dest2 }), std::wstring() });

            // Assert that remapping set is valid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == ShortcutErrorType::NoError);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is successful when valid remaps are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnNoError_OnPassingValidRemapsOfAllTypes)
        {
            RemapBuffer remapBuffer;

            // Remap Ctrl+V to Ctrl+D, Alt+Tab to A, A to B and B to Win+A
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x56);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x44);
            Shortcut src2;
            src2.SetKey(VK_MENU);
            src2.SetKey(VK_TAB);
            Shortcut dest2;
            dest2.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            dest2.SetKey(0x41);
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src1, dest1 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src2, (DWORD)0x41 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x41, (DWORD)0x42 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x42, dest2 }), std::wstring() });

            // Assert that remapping set is valid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == ShortcutErrorType::NoError);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is unsuccessful when remaps with null keys are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnRemapUnsuccessful_OnPassingRemapsWithNullKeys)
        {
            RemapBuffer remapBuffer;

            // Remap A to NULL
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x41, (DWORD)0 }), std::wstring() });

            // Assert that remapping set is invalid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == ShortcutErrorType::RemapUnsuccessful);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is unsuccessful when remaps with invalid shortcuts are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnRemapUnsuccessful_OnPassingRemapsWithInvalidShortcut)
        {
            RemapBuffer remapBuffer;

            // Remap A to incomplete shortcut (Ctrl)
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x41, src1 }), std::wstring() });

            // Assert that remapping set is invalid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == ShortcutErrorType::RemapUnsuccessful);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is unsuccessful when remaps with the same key remapped twice are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnRemapUnsuccessful_OnPassingRemapsWithSameKeyRemappedTwice)
        {
            RemapBuffer remapBuffer;

            // Remap A to B and A to Ctrl+C
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x43);
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x41, (DWORD)0x42 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x41, src1 }), std::wstring() });

            // Assert that remapping set is invalid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == ShortcutErrorType::RemapUnsuccessful);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is unsuccessful when remaps with the same shortcut remapped twice are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnRemapUnsuccessful_OnPassingRemapsWithSameShortcutRemappedTwice)
        {
            RemapBuffer remapBuffer;

            // Remap Ctrl+A to B and Ctrl+A to Ctrl+V
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x41);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x56);
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src1, (DWORD)0x42 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src1, dest1 }), std::wstring() });

            // Assert that remapping set is invalid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == ShortcutErrorType::RemapUnsuccessful);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is unsuccessful when app specific remaps with the same shortcut remapped twice for the same target app are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnRemapUnsuccessful_OnPassingAppSpecificRemapsWithSameShortcutRemappedTwiceForTheSameTargetApp)
        {
            RemapBuffer remapBuffer;

            // Remap Ctrl+A to B and Ctrl+A to Ctrl+V for testApp1
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x41);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x56);
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src1, (DWORD)0x42 }), testApp1 });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src1, dest1 }), testApp1 });

            // Assert that remapping set is invalid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == ShortcutErrorType::RemapUnsuccessful);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is successful when app specific remaps with the same shortcut remapped twice for different target apps are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnNoError_OnPassingAppSpecificRemapsWithSameShortcutRemappedTwiceForDifferentTargetApps)
        {
            RemapBuffer remapBuffer;

            // Remap Ctrl+A to B for testApp1 and Ctrl+A to Ctrl+V for testApp2
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x41);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x56);
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src1, (DWORD)0x42 }), testApp1 });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src1, dest1 }), testApp2 });

            // Assert that remapping set is valid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == ShortcutErrorType::NoError);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the GetOrphanedKeys method return an empty vector on passing no remaps
        TEST_METHOD (GetOrphanedKeys_ShouldReturnEmptyVector_OnPassingNoRemaps)
        {
            RemapBuffer remapBuffer;

            // Assert that there are no orphaned keys
            Assert::AreEqual(true, LoadingAndSavingRemappingHelper::GetOrphanedKeys(remapBuffer).empty());
        }

        // Test if the GetOrphanedKeys method return one orphaned on passing one key remap
        TEST_METHOD (GetOrphanedKeys_ShouldReturnOneOrphanedKey_OnPassingOneKeyRemap)
        {
            RemapBuffer remapBuffer;

            // Remap A to B
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x41, (DWORD)0x42 }), std::wstring() });

            // Assert that only A is orphaned
            Assert::AreEqual((size_t)1, LoadingAndSavingRemappingHelper::GetOrphanedKeys(remapBuffer).size());
            Assert::AreEqual((DWORD)0x41, LoadingAndSavingRemappingHelper::GetOrphanedKeys(remapBuffer)[0]);
        }

        // Test if the GetOrphanedKeys method return an empty vector on passing swapped key remaps
        TEST_METHOD (GetOrphanedKeys_ShouldReturnEmptyVector_OnPassingSwappedKeyRemap)
        {
            RemapBuffer remapBuffer;

            // Remap A to B and B to A
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x41, (DWORD)0x42 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x42, (DWORD)0x41 }), std::wstring() });

            // Assert that there are no orphaned keys
            Assert::AreEqual(true, LoadingAndSavingRemappingHelper::GetOrphanedKeys(remapBuffer).empty());
        }

        // Test if the GetOrphanedKeys method return one orphaned on passing two key remaps where one key is mapped to a remapped key
        TEST_METHOD (GetOrphanedKeys_ShouldReturnOneOrphanedKey_OnPassingTwoKeyRemapsWhereOneKeyIsMappedToARemappedKey)
        {
            RemapBuffer remapBuffer;

            // Remap A to Ctrl+B and C to A
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x42);
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x41, dest1 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x43, (DWORD)0x41 }), std::wstring() });

            // Assert that only C is orphaned
            Assert::AreEqual((size_t)1, LoadingAndSavingRemappingHelper::GetOrphanedKeys(remapBuffer).size());
            Assert::AreEqual((DWORD)0x43, LoadingAndSavingRemappingHelper::GetOrphanedKeys(remapBuffer)[0]);
        }

        // Test if the PreProcessRemapTable method combines all the modifier pairs when the left and right modifiers are remapped to the same target
        TEST_METHOD (PreProcessRemapTable_ShouldCombineAllPairs_OnPassingLeftAndRightModifiersRemappedToTheSameTarget)
        {
            SingleKeyRemapTable remapTable;

            // Remap LCtrl and RCtrl to A, LAlt and RAlt to B, LShift and RShift to C, LWin and RWin to D
            remapTable[VK_LCONTROL] = (DWORD)0x41;
            remapTable[VK_RCONTROL] = (DWORD)0x41;
            remapTable[VK_LMENU] = (DWORD)0x42;
            remapTable[VK_RMENU] = (DWORD)0x42;
            remapTable[VK_LSHIFT] = (DWORD)0x43;
            remapTable[VK_RSHIFT] = (DWORD)0x43;
            remapTable[VK_LWIN] = (DWORD)0x44;
            remapTable[VK_RWIN] = (DWORD)0x44;

            // Pre process table
            LoadingAndSavingRemappingHelper::PreProcessRemapTable(remapTable);

            // Expected Ctrl remapped to A, Alt to B, Shift to C, Win to D
            SingleKeyRemapTable expectedTable;
            expectedTable[VK_CONTROL] = (DWORD)0x41;
            expectedTable[VK_MENU] = (DWORD)0x42;
            expectedTable[VK_SHIFT] = (DWORD)0x43;
            expectedTable[CommonSharedConstants::VK_WIN_BOTH] = (DWORD)0x44;

            bool areTablesEqual = (expectedTable == remapTable);
            Assert::AreEqual(true, areTablesEqual);
        }

        // Test if the PreProcessRemapTable method does not combines any of the modifier pairs when the left and right modifiers are remapped to different targets
        TEST_METHOD (PreProcessRemapTable_ShouldNotCombineAnyPairs_OnPassingLeftAndRightModifiersRemappedToTheDifferentTargets)
        {
            SingleKeyRemapTable remapTable;

            // Remap left modifiers to A and right modifiers to B
            remapTable[VK_LCONTROL] = (DWORD)0x41;
            remapTable[VK_RCONTROL] = (DWORD)0x42;
            remapTable[VK_LMENU] = (DWORD)0x41;
            remapTable[VK_RMENU] = (DWORD)0x42;
            remapTable[VK_LSHIFT] = (DWORD)0x41;
            remapTable[VK_RSHIFT] = (DWORD)0x42;
            remapTable[VK_LWIN] = (DWORD)0x41;
            remapTable[VK_RWIN] = (DWORD)0x42;

            // Pre process table
            LoadingAndSavingRemappingHelper::PreProcessRemapTable(remapTable);

            // Expected unchanged table
            SingleKeyRemapTable expectedTable;
            expectedTable[VK_LCONTROL] = (DWORD)0x41;
            expectedTable[VK_RCONTROL] = (DWORD)0x42;
            expectedTable[VK_LMENU] = (DWORD)0x41;
            expectedTable[VK_RMENU] = (DWORD)0x42;
            expectedTable[VK_LSHIFT] = (DWORD)0x41;
            expectedTable[VK_RSHIFT] = (DWORD)0x42;
            expectedTable[VK_LWIN] = (DWORD)0x41;
            expectedTable[VK_RWIN] = (DWORD)0x42;

            bool areTablesEqual = (expectedTable == remapTable);
            Assert::AreEqual(true, areTablesEqual);
        }

        // Test if the ApplySingleKeyRemappings method resets the keyboard manager state's single key remappings on passing an empty buffer
        TEST_METHOD (ApplySingleKeyRemappings_ShouldResetSingleKeyRemappings_OnPassingEmptyBuffer)
        {
            MappingConfiguration testShortcuts;
            RemapBuffer remapBuffer;

            // Remap A to B
            testShortcuts.AddSingleKeyRemap(0x41, (DWORD)0x42);

            // Apply the single key remaps from the buffer to the keyboard manager state variable
            LoadingAndSavingRemappingHelper::ApplySingleKeyRemappings(testShortcuts, remapBuffer, false);

            // Assert that single key remapping in the kbm state variable is empty
            Assert::AreEqual((size_t)0, testShortcuts.singleKeyReMap.size());
        }

        // Test if the ApplySingleKeyRemappings method copies only the valid remappings to the keyboard manager state variable when some of the remappings are invalid
        TEST_METHOD (ApplySingleKeyRemappings_ShouldCopyOnlyValidRemappings_OnPassingBufferWithSomeInvalidRemappings)
        {
            MappingConfiguration testShortcuts;
            RemapBuffer remapBuffer;

            // Add A->B, B->Ctrl+V, C to incomplete shortcut and D to incomplete key remappings to the buffer
            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(0x56);
            Shortcut s2;
            s2.SetKey(VK_LMENU);
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x41, (DWORD)0x42 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x42, s1 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x43, (DWORD)0 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)0x44, s2 }), std::wstring() });

            // Apply the single key remaps from the buffer to the keyboard manager state variable
            LoadingAndSavingRemappingHelper::ApplySingleKeyRemappings(testShortcuts, remapBuffer, false);

            // Expected A remapped to B, B remapped to Ctrl+V
            SingleKeyRemapTable expectedTable;
            expectedTable[0x41] = (DWORD)0x42;
            expectedTable[0x42] = s1;

            bool areTablesEqual = (expectedTable == testShortcuts.singleKeyReMap);
            Assert::AreEqual(true, areTablesEqual);
        }

        // Test if the ApplySingleKeyRemappings method splits common modifiers to their left and right version when copying to the keyboard manager state variable if remappings from common modifiers are passed
        TEST_METHOD (ApplySingleKeyRemappings_ShouldSplitRemappingsFromCommonModifiers_OnPassingBufferWithSomeMappingsFromCommonModifiers)
        {
            MappingConfiguration testShortcuts;
            RemapBuffer remapBuffer;

            // Add Ctrl->A, Alt->B, Shift->C and Win->D remappings to the buffer
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)VK_CONTROL, (DWORD)0x41 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)VK_MENU, (DWORD)0x42 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)VK_SHIFT, (DWORD)0x43 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ (DWORD)CommonSharedConstants::VK_WIN_BOTH, (DWORD)0x44 }), std::wstring() });

            // Apply the single key remaps from the buffer to the keyboard manager state variable
            LoadingAndSavingRemappingHelper::ApplySingleKeyRemappings(testShortcuts, remapBuffer, false);

            // Expected LCtrl/RCtrl remapped to A, LAlt/RAlt to B, LShift/RShift to C, LWin/RWin to D
            SingleKeyRemapTable expectedTable;
            expectedTable[VK_LCONTROL] = (DWORD)0x41;
            expectedTable[VK_RCONTROL] = (DWORD)0x41;
            expectedTable[VK_LMENU] = (DWORD)0x42;
            expectedTable[VK_RMENU] = (DWORD)0x42;
            expectedTable[VK_LSHIFT] = (DWORD)0x43;
            expectedTable[VK_RSHIFT] = (DWORD)0x43;
            expectedTable[VK_LWIN] = (DWORD)0x44;
            expectedTable[VK_RWIN] = (DWORD)0x44;

            bool areTablesEqual = (expectedTable == testShortcuts.singleKeyReMap);
            Assert::AreEqual(true, areTablesEqual);
        }

        // Test that a single-key remap tagged condition="alone" is loaded into the alone table while
        // an untagged (legacy / "always") remap goes to the regular table. This locks the dual-key
        // (tap-alone) round-trip contract: SaveSettingsToFile writes the "condition" field for alone
        // remaps, and LoadSingleKeyRemaps must route by it. Builds the JSON exactly as the save path
        // emits it so it exercises the real on-disk shape without touching disk.
        TEST_METHOD (LoadSingleKeyRemaps_ShouldRouteAloneToAloneTable_AndAlwaysToRegularTable)
        {
            MappingConfiguration config;

            // Alone entry: A -> B, tagged condition="alone" (as SaveSettingsToFile writes it)
            json::JsonObject aloneEntry;
            aloneEntry.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(winrt::to_hstring(static_cast<unsigned int>(0x41))));
            aloneEntry.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(winrt::to_hstring(static_cast<unsigned int>(0x42))));
            aloneEntry.SetNamedValue(KeyboardManagerConstants::RemapConditionSettingName, json::value(KeyboardManagerConstants::RemapConditionAlone));

            // Always entry: C -> D, no condition field (legacy shape; loader defaults to "always")
            json::JsonObject alwaysEntry;
            alwaysEntry.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(winrt::to_hstring(static_cast<unsigned int>(0x43))));
            alwaysEntry.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(winrt::to_hstring(static_cast<unsigned int>(0x44))));

            json::JsonArray inProcess;
            inProcess.Append(aloneEntry);
            inProcess.Append(alwaysEntry);

            json::JsonObject remapKeys;
            remapKeys.SetNamedValue(KeyboardManagerConstants::InProcessRemapKeysSettingName, inProcess);

            json::JsonObject root;
            root.SetNamedValue(KeyboardManagerConstants::RemapKeysSettingName, remapKeys);

            config.LoadSingleKeyRemaps(root);

            // Alone entry routed to the alone table (A -> B) and NOT the regular table
            Assert::AreEqual(static_cast<size_t>(1), config.aloneSingleKeyReMap.size());
            Assert::IsTrue(config.aloneSingleKeyReMap.find(0x41) != config.aloneSingleKeyReMap.end());
            Assert::AreEqual(static_cast<DWORD>(0x42), std::get<DWORD>(config.aloneSingleKeyReMap[0x41]));
            Assert::IsTrue(config.singleKeyReMap.find(0x41) == config.singleKeyReMap.end());

            // Always entry routed to the regular table (C -> D) and NOT the alone table
            Assert::AreEqual(static_cast<size_t>(1), config.singleKeyReMap.size());
            Assert::IsTrue(config.singleKeyReMap.find(0x43) != config.singleKeyReMap.end());
            Assert::AreEqual(static_cast<DWORD>(0x44), std::get<DWORD>(config.singleKeyReMap[0x43]));
            Assert::IsTrue(config.aloneSingleKeyReMap.find(0x43) == config.aloneSingleKeyReMap.end());
        }

        TEST_METHOD (LoadSettingsFromJson_ShouldSkipSingleKeyRemapWithUnknownCondition)
        {
            auto profile = CreateEmptyMappingProfile();
            auto remaps = profile.GetNamedObject(KeyboardManagerConstants::RemapKeysSettingName)
                              .GetNamedArray(KeyboardManagerConstants::InProcessRemapKeysSettingName);

            json::JsonObject invalidRemap;
            invalidRemap.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(L"65"));
            invalidRemap.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(L"66"));
            invalidRemap.SetNamedValue(KeyboardManagerConstants::RemapConditionSettingName, json::value(L"unknown"));
            remaps.Append(invalidRemap);

            json::JsonObject validRemap;
            validRemap.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(L"67"));
            validRemap.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(L"68"));
            remaps.Append(validRemap);

            MappingConfiguration configuration;
            const auto result = configuration.LoadSettingsFromJson(profile);

            Assert::AreEqual(static_cast<int>(MappingConfigurationLoadResult::Partial), static_cast<int>(result));
            Assert::AreEqual(static_cast<size_t>(1), configuration.singleKeyReMap.size());
            Assert::IsFalse(configuration.singleKeyReMap.contains(L'A'));
            Assert::IsTrue(configuration.singleKeyReMap.contains(L'C'));
            Assert::AreEqual(static_cast<DWORD>(L'D'), std::get<DWORD>(configuration.singleKeyReMap.at(L'C')));
            Assert::IsTrue(configuration.aloneSingleKeyReMap.empty());
        }

        TEST_METHOD (LoadSettingsFromJson_ShouldReportPartialAndKeepFirstDuplicatePerCondition)
        {
            auto profile = CreateEmptyMappingProfile();
            auto remaps = profile.GetNamedObject(KeyboardManagerConstants::RemapKeysSettingName)
                              .GetNamedArray(KeyboardManagerConstants::InProcessRemapKeysSettingName);

            for (const auto targetKey : { L'B', L'C' })
            {
                json::JsonObject remap;
                remap.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(L"65"));
                remap.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(std::to_wstring(targetKey)));
                remap.SetNamedValue(KeyboardManagerConstants::RemapConditionSettingName, json::value(KeyboardManagerConstants::RemapConditionAlways));
                remaps.Append(remap);
            }

            for (const auto targetKey : { L'E', L'F' })
            {
                json::JsonObject remap;
                remap.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(L"68"));
                remap.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(std::to_wstring(targetKey)));
                remap.SetNamedValue(KeyboardManagerConstants::RemapConditionSettingName, json::value(KeyboardManagerConstants::RemapConditionAlone));
                remaps.Append(remap);
            }

            MappingConfiguration configuration;
            const auto result = configuration.LoadSettingsFromJson(profile);

            Assert::AreEqual(static_cast<int>(MappingConfigurationLoadResult::Partial), static_cast<int>(result));
            Assert::AreEqual(static_cast<size_t>(1), configuration.singleKeyReMap.size());
            Assert::AreEqual(static_cast<DWORD>(L'B'), std::get<DWORD>(configuration.singleKeyReMap.at(L'A')));
            Assert::AreEqual(static_cast<size_t>(1), configuration.aloneSingleKeyReMap.size());
            Assert::AreEqual(static_cast<DWORD>(L'E'), std::get<DWORD>(configuration.aloneSingleKeyReMap.at(L'D')));
        }

        // Test if the ApplyShortcutRemappings method resets the keyboard manager state's os level and app specific shortcut remappings on passing an empty buffer
        TEST_METHOD (ApplyShortcutRemappings_ShouldResetShortcutRemappings_OnPassingEmptyBuffer)
        {
            MappingConfiguration testShortcuts;
            RemapBuffer remapBuffer;

            // Remap Ctrl+A to Ctrl+B for all apps and Ctrl+C to Alt+V for testApp1
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x41);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x42);
            Shortcut src2;
            src2.SetKey(VK_CONTROL);
            src2.SetKey(0x43);
            Shortcut dest2;
            dest2.SetKey(VK_MENU);
            dest2.SetKey(0x56);
            testShortcuts.AddOSLevelShortcut(src1, dest1);
            testShortcuts.AddAppSpecificShortcut(testApp1, src1, dest1);

            // Apply the shortcut remaps from the buffer to the keyboard manager state variable
            LoadingAndSavingRemappingHelper::ApplyShortcutRemappings(testShortcuts, remapBuffer, false);

            // Assert that shortcut remappings in the kbm state variable is empty
            Assert::AreEqual((size_t)0, testShortcuts.osLevelShortcutReMap.size());
            Assert::AreEqual((size_t)0, testShortcuts.appSpecificShortcutReMap.size());
        }

        // Test if the ApplyShortcutRemappings method copies only the valid remappings to the keyboard manager state variable when some of the remappings are invalid
        TEST_METHOD (ApplyShortcutRemappings_ShouldCopyOnlyValidRemappings_OnPassingBufferWithSomeInvalidRemappings)
        {
            MappingConfiguration testShortcuts;
            RemapBuffer remapBuffer;

            // Add Ctrl+A->Ctrl+B, Ctrl+C->Alt+V, Ctrl+F->incomplete shortcut and Ctrl+G->incomplete key os level remappings to buffer
            // Add Ctrl+F->Alt+V, Ctrl+G->Ctrl+B, Ctrl+A->incomplete shortcut and  Ctrl+C->incomplete key app specific remappings to buffer
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x41);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x42);
            Shortcut src2;
            src2.SetKey(VK_CONTROL);
            src2.SetKey(0x43);
            Shortcut dest2;
            dest2.SetKey(VK_MENU);
            dest2.SetKey(0x56);
            Shortcut src3;
            src3.SetKey(VK_CONTROL);
            src3.SetKey(0x46);
            Shortcut src4;
            src4.SetKey(VK_CONTROL);
            src4.SetKey(0x47);
            Shortcut dest4;
            dest4.SetKey(VK_CONTROL);
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src1, dest1 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src2, dest2 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src3, (DWORD)0 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src4, dest4 }), std::wstring() });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src3, dest2 }), testApp1 });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src4, dest1 }), testApp1 });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src1, (DWORD)0 }), testApp1 });
            remapBuffer.push_back(RemapBufferRow{ RemapBufferItem({ src2, dest4 }), testApp1 });

            // Apply the shortcut remaps from the buffer to the keyboard manager state variable
            LoadingAndSavingRemappingHelper::ApplyShortcutRemappings(testShortcuts, remapBuffer, false);

            // Ctrl+A->Ctrl+B and Ctrl+C->Alt+V
            ShortcutRemapTable expectedOSLevelTable;
            expectedOSLevelTable[src1] = RemapShortcut(dest1);
            expectedOSLevelTable[src2] = RemapShortcut(dest2);

            // Ctrl+F->Alt+V and Ctrl+G->Ctrl+B for testApp1
            AppSpecificShortcutRemapTable expectedAppSpecificLevelTable;
            expectedAppSpecificLevelTable[testApp1][src3] = RemapShortcut(dest2);
            expectedAppSpecificLevelTable[testApp1][src4] = RemapShortcut(dest1);

            bool areOSLevelTablesEqual = (expectedOSLevelTable == testShortcuts.osLevelShortcutReMap);
            bool areAppSpecificTablesEqual = (expectedAppSpecificLevelTable == testShortcuts.appSpecificShortcutReMap);
            Assert::AreEqual(true, areOSLevelTablesEqual);
            Assert::AreEqual(true, areAppSpecificTablesEqual);
        }

        TEST_METHOD (SaveSettingsToFileWithResult_ShouldKeepCommitSuccess_WhenReloadNotificationFails)
        {
            bool writerCalled = false;
            bool notifierCalled = false;
            MappingConfiguration configuration{
                [&writerCalled](const std::wstring&, const json::JsonObject&) {
                    writerCalled = true;
                    return true;
                },
                [&notifierCalled]() {
                    notifierCalled = true;
                    return false;
                },
                [](const std::wstring&) {
                    return L"test-settings.json";
                }
            };

            const auto result = configuration.SaveSettingsToFileWithResult();

            Assert::IsTrue(result.settingsCommitted);
            Assert::IsFalse(result.reloadNotified);
            Assert::IsTrue(writerCalled);
            Assert::IsTrue(notifierCalled);
            Assert::IsTrue(configuration.SaveSettingsToFile());
        }

        TEST_METHOD (SaveSettingsToFileWithResult_ShouldNotNotify_WhenAtomicCommitFails)
        {
            bool notifierCalled = false;
            MappingConfiguration configuration{
                [](const std::wstring&, const json::JsonObject&) {
                    return false;
                },
                [&notifierCalled]() {
                    notifierCalled = true;
                    return true;
                },
                [](const std::wstring&) {
                    return L"test-settings.json";
                }
            };

            const auto result = configuration.SaveSettingsToFileWithResult();

            Assert::IsFalse(result.settingsCommitted);
            Assert::IsFalse(result.reloadNotified);
            Assert::IsFalse(notifierCalled);
            Assert::IsFalse(configuration.SaveSettingsToFile());
        }

        TEST_METHOD (LoadSettingsFromFile_ShouldTreatMissingActiveProfileAsEmptySuccess)
        {
            ScopedProfileTestPath missingProfile{ L"missing.json" };
            MappingConfiguration configuration;
            configuration.currentConfig = L"previous";
            Assert::IsTrue(configuration.AddSingleKeyRemap(L'A', static_cast<DWORD>(L'B')));
            Assert::IsTrue(configuration.AddSingleKeyAloneRemap(L'C', static_cast<DWORD>(L'D')));
            Assert::IsTrue(configuration.AddTextExpansion(CreateTextExpansionRule(TextExpansionId1)));

            const auto result = configuration.LoadSettingsFromFile(L"new-profile", missingProfile.path.wstring());

            Assert::AreEqual(static_cast<int>(MappingConfigurationLoadResult::Success), static_cast<int>(result));
            Assert::AreEqual(std::wstring(L"new-profile"), configuration.currentConfig);
            Assert::IsTrue(configuration.singleKeyReMap.empty());
            Assert::IsTrue(configuration.aloneSingleKeyReMap.empty());
            Assert::IsTrue(configuration.textExpansions.empty());
        }

        TEST_METHOD (LoadSettingsFromFile_ShouldLeaveStateUnchangedWhenExistingProfileCannotBeParsed)
        {
            ScopedProfileTestPath corruptProfile{ L"corrupt.json" };
            {
                std::ofstream file{ corruptProfile.path, std::ios::binary | std::ios::trunc };
                Assert::IsTrue(file.good());
                file << "not valid json";
            }

            MappingConfiguration configuration;
            configuration.currentConfig = L"previous";
            Assert::IsTrue(configuration.AddTextExpansion(CreateTextExpansionRule(TextExpansionId1)));

            const auto result = configuration.LoadSettingsFromFile(L"corrupt-profile", corruptProfile.path.wstring());

            Assert::AreEqual(static_cast<int>(MappingConfigurationLoadResult::Failure), static_cast<int>(result));
            Assert::AreEqual(std::wstring(L"previous"), configuration.currentConfig);
            Assert::AreEqual(static_cast<size_t>(1), configuration.textExpansions.size());
            Assert::AreEqual(std::wstring(TextExpansionId1), configuration.textExpansions[0].id);
        }

        TEST_METHOD (LoadSettingsFromFile_ShouldApplyValidSubsetAndUpdateConfigurationOnPartial)
        {
            ScopedProfileTestPath partialProfile{ L"partial.json" };
            auto profile = CreateEmptyMappingProfile();
            json::JsonObject keyRemap;
            keyRemap.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(L"65"));
            keyRemap.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(L"66"));
            profile.GetNamedObject(KeyboardManagerConstants::RemapKeysSettingName)
                .GetNamedArray(KeyboardManagerConstants::InProcessRemapKeysSettingName)
                .Append(keyRemap);
            json::JsonObject aloneKeyRemap;
            aloneKeyRemap.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(L"69"));
            aloneKeyRemap.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(L"70"));
            aloneKeyRemap.SetNamedValue(KeyboardManagerConstants::RemapConditionSettingName, json::value(KeyboardManagerConstants::RemapConditionAlone));
            profile.GetNamedObject(KeyboardManagerConstants::RemapKeysSettingName)
                .GetNamedArray(KeyboardManagerConstants::InProcessRemapKeysSettingName)
                .Append(aloneKeyRemap);
            auto rules = GetTextExpansionArray(profile);
            rules.Append(CreateTextExpansionJson(
                TextExpansionId1,
                L"valid",
                CreateActivationJson({ VK_SPACE }),
                L"loaded",
                true));
            auto invalidRule = CreateTextExpansionJson(
                TextExpansionId2,
                L"invalid",
                CreateActivationJson({ VK_SPACE }),
                L"skipped",
                true);
            invalidRule.Remove(KeyboardManagerConstants::TextExpansionReplacementTextSettingName);
            rules.Append(invalidRule);
            json::to_file(partialProfile.path.wstring(), profile);

            MappingConfiguration configuration;
            configuration.currentConfig = L"previous";
            Assert::IsTrue(configuration.AddSingleKeyRemap(L'C', static_cast<DWORD>(L'D')));
            Assert::IsTrue(configuration.AddSingleKeyAloneRemap(L'G', static_cast<DWORD>(L'H')));
            Assert::IsTrue(configuration.AddTextExpansion(CreateTextExpansionRule(TextExpansionId3)));

            const auto result = configuration.LoadSettingsFromFile(L"partial-profile", partialProfile.path.wstring());

            Assert::AreEqual(static_cast<int>(MappingConfigurationLoadResult::Partial), static_cast<int>(result));
            Assert::AreEqual(std::wstring(L"partial-profile"), configuration.currentConfig);
            Assert::AreEqual(static_cast<size_t>(1), configuration.singleKeyReMap.size());
            Assert::IsTrue(configuration.singleKeyReMap.contains(L'A'));
            Assert::AreEqual(static_cast<DWORD>(L'B'), std::get<DWORD>(configuration.singleKeyReMap.at(L'A')));
            Assert::AreEqual(static_cast<size_t>(1), configuration.aloneSingleKeyReMap.size());
            Assert::IsTrue(configuration.aloneSingleKeyReMap.contains(L'E'));
            Assert::AreEqual(static_cast<DWORD>(L'F'), std::get<DWORD>(configuration.aloneSingleKeyReMap.at(L'E')));
            Assert::AreEqual(static_cast<size_t>(1), configuration.textExpansions.size());
            Assert::AreEqual(std::wstring(TextExpansionId1), configuration.textExpansions[0].id);
            Assert::AreEqual(std::wstring(L"loaded"), configuration.textExpansions[0].replacementText);
        }

        TEST_METHOD (LoadSettingsFromJson_ShouldLoadValidAppSpecificShortcutsWhenGlobalShortcutIsInvalid)
        {
            auto profile = CreateEmptyMappingProfile();
            auto shortcuts = profile.GetNamedObject(KeyboardManagerConstants::RemapShortcutsSettingName);
            shortcuts.GetNamedArray(KeyboardManagerConstants::GlobalRemapShortcutsSettingName)
                .Append(json::value(L"invalid"));

            json::JsonObject appSpecificShortcut;
            appSpecificShortcut.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(L"17;65"));
            appSpecificShortcut.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(L"66"));
            appSpecificShortcut.SetNamedValue(KeyboardManagerConstants::TargetAppSettingName, json::value(L"test.exe"));
            shortcuts.GetNamedArray(KeyboardManagerConstants::AppSpecificRemapShortcutsSettingName)
                .Append(appSpecificShortcut);

            MappingConfiguration configuration;
            const auto result = configuration.LoadSettingsFromJson(profile);

            Assert::AreEqual(static_cast<int>(MappingConfigurationLoadResult::Partial), static_cast<int>(result));
            Assert::AreEqual(static_cast<size_t>(1), configuration.appSpecificShortcutReMap.size());
            Assert::IsTrue(configuration.appSpecificShortcutReMap.contains(L"test.exe"));
            const auto& appMappings = configuration.appSpecificShortcutReMap.at(L"test.exe");
            Assert::AreEqual(static_cast<size_t>(1), appMappings.size());
            Assert::IsTrue(appMappings.contains(Shortcut(L"17;65")));
        }

        TEST_METHOD (LoadTextExpansions_ShouldAcceptCanonicalSchema_NormalizeActivationAndPreserveDuplicates)
        {
            auto profile = CreateEmptyMappingProfile();
            auto rules = GetTextExpansionArray(profile);
            rules.Append(CreateTextExpansionJson(
                TextExpansionId1,
                L"brb",
                CreateActivationJson({ VK_SPACE }),
                L"first",
                true));
            rules.Append(CreateTextExpansionJson(
                TextExpansionId2,
                L"brb",
                CreateActivationJson({ L'A', VK_SHIFT, VK_CONTROL }),
                L"second",
                false));
            rules.Append(CreateTextExpansionJson(
                TextExpansionId3,
                L"brb",
                CreateActivationJson({ VK_SPACE }),
                L"first",
                true));

            MappingConfiguration configuration;
            const auto result = configuration.LoadSettingsFromJson(profile);

            Assert::AreEqual(static_cast<int>(MappingConfigurationLoadResult::Success), static_cast<int>(result));
            Assert::AreEqual(static_cast<size_t>(3), configuration.textExpansions.size());
            Assert::AreEqual(std::wstring(TextExpansionId1), configuration.textExpansions[0].id);
            Assert::AreEqual(std::wstring(TextExpansionId2), configuration.textExpansions[1].id);
            Assert::AreEqual(std::wstring(TextExpansionId3), configuration.textExpansions[2].id);
            Assert::AreEqual(std::wstring(L"brb"), configuration.textExpansions[0].sourceText);
            Assert::AreEqual(std::wstring(L"first"), configuration.textExpansions[0].replacementText);
            Assert::IsTrue(configuration.textExpansions[0].enabled);
            Assert::IsFalse(configuration.textExpansions[1].enabled);
            Assert::IsTrue(configuration.textExpansions[1].activation.GetKeyCodes() == std::vector<DWORD>{ VK_CONTROL, VK_SHIFT, L'A' });
        }

        TEST_METHOD (LoadTextExpansions_ShouldTreatMissingSectionAsAnEmptySuccessfulSnapshot)
        {
            MappingConfiguration configuration;
            Assert::IsTrue(configuration.AddTextExpansion(CreateTextExpansionRule(TextExpansionId1)));

            const auto result = configuration.LoadSettingsFromJson(CreateEmptyMappingProfile(false));

            Assert::AreEqual(static_cast<int>(MappingConfigurationLoadResult::Success), static_cast<int>(result));
            Assert::IsTrue(configuration.textExpansions.empty());
        }

        TEST_METHOD (LoadTextExpansions_ShouldSkipInvalidOrDuplicateGuidsAndApplyValidSubset)
        {
            const auto existingRule = CreateTextExpansionRule(TextExpansionId3, L"existing", Shortcut(VK_TAB), L"unchanged", false);
            MappingConfiguration configuration;
            Assert::IsTrue(configuration.AddTextExpansion(existingRule));

            for (const auto& invalidId : {
                     std::wstring(L"not-a-guid"),
                     std::wstring(L"11111111-1111-4111-8111-11111111111A"),
                     std::wstring(L"{11111111-1111-4111-8111-111111111111}") })
            {
                auto profile = CreateEmptyMappingProfile();
                auto rules = GetTextExpansionArray(profile);
                rules.Append(CreateTextExpansionJson(
                    TextExpansionId2,
                    L"valid",
                    CreateActivationJson({ VK_TAB }),
                    L"loaded",
                    true));
                rules.Append(CreateTextExpansionJson(
                    invalidId,
                    L"brb",
                    CreateActivationJson({ VK_SPACE }),
                    L"replacement",
                    true));

                const auto result = configuration.LoadSettingsFromJson(profile);
                Assert::AreEqual(static_cast<int>(MappingConfigurationLoadResult::Partial), static_cast<int>(result));
                Assert::AreEqual(static_cast<size_t>(1), configuration.textExpansions.size());
                Assert::AreEqual(std::wstring(TextExpansionId2), configuration.textExpansions[0].id);
                Assert::AreEqual(std::wstring(L"loaded"), configuration.textExpansions[0].replacementText);
            }

            auto duplicateProfile = CreateEmptyMappingProfile();
            auto duplicateRules = GetTextExpansionArray(duplicateProfile);
            duplicateRules.Append(CreateTextExpansionJson(
                TextExpansionId1,
                L"first",
                CreateActivationJson({ VK_SPACE }),
                L"one",
                true));
            duplicateRules.Append(CreateTextExpansionJson(
                TextExpansionId1,
                L"second",
                CreateActivationJson({ VK_CONTROL, VK_SPACE }),
                L"two",
                false));

            const auto duplicateResult = configuration.LoadSettingsFromJson(duplicateProfile);
            Assert::AreEqual(static_cast<int>(MappingConfigurationLoadResult::Partial), static_cast<int>(duplicateResult));
            Assert::AreEqual(static_cast<size_t>(1), configuration.textExpansions.size());
            Assert::AreEqual(std::wstring(TextExpansionId1), configuration.textExpansions[0].id);
            Assert::AreEqual(std::wstring(L"first"), configuration.textExpansions[0].sourceText);
        }

        TEST_METHOD (LoadTextExpansions_ShouldSkipEntriesWithMissingRequiredFieldsAndApplyValidSubset)
        {
            MappingConfiguration configuration;
            Assert::IsTrue(configuration.AddTextExpansion(CreateTextExpansionRule(TextExpansionId3)));

            for (const auto& missingField : {
                     KeyboardManagerConstants::TextExpansionIdSettingName,
                     KeyboardManagerConstants::TextExpansionSourceTextSettingName,
                     KeyboardManagerConstants::TextExpansionActivationKeysSettingName,
                     KeyboardManagerConstants::TextExpansionReplacementTextSettingName,
                     KeyboardManagerConstants::TextExpansionEnabledSettingName })
            {
                auto profile = CreateEmptyMappingProfile();
                auto rules = GetTextExpansionArray(profile);
                rules.Append(CreateTextExpansionJson(
                    TextExpansionId2,
                    L"valid",
                    CreateActivationJson({ VK_TAB }),
                    L"loaded",
                    true));
                auto rule = CreateTextExpansionJson(
                    TextExpansionId1,
                    L"brb",
                    CreateActivationJson({ VK_SPACE }),
                    L"replacement",
                    true);
                rule.Remove(missingField);
                rules.Append(rule);

                const auto result = configuration.LoadSettingsFromJson(profile);
                Assert::AreEqual(static_cast<int>(MappingConfigurationLoadResult::Partial), static_cast<int>(result));
                Assert::AreEqual(static_cast<size_t>(1), configuration.textExpansions.size());
                Assert::AreEqual(std::wstring(TextExpansionId2), configuration.textExpansions[0].id);
                Assert::AreEqual(std::wstring(L"loaded"), configuration.textExpansions[0].replacementText);
            }
        }

        TEST_METHOD (LoadTextExpansions_ShouldSkipInvalidActivationShapesAndApplyValidSubset)
        {
            MappingConfiguration configuration;
            Assert::IsTrue(configuration.AddTextExpansion(CreateTextExpansionRule(TextExpansionId3)));

            std::vector<json::JsonArray> invalidActivations{
                CreateActivationJson({}),
                CreateActivationJson({ VK_CONTROL }),
                CreateActivationJson({ L'A', L'B' }),
                CreateActivationJson({ VK_CONTROL, VK_CONTROL, L'A' }),
                CreateActivationJson({ VK_CONTROL, VK_LCONTROL, L'A' }),
                CreateActivationJson({ 0 }),
                CreateActivationJson({ 0x100 }),
                CreateActivationJson({ 65.5 }),
            };

            for (const auto& activation : invalidActivations)
            {
                auto profile = CreateEmptyMappingProfile();
                auto rules = GetTextExpansionArray(profile);
                rules.Append(CreateTextExpansionJson(
                    TextExpansionId2,
                    L"valid",
                    CreateActivationJson({ VK_TAB }),
                    L"loaded",
                    true));
                rules.Append(CreateTextExpansionJson(
                    TextExpansionId1,
                    L"brb",
                    activation,
                    L"replacement",
                    true));

                const auto result = configuration.LoadSettingsFromJson(profile);
                Assert::AreEqual(static_cast<int>(MappingConfigurationLoadResult::Partial), static_cast<int>(result));
                Assert::AreEqual(static_cast<size_t>(1), configuration.textExpansions.size());
                Assert::AreEqual(std::wstring(TextExpansionId2), configuration.textExpansions[0].id);
                Assert::AreEqual(std::wstring(L"loaded"), configuration.textExpansions[0].replacementText);
            }

            auto nonArrayProfile = CreateEmptyMappingProfile();
            auto nonArrayRules = GetTextExpansionArray(nonArrayProfile);
            nonArrayRules.Append(CreateTextExpansionJson(
                TextExpansionId2,
                L"valid",
                CreateActivationJson({ VK_TAB }),
                L"loaded",
                true));
            auto nonArrayRule = CreateTextExpansionJson(
                TextExpansionId1,
                L"brb",
                CreateActivationJson({ VK_SPACE }),
                L"replacement",
                true);
            nonArrayRule.SetNamedValue(KeyboardManagerConstants::TextExpansionActivationKeysSettingName, json::value(L"32"));
            nonArrayRules.Append(nonArrayRule);

            const auto nonArrayResult = configuration.LoadSettingsFromJson(nonArrayProfile);
            Assert::AreEqual(static_cast<int>(MappingConfigurationLoadResult::Partial), static_cast<int>(nonArrayResult));
            Assert::AreEqual(static_cast<size_t>(1), configuration.textExpansions.size());
            Assert::AreEqual(std::wstring(TextExpansionId2), configuration.textExpansions[0].id);
            Assert::AreEqual(std::wstring(L"loaded"), configuration.textExpansions[0].replacementText);
        }

        TEST_METHOD (TextExpansionCrud_ShouldUseGuidPreserveOrderAndAllowDuplicateSourceAndActivation)
        {
            MappingConfiguration configuration;
            const auto duplicateContent1 = CreateTextExpansionRule(TextExpansionId1);
            const auto duplicateContent2 = CreateTextExpansionRule(TextExpansionId2);
            const auto thirdRule = CreateTextExpansionRule(
                TextExpansionId3,
                L"sig",
                Shortcut(L'X'),
                L"signature",
                false);

            Assert::IsTrue(configuration.AddTextExpansion(duplicateContent1));
            Assert::IsTrue(configuration.AddTextExpansion(duplicateContent2));
            Assert::IsTrue(configuration.AddTextExpansion(thirdRule));
            Assert::IsFalse(configuration.AddTextExpansion(duplicateContent1));
            Assert::IsFalse(configuration.AddTextExpansion(CreateTextExpansionRule(
                L"AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA")));
            Assert::AreEqual(static_cast<size_t>(3), configuration.textExpansions.size());

            Assert::IsTrue(configuration.UpdateTextExpansion(
                TextExpansionId2,
                L"updated",
                CreateActivation({ VK_CONTROL, VK_SHIFT, L'U' }),
                L"updated replacement",
                false));
            Assert::AreEqual(std::wstring(TextExpansionId1), configuration.textExpansions[0].id);
            Assert::AreEqual(std::wstring(TextExpansionId2), configuration.textExpansions[1].id);
            Assert::AreEqual(std::wstring(TextExpansionId3), configuration.textExpansions[2].id);
            Assert::AreEqual(std::wstring(L"updated"), configuration.textExpansions[1].sourceText);
            Assert::AreEqual(std::wstring(L"updated replacement"), configuration.textExpansions[1].replacementText);
            Assert::IsFalse(configuration.textExpansions[1].enabled);

            Assert::IsTrue(configuration.SetTextExpansionEnabled(TextExpansionId1, false));
            Assert::IsFalse(configuration.textExpansions[0].enabled);
            Assert::IsFalse(configuration.SetTextExpansionEnabled(L"44444444-4444-4444-8444-444444444444", true));

            Assert::IsFalse(configuration.UpdateTextExpansion(
                TextExpansionId2,
                L"",
                Shortcut(VK_SPACE),
                L"invalid",
                true));
            Assert::AreEqual(std::wstring(L"updated"), configuration.textExpansions[1].sourceText);
            Assert::IsFalse(configuration.textExpansions[1].enabled);

            Assert::IsTrue(configuration.DeleteTextExpansion(TextExpansionId2));
            Assert::IsFalse(configuration.DeleteTextExpansion(TextExpansionId2));
            Assert::AreEqual(static_cast<size_t>(2), configuration.textExpansions.size());
            Assert::AreEqual(std::wstring(TextExpansionId1), configuration.textExpansions[0].id);
            Assert::AreEqual(std::wstring(TextExpansionId3), configuration.textExpansions[1].id);
        }

        TEST_METHOD (SaveTextExpansions_ShouldWriteOnlyCanonicalFieldsAndNormalizedNumericActivation)
        {
            json::JsonObject savedProfile;
            bool writerCalled = false;
            MappingConfiguration configuration{
                [&savedProfile, &writerCalled](const std::wstring&, const json::JsonObject& profile) {
                    savedProfile = profile;
                    writerCalled = true;
                    return true;
                },
                []() {
                    return true;
                },
                [](const std::wstring&) {
                    return L"test-settings.json";
                }
            };
            Assert::IsTrue(configuration.AddTextExpansion(CreateTextExpansionRule(
                TextExpansionId1,
                L"brb",
                CreateActivation({ L'A', VK_SHIFT, VK_CONTROL }),
                L"be right back",
                false)));

            const auto result = configuration.SaveSettingsToFileWithResult();

            Assert::IsTrue(result.settingsCommitted);
            Assert::IsTrue(result.reloadNotified);
            Assert::IsTrue(writerCalled);

            const auto rules = GetTextExpansionArray(savedProfile);
            Assert::AreEqual(static_cast<uint32_t>(1), rules.Size());
            const auto rule = rules.GetObjectAt(0);
            Assert::AreEqual(std::wstring(TextExpansionId1), std::wstring(rule.GetNamedString(KeyboardManagerConstants::TextExpansionIdSettingName)));
            Assert::AreEqual(std::wstring(L"brb"), std::wstring(rule.GetNamedString(KeyboardManagerConstants::TextExpansionSourceTextSettingName)));
            Assert::AreEqual(std::wstring(L"be right back"), std::wstring(rule.GetNamedString(KeyboardManagerConstants::TextExpansionReplacementTextSettingName)));
            Assert::IsFalse(rule.GetNamedBoolean(KeyboardManagerConstants::TextExpansionEnabledSettingName));
            Assert::IsFalse(rule.HasKey(L"trigger"));
            Assert::IsFalse(rule.HasKey(L"triggerKey"));
            Assert::IsFalse(rule.HasKey(L"unicodeText"));

            const auto activation = rule.GetNamedArray(KeyboardManagerConstants::TextExpansionActivationKeysSettingName);
            Assert::AreEqual(static_cast<uint32_t>(3), activation.Size());
            Assert::AreEqual(static_cast<int>(VK_CONTROL), static_cast<int>(activation.GetNumberAt(0)));
            Assert::AreEqual(static_cast<int>(VK_SHIFT), static_cast<int>(activation.GetNumberAt(1)));
            Assert::AreEqual(static_cast<int>(L'A'), static_cast<int>(activation.GetNumberAt(2)));
        }
    };
}
