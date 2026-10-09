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

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

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

        // Test that a key-sequence remap round-trips: KeySequenceToString writes the target exactly as
        // SaveSettingsToFile stores it (in the separate inProcessSequence array), and
        // LoadSingleKeyRemaps loads it into the sequence table in the same order -- not into the
        // regular table as a shortcut, even though both use the ';'-separated format.
        TEST_METHOD (LoadSingleKeyRemaps_ShouldRouteSequenceToSequenceTable_InOrder)
        {
            const std::vector<DWORD> sequence{ VK_ESCAPE, VK_IME_OFF, 0x41 };
            const std::wstring stored = MappingConfiguration::KeySequenceToString(sequence);
            Assert::AreEqual(std::wstring(L"27;26;65"), stored);

            json::JsonObject sequenceEntry;
            sequenceEntry.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(winrt::to_hstring(static_cast<unsigned int>(VK_ESCAPE))));
            sequenceEntry.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(stored));

            json::JsonArray inProcessSequence;
            inProcessSequence.Append(sequenceEntry);

            json::JsonObject remapKeys;
            remapKeys.SetNamedValue(KeyboardManagerConstants::InProcessRemapKeysSettingName, json::JsonArray());
            remapKeys.SetNamedValue(KeyboardManagerConstants::InProcessSequenceRemapKeysSettingName, inProcessSequence);

            json::JsonObject root;
            root.SetNamedValue(KeyboardManagerConstants::RemapKeysSettingName, remapKeys);

            MappingConfiguration config;
            Assert::IsTrue(config.LoadSingleKeyRemaps(root));

            Assert::AreEqual(static_cast<size_t>(1), config.singleKeyToSequenceReMap.size());
            Assert::IsTrue(config.singleKeyToSequenceReMap[VK_ESCAPE] == sequence);
            Assert::IsTrue(config.singleKeyReMap.empty());
            Assert::IsTrue(config.aloneSingleKeyReMap.empty());
        }

        // Test the key-sequence string parser: empty elements are skipped, and a non-numeric element
        // throws (LoadSingleKeyRemaps catches it and skips that one remap).
        TEST_METHOD (ParseKeySequence_ShouldSkipEmptyElements_AndThrowOnInvalid)
        {
            Assert::IsTrue(MappingConfiguration::ParseKeySequence(L"27;;26;") == std::vector<DWORD>{ 27, 26 });
            Assert::IsTrue(MappingConfiguration::ParseKeySequence(L"").empty());

            Assert::IsTrue(MappingConfiguration::ParseKeySequence(L"256;260;255") == std::vector<DWORD>{ CommonSharedConstants::VK_DISABLED, CommonSharedConstants::VK_WIN_BOTH, 255 });

            for (const wchar_t* invalid : { L"27;abc", L"0x1A", L"27abc", L"0", L"300", L"-1", L"+27", L" 27" })
            {
                bool threw = false;
                try
                {
                    MappingConfiguration::ParseKeySequence(invalid);
                }
                catch (...)
                {
                    threw = true;
                }
                Assert::IsTrue(threw, invalid);
            }
        }

        // Test that loading again replaces the key sequences instead of keeping the old ones (the
        // duplicate check would otherwise reject the new value), and that a ';' value in the regular
        // inProcess array is still a shortcut, not a sequence. Also: a file without the
        // inProcessSequence array (every settings file written before this feature) loads cleanly.
        TEST_METHOD (LoadSingleKeyRemaps_ReloadReplacesSequences_AndUntaggedStaysShortcut)
        {
            auto makeRoot = [](const std::wstring& sequenceValue) {
                json::JsonObject sequenceEntry;
                sequenceEntry.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(winrt::to_hstring(static_cast<unsigned int>(VK_ESCAPE))));
                sequenceEntry.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(sequenceValue));

                json::JsonObject shortcutEntry;
                shortcutEntry.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(winrt::to_hstring(static_cast<unsigned int>(0x41))));
                shortcutEntry.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(L"162;67"));

                json::JsonArray inProcess;
                inProcess.Append(shortcutEntry);
                json::JsonArray inProcessSequence;
                inProcessSequence.Append(sequenceEntry);

                json::JsonObject remapKeys;
                remapKeys.SetNamedValue(KeyboardManagerConstants::InProcessRemapKeysSettingName, inProcess);
                remapKeys.SetNamedValue(KeyboardManagerConstants::InProcessSequenceRemapKeysSettingName, inProcessSequence);

                json::JsonObject root;
                root.SetNamedValue(KeyboardManagerConstants::RemapKeysSettingName, remapKeys);
                return root;
            };

            MappingConfiguration config;
            config.LoadSingleKeyRemaps(makeRoot(L"27;26"));
            config.LoadSingleKeyRemaps(makeRoot(L"26;27"));

            Assert::AreEqual(static_cast<size_t>(1), config.singleKeyToSequenceReMap.size());
            Assert::IsTrue(config.singleKeyToSequenceReMap[VK_ESCAPE] == std::vector<DWORD>{ 26, 27 });

            Assert::AreEqual(static_cast<size_t>(1), config.singleKeyReMap.size());
            Assert::AreEqual(static_cast<size_t>(1), config.singleKeyReMap[0x41].index());
            Assert::IsTrue(config.singleKeyToSequenceReMap.find(0x41) == config.singleKeyToSequenceReMap.end());

            // A file written before this feature has no inProcessSequence array: it loads without error
            // and clears the sequences loaded before.
            json::JsonArray legacyInProcess;
            json::JsonObject legacyRemapKeys;
            legacyRemapKeys.SetNamedValue(KeyboardManagerConstants::InProcessRemapKeysSettingName, legacyInProcess);
            json::JsonObject legacyRoot;
            legacyRoot.SetNamedValue(KeyboardManagerConstants::RemapKeysSettingName, legacyRemapKeys);
            Assert::IsTrue(config.LoadSingleKeyRemaps(legacyRoot));
            Assert::IsTrue(config.singleKeyToSequenceReMap.empty());
        }

        // Key sequences are hand-written for now, so a bad entry is skipped without failing the load
        // (a failed load stops the editor from opening the profile). Only the valid entry is loaded.
        TEST_METHOD (LoadSingleKeyRemaps_SkipsBadSequenceEntries_WithoutFailingLoad)
        {
            auto entry = [](const wchar_t* original, const wchar_t* sequence) {
                json::JsonObject e;
                e.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(original));
                e.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(sequence));
                return e;
            };

            json::JsonArray inProcessSequence;
            inProcessSequence.Append(entry(L"27", L"27;26"));   // valid
            inProcessSequence.Append(entry(L"27", L"26"));      // duplicate source key
            inProcessSequence.Append(entry(L"112", L""));       // empty sequence
            inProcessSequence.Append(entry(L"113", L"27;0x1A")); // not decimal
            inProcessSequence.Append(entry(L"0x72", L"65"));    // source key not decimal
            inProcessSequence.Append(entry(L"114abc", L"65"));  // trailing characters
            inProcessSequence.Append(entry(L"0", L"65"));       // source key 0
            inProcessSequence.Append(entry(L"-27", L"65"));     // sign
            inProcessSequence.Append(entry(L" 115", L"65"));    // leading space

            json::JsonObject remapKeys;
            remapKeys.SetNamedValue(KeyboardManagerConstants::InProcessRemapKeysSettingName, json::JsonArray());
            remapKeys.SetNamedValue(KeyboardManagerConstants::InProcessSequenceRemapKeysSettingName, inProcessSequence);
            json::JsonObject root;
            root.SetNamedValue(KeyboardManagerConstants::RemapKeysSettingName, remapKeys);

            MappingConfiguration config;
            Assert::IsTrue(config.LoadSingleKeyRemaps(root));
            Assert::AreEqual(static_cast<size_t>(1), config.singleKeyToSequenceReMap.size());
            Assert::IsTrue(config.singleKeyToSequenceReMap[27] == std::vector<DWORD>{ 27, 26 });
        }

        // The key-sequence array is read even when remapKeys has no inProcess array.
        TEST_METHOD (LoadSingleKeyRemaps_ReadsSequences_WhenInProcessIsMissing)
        {
            json::JsonObject e;
            e.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(L"27"));
            e.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(L"27;26"));
            json::JsonArray inProcessSequence;
            inProcessSequence.Append(e);

            json::JsonObject remapKeys;
            remapKeys.SetNamedValue(KeyboardManagerConstants::InProcessSequenceRemapKeysSettingName, inProcessSequence);
            json::JsonObject root;
            root.SetNamedValue(KeyboardManagerConstants::RemapKeysSettingName, remapKeys);

            MappingConfiguration config;
            config.LoadSingleKeyRemaps(root);
            Assert::AreEqual(static_cast<size_t>(1), config.singleKeyToSequenceReMap.size());
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
    };
}
