#include "pch.h"
#include "CppUnitTest.h"
#include <keyboardmanager/common/Shortcut.h>
#include <keyboardmanager/common/Helpers.h>
#include "TestHelpers.h"
#include <common/interop/keyboard_layout.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace KeyboardManagerCommonTests
{
    // Tests for methods in the Shortcut class
    TEST_CLASS (KeyboardManagerHelperTests)
    {
    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
        }

        // Test if the IsValidShortcut method returns false on passing shortcut with null action key
        TEST_METHOD (IsValidShortcut_ShouldReturnFalse_OnPassingShortcutWithNullActionKey)
        {
            // Arrange
            Shortcut s;
            s.SetKey(NULL);

            // Act
            bool result = s.IsValidShortcut();

            // Assert
            Assert::IsFalse(result);
        }

        // Test if the IsValidShortcut method returns false on passing shortcut with only action key
        TEST_METHOD (IsValidShortcut_ShouldReturnFalse_OnPassingShortcutWithOnlyActionKey)
        {
            // Arrange
            Shortcut s;
            s.SetKey(0x41);

            // Act
            bool result = s.IsValidShortcut();

            // Assert
            Assert::IsFalse(result);
        }

        // Test if the IsValidShortcut method returns false on passing shortcut with only modifier keys
        TEST_METHOD (IsValidShortcut_ShouldReturnFalse_OnPassingShortcutWithOnlyModifierKeys)
        {
            // Arrange
            Shortcut s;
            s.SetKey(VK_CONTROL);
            s.SetKey(VK_SHIFT);

            // Act
            bool result = s.IsValidShortcut();

            // Assert
            Assert::IsFalse(result);
        }

        // Test if the IsValidShortcut method returns true on passing shortcut with modifier and action key
        TEST_METHOD (IsValidShortcut_ShouldReturnFalse_OnPassingShortcutWithModifierAndActionKey)
        {
            // Arrange
            Shortcut s;
            s.SetKey(VK_CONTROL);
            s.SetKey(0x41);

            // Act
            bool result = s.IsValidShortcut();

            // Assert
            Assert::IsTrue(result);
        }

        // Test if the DoKeysOverlap method returns NoError on passing invalid shortcut for one of the arguments
        TEST_METHOD (DoKeysOverlap_ShouldReturnNoError_OnPassingInvalidShortcutForOneOfTheArguments)
        {
            // Arrange
            Shortcut s1(std::vector<int32_t>{ NULL });
            Shortcut s2(std::vector<int32_t>{ VK_CONTROL, 0x41 });

            // Act
            auto result = Shortcut::DoKeysOverlap(s1, s2);

            // Assert
            Assert::IsTrue(result == KeyboardManagerHelper::ErrorType::NoError);
        }

        // Test if the DoKeysOverlap method returns SameShortcutPreviouslyMapped on passing same shortcut for both arguments
        TEST_METHOD (DoKeysOverlap_ShouldReturnSameShortcutPreviouslyMapped_OnPassingSameShortcutForBothArguments)
        {
            // Arrange
            Shortcut s1(std::vector<int32_t>{ VK_CONTROL, 0x41 });
            Shortcut s2 = s1;

            // Act
            auto result = Shortcut::DoKeysOverlap(s1, s2);

            // Assert
            Assert::IsTrue(result == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);
        }

        // Test if the DoKeysOverlap method returns NoError on passing shortcuts with different action keys
        TEST_METHOD (DoKeysOverlap_ShouldReturnNoError_OnPassingShortcutsWithDifferentActionKeys)
        {
            // Arrange
            Shortcut s1(std::vector<int32_t>{ VK_CONTROL, 0x42 });
            Shortcut s2(std::vector<int32_t>{ VK_CONTROL, 0x41 });

            // Act
            auto result = Shortcut::DoKeysOverlap(s1, s2);

            // Assert
            Assert::IsTrue(result == KeyboardManagerHelper::ErrorType::NoError);
        }

        // Test if the DoKeysOverlap method returns NoError on passing shortcuts with different modifiers
        TEST_METHOD (DoKeysOverlap_ShouldReturnNoError_OnPassingShortcutsWithDifferentModifiers)
        {
            // Arrange
            Shortcut s1(std::vector<int32_t>{ VK_CONTROL, 0x42 });
            Shortcut s2(std::vector<int32_t>{ VK_SHIFT, 0x42 });

            // Act
            auto result = Shortcut::DoKeysOverlap(s1, s2);

            // Assert
            Assert::IsTrue(result == KeyboardManagerHelper::ErrorType::NoError);
        }

        // Test if the DoKeysOverlap method returns ConflictingModifierShortcut on passing shortcuts with left modifier and common modifier
        TEST_METHOD (DoKeysOverlap_ShouldReturnConflictingModifierShortcut_OnPassingShortcutsWithLeftModifierAndCommonModifierOfSameType)
        {
            // Arrange
            Shortcut s1(std::vector<int32_t>{ VK_LCONTROL, 0x42 });
            Shortcut s2(std::vector<int32_t>{ VK_CONTROL, 0x42 });

            // Act
            auto result = Shortcut::DoKeysOverlap(s1, s2);

            // Assert
            Assert::IsTrue(result == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);
        }

        // Test if the DoKeysOverlap method returns ConflictingModifierShortcut on passing shortcuts with right modifier and common modifier
        TEST_METHOD (DoKeysOverlap_ShouldReturnConflictingModifierShortcut_OnPassingShortcutsWithRightModifierAndCommonModifierOfSameType)
        {
            // Arrange
            Shortcut s1(std::vector<int32_t>{ VK_RCONTROL, 0x42 });
            Shortcut s2(std::vector<int32_t>{ VK_CONTROL, 0x42 });

            // Act
            auto result = Shortcut::DoKeysOverlap(s1, s2);

            // Assert
            Assert::IsTrue(result == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);
        }

        // Test if the DoKeysOverlap method returns ConflictingModifierShortcut on passing shortcuts with left modifier and right modifier
        TEST_METHOD (DoKeysOverlap_ShouldReturnConflictingModifierShortcut_OnPassingShortcutsWithLeftModifierAndRightModifierOfSameType)
        {
            // Arrange
            Shortcut s1(std::vector<int32_t>{ VK_LCONTROL, 0x42 });
            Shortcut s2(std::vector<int32_t>{ VK_RCONTROL, 0x42 });

            // Act
            auto result = Shortcut::DoKeysOverlap(s1, s2);

            // Assert
            Assert::IsTrue(result == KeyboardManagerHelper::ErrorType::NoError);
        }
    };
}
