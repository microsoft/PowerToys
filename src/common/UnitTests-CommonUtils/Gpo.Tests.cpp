#include "pch.h"
#include "TestHelpers.h"
#include <gpo.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace powertoys_gpo;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(GpoTests)
    {
    public:
        // Helper to check if result is a valid gpo_rule_configured_t value
        static constexpr bool IsValidGpoResult(gpo_rule_configured_t result)
        {
            return result == gpo_rule_configured_wrong_value ||
                   result == gpo_rule_configured_unavailable ||
                   result == gpo_rule_configured_not_configured ||
                   result == gpo_rule_configured_disabled ||
                   result == gpo_rule_configured_enabled;
        }

        // gpo_rule_configured_t enum tests
        TEST_METHOD(GpoRuleConfigured_EnumValues_AreDistinct)
        {
            Assert::AreNotEqual(static_cast<int>(gpo_rule_configured_not_configured),
                               static_cast<int>(gpo_rule_configured_enabled));
            Assert::AreNotEqual(static_cast<int>(gpo_rule_configured_enabled),
                               static_cast<int>(gpo_rule_configured_disabled));
            Assert::AreNotEqual(static_cast<int>(gpo_rule_configured_not_configured),
                               static_cast<int>(gpo_rule_configured_disabled));
        }

        // getConfiguredValue tests
        TEST_METHOD(GetConfiguredValue_NonExistentKey_ReturnsNotConfigured)
        {
            auto result = getConfiguredValue(L"NonExistentPolicyValue12345");
            Assert::IsTrue(result == gpo_rule_configured_not_configured ||
                          result == gpo_rule_configured_unavailable);
        }

        // Utility enabled getters - these all follow the same pattern
        TEST_METHOD(GetAllowExperimentationValue_ReturnsValidState)
        {
            auto result = getAllowExperimentationValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredAlwaysOnTopEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredAlwaysOnTopEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredAwakeEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredAwakeEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredColorPickerEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredColorPickerEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredFancyZonesEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredFancyZonesEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredFileLocksmithEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredFileLocksmithEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredImageResizerEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredImageResizerEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredKeyboardManagerEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredKeyboardManagerEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredPowerRenameEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredPowerRenameEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredPowerLauncherEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredPowerLauncherEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredShortcutGuideEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredShortcutGuideEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredTextExtractorEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredTextExtractorEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredHostsFileEditorEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredHostsFileEditorEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredMousePointerCrosshairsEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredMousePointerCrosshairsEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredMouseHighlighterEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredMouseHighlighterEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredMouseJumpEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredMouseJumpEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredFindMyMouseEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredFindMyMouseEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredMouseWithoutBordersEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredMouseWithoutBordersEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredAdvancedPasteEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredAdvancedPasteEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredPeekEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredPeekEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredRegistryPreviewEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredRegistryPreviewEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredScreenRulerEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredScreenRulerEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredCropAndLockEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredCropAndLockEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        TEST_METHOD(GetConfiguredEnvironmentVariablesEnabledValue_ReturnsValidState)
        {
            auto result = getConfiguredEnvironmentVariablesEnabledValue();
            Assert::IsTrue(IsValidGpoResult(result));
        }

        // All GPO functions should not crash
        TEST_METHOD(AllGpoFunctions_DoNotCrash)
        {
            getAllowExperimentationValue();
            getConfiguredAlwaysOnTopEnabledValue();
            getConfiguredAwakeEnabledValue();
            getConfiguredColorPickerEnabledValue();
            getConfiguredFancyZonesEnabledValue();
            getConfiguredFileLocksmithEnabledValue();
            getConfiguredImageResizerEnabledValue();
            getConfiguredKeyboardManagerEnabledValue();
            getConfiguredPowerRenameEnabledValue();
            getConfiguredPowerLauncherEnabledValue();
            getConfiguredShortcutGuideEnabledValue();
            getConfiguredTextExtractorEnabledValue();
            getConfiguredHostsFileEditorEnabledValue();
            getConfiguredMousePointerCrosshairsEnabledValue();
            getConfiguredMouseHighlighterEnabledValue();
            getConfiguredMouseJumpEnabledValue();
            getConfiguredFindMyMouseEnabledValue();
            getConfiguredMouseWithoutBordersEnabledValue();
            getConfiguredAdvancedPasteEnabledValue();
            getConfiguredPeekEnabledValue();
            getConfiguredRegistryPreviewEnabledValue();
            getConfiguredScreenRulerEnabledValue();
            getConfiguredCropAndLockEnabledValue();
            getConfiguredEnvironmentVariablesEnabledValue();

            Assert::IsTrue(true);
        }
    };
}
