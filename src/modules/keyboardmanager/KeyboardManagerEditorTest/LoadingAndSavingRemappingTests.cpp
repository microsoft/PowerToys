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

        TEST_METHOD (AddTextReplacement_ShouldStoreReplacementAndUpdateMaxTriggerLength)
        {
            MappingConfiguration testShortcuts;

            bool addedShortTrigger = testShortcuts.AddTextReplacement(L"sun", L"moon", VK_SPACE);
            bool addedLongTrigger = testShortcuts.AddTextReplacement(L"planet", L"galaxy", VK_RETURN);

            Assert::AreEqual(true, addedShortTrigger);
            Assert::AreEqual(true, addedLongTrigger);
            Assert::AreEqual(std::wstring(L"moon"), testShortcuts.textReplacements[L"sun"].text);
            Assert::AreEqual(static_cast<DWORD>(VK_SPACE), testShortcuts.textReplacements[L"sun"].triggerKey);
            Assert::AreEqual(std::wstring(L"galaxy"), testShortcuts.textReplacements[L"planet"].text);
            Assert::AreEqual(static_cast<DWORD>(VK_RETURN), testShortcuts.textReplacements[L"planet"].triggerKey);
            Assert::AreEqual(static_cast<size_t>(6), testShortcuts.maxTextReplacementTriggerLength);
        }

        TEST_METHOD (AddTextReplacement_ShouldRejectEmptyOrDuplicateReplacement)
        {
            MappingConfiguration testShortcuts;

            Assert::AreEqual(true, testShortcuts.AddTextReplacement(L"hello", L"world", VK_SPACE));
            Assert::AreEqual(false, testShortcuts.AddTextReplacement(L"hello", L"target", VK_TAB));
            Assert::AreEqual(false, testShortcuts.AddTextReplacement(L"", L"world", VK_SPACE));
            Assert::AreEqual(false, testShortcuts.AddTextReplacement(L"greeting", L"", VK_SPACE));
            Assert::AreEqual(static_cast<size_t>(1), testShortcuts.textReplacements.size());
            Assert::AreEqual(static_cast<size_t>(5), testShortcuts.maxTextReplacementTriggerLength);
        }

        TEST_METHOD (AddTextReplacement_ShouldAllowPrefixAndSuffixOverlaps)
        {
            MappingConfiguration shorterTriggerFirst;
            Assert::AreEqual(true, shorterTriggerFirst.AddTextReplacement(L"sun", L"first", VK_SPACE));
            Assert::AreEqual(true, shorterTriggerFirst.AddTextReplacement(L"sunny", L"second", VK_SPACE));

            MappingConfiguration longerTriggerFirst;
            Assert::AreEqual(true, longerTriggerFirst.AddTextReplacement(L"sunny", L"first", VK_RETURN));
            Assert::AreEqual(true, longerTriggerFirst.AddTextReplacement(L"sun", L"second", VK_TAB));

            MappingConfiguration suffixOverlap;
            Assert::AreEqual(true, suffixOverlap.AddTextReplacement(L"sun", L"first", VK_TAB));
            Assert::AreEqual(true, suffixOverlap.AddTextReplacement(L"risesun", L"second", VK_TAB));
        }

        TEST_METHOD (AddTextReplacement_ShouldRejectSpaceDelimiterPrefixConflicts)
        {
            MappingConfiguration shorterFirst;
            Assert::AreEqual(true, shorterFirst.AddTextReplacement(L"hello", L"short", VK_SPACE));
            Assert::AreEqual(false, shorterFirst.AddTextReplacement(L"hello world", L"long", VK_RETURN));

            MappingConfiguration longerFirst;
            Assert::AreEqual(true, longerFirst.AddTextReplacement(L"hello world", L"long", VK_SPACE));
            Assert::AreEqual(false, longerFirst.AddTextReplacement(L"hello", L"short", VK_SPACE));

            MappingConfiguration tabActivatedShorter;
            Assert::AreEqual(true, tabActivatedShorter.AddTextReplacement(L"hello", L"short", VK_TAB));
            Assert::AreEqual(true, tabActivatedShorter.AddTextReplacement(L"hello world", L"long", VK_SPACE));
        }

        TEST_METHOD (AddTextReplacement_ShouldAllowOnlySupportedTriggerKeys)
        {
            MappingConfiguration testShortcuts;

            Assert::AreEqual(true, testShortcuts.AddTextReplacement(L"tab", L"first", VK_TAB));
            Assert::AreEqual(true, testShortcuts.AddTextReplacement(L"enter", L"second", VK_RETURN));
            Assert::AreEqual(true, testShortcuts.AddTextReplacement(L"space", L"third", VK_SPACE));
            Assert::AreEqual(false, testShortcuts.AddTextReplacement(L"missing", L"invalid", 0));
            Assert::AreEqual(false, testShortcuts.AddTextReplacement(L"letter", L"invalid", L'A'));
            Assert::AreEqual(false, testShortcuts.AddTextReplacement(L"modifier", L"invalid", VK_CONTROL));
        }

        TEST_METHOD (AddTextReplacement_ShouldEnforceLengthLimitsAndRejectInvalidTriggerText)
        {
            MappingConfiguration atLimit;
            const std::wstring maximumTrigger(KeyboardManagerConstants::MaxTextReplacementTriggerLength, L't');
            const std::wstring maximumText(KeyboardManagerConstants::MaxTextReplacementTextLength, L'x');
            Assert::AreEqual(true, atLimit.AddTextReplacement(maximumTrigger, maximumText, VK_SPACE));
            Assert::AreEqual(KeyboardManagerConstants::MaxTextReplacementTriggerLength, atLimit.maxTextReplacementTriggerLength);

            MappingConfiguration overTriggerLimit;
            const std::wstring excessiveTrigger(KeyboardManagerConstants::MaxTextReplacementTriggerLength + 1, L't');
            Assert::AreEqual(false, overTriggerLimit.AddTextReplacement(excessiveTrigger, L"text", VK_SPACE));
            Assert::AreEqual(static_cast<size_t>(0), overTriggerLimit.textReplacements.size());
            Assert::AreEqual(static_cast<size_t>(0), overTriggerLimit.maxTextReplacementTriggerLength);

            MappingConfiguration overTextLimit;
            const std::wstring excessiveText(KeyboardManagerConstants::MaxTextReplacementTextLength + 1, L'x');
            Assert::AreEqual(false, overTextLimit.AddTextReplacement(L"trigger", excessiveText, VK_SPACE));
            Assert::AreEqual(static_cast<size_t>(0), overTextLimit.textReplacements.size());

            MappingConfiguration embeddedNull;
            Assert::AreEqual(false, embeddedNull.AddTextReplacement(std::wstring(L"tr\0igger", 8), L"text", VK_SPACE));
            Assert::AreEqual(false, embeddedNull.AddTextReplacement(L"trigger", std::wstring(L"te\0xt", 5), VK_SPACE));

            MappingConfiguration controlCharacters;
            Assert::AreEqual(false, controlCharacters.AddTextReplacement(L"line\nbreak", L"text", VK_SPACE));
            Assert::AreEqual(false, controlCharacters.AddTextReplacement(L"tab\tcharacter", L"text", VK_SPACE));
            Assert::AreEqual(false, controlCharacters.AddTextReplacement(std::wstring{ L'a', static_cast<wchar_t>(0x85), L'b' }, L"text", VK_SPACE));

            MappingConfiguration surrogatePairs;
            const std::wstring validPair{ static_cast<wchar_t>(0xD83D), static_cast<wchar_t>(0xDE00) };
            const std::wstring loneHighSurrogate{ static_cast<wchar_t>(0xD83D) };
            const std::wstring loneLowSurrogate{ static_cast<wchar_t>(0xDE00) };
            Assert::AreEqual(true, surrogatePairs.AddTextReplacement(validPair, L"emoji", VK_SPACE));
            Assert::AreEqual(false, surrogatePairs.AddTextReplacement(loneHighSurrogate, L"text", VK_SPACE));
            Assert::AreEqual(false, surrogatePairs.AddTextReplacement(loneLowSurrogate, L"text", VK_SPACE));

            MappingConfiguration multilineTarget;
            Assert::AreEqual(true, multilineTarget.AddTextReplacement(L"multiline", L"first line\nsecond line", VK_SPACE));
        }

        TEST_METHOD (UpdateAndDeleteTextReplacement_ShouldBeAtomicAndRecalculateMaximumTriggerLength)
        {
            MappingConfiguration testShortcuts;
            Assert::AreEqual(true, testShortcuts.AddTextReplacement(L"planet", L"old text", VK_SPACE));
            Assert::AreEqual(true, testShortcuts.AddTextReplacement(L"moon", L"moon text", VK_TAB));

            Assert::AreEqual(true, testShortcuts.UpdateTextReplacement(L"planet", L"star", L"new text", VK_RETURN));
            Assert::AreEqual(static_cast<size_t>(0), testShortcuts.textReplacements.count(L"planet"));
            Assert::AreEqual(std::wstring(L"new text"), testShortcuts.textReplacements.at(L"star").text);
            Assert::AreEqual(static_cast<DWORD>(VK_RETURN), testShortcuts.textReplacements.at(L"star").triggerKey);
            Assert::AreEqual(static_cast<size_t>(4), testShortcuts.maxTextReplacementTriggerLength);

            Assert::AreEqual(true, testShortcuts.UpdateTextReplacement(L"star", L"star", L"updated text", VK_TAB));
            Assert::AreEqual(std::wstring(L"updated text"), testShortcuts.textReplacements.at(L"star").text);
            Assert::AreEqual(static_cast<DWORD>(VK_TAB), testShortcuts.textReplacements.at(L"star").triggerKey);

            Assert::AreEqual(false, testShortcuts.UpdateTextReplacement(L"star", L"moon", L"conflict", VK_SPACE));
            Assert::AreEqual(false, testShortcuts.UpdateTextReplacement(L"star", L"renamed", L"conflict", L'A'));
            Assert::AreEqual(std::wstring(L"updated text"), testShortcuts.textReplacements.at(L"star").text);
            Assert::AreEqual(static_cast<DWORD>(VK_TAB), testShortcuts.textReplacements.at(L"star").triggerKey);
            Assert::AreEqual(static_cast<size_t>(0), testShortcuts.textReplacements.count(L"renamed"));

            Assert::AreEqual(true, testShortcuts.AddTextReplacement(L"forest", L"first maximum", VK_SPACE));
            Assert::AreEqual(true, testShortcuts.AddTextReplacement(L"galaxy", L"second maximum", VK_RETURN));
            Assert::AreEqual(static_cast<size_t>(6), testShortcuts.maxTextReplacementTriggerLength);
            Assert::AreEqual(true, testShortcuts.DeleteTextReplacement(L"forest"));
            Assert::AreEqual(static_cast<size_t>(6), testShortcuts.maxTextReplacementTriggerLength);
            Assert::AreEqual(true, testShortcuts.DeleteTextReplacement(L"galaxy"));
            Assert::AreEqual(static_cast<size_t>(4), testShortcuts.maxTextReplacementTriggerLength);
            Assert::AreEqual(true, testShortcuts.DeleteTextReplacement(L"star"));
            Assert::AreEqual(static_cast<size_t>(4), testShortcuts.maxTextReplacementTriggerLength);
            Assert::AreEqual(true, testShortcuts.DeleteTextReplacement(L"moon"));
            Assert::AreEqual(static_cast<size_t>(0), testShortcuts.maxTextReplacementTriggerLength);
            Assert::AreEqual(false, testShortcuts.DeleteTextReplacement(L"missing"));
        }

    };
}
