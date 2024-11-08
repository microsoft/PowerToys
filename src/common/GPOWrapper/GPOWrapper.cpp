#include "pch.h"
#include "GPOWrapper.h"
#include "GPOWrapper.g.cpp"

namespace winrt::PowerToys::GPOWrapper::implementation
{
    GpoRuleConfigured GPOWrapper::GetConfiguredAlwaysOnTopEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredAlwaysOnTopEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredAwakeEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredAwakeEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredCmdNotFoundEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredCmdNotFoundEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredColorPickerEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredColorPickerEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredCropAndLockEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredCropAndLockEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredFancyZonesEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredFancyZonesEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredFileLocksmithEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredFileLocksmithEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredSvgPreviewEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredSvgPreviewEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMarkdownPreviewEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredMarkdownPreviewEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMonacoPreviewEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredMonacoPreviewEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredPdfPreviewEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredPdfPreviewEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredGcodePreviewEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredGcodePreviewEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredSvgThumbnailsEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredSvgThumbnailsEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredPdfThumbnailsEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredPdfThumbnailsEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredGcodeThumbnailsEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredGcodeThumbnailsEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredStlThumbnailsEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredStlThumbnailsEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredHostsFileEditorEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredHostsFileEditorEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredImageResizerEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredImageResizerEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredKeyboardManagerEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredKeyboardManagerEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredFindMyMouseEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredFindMyMouseEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMouseHighlighterEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredMouseHighlighterEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMouseJumpEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredMouseJumpEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMousePointerCrosshairsEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredMousePointerCrosshairsEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredPowerRenameEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredPowerRenameEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredPowerLauncherEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredPowerLauncherEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredQuickAccentEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredQuickAccentEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredRegistryPreviewEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredRegistryPreviewEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredScreenRulerEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredScreenRulerEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredShortcutGuideEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredShortcutGuideEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredTextExtractorEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredTextExtractorEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredAdvancedPasteEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredAdvancedPasteEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredVideoConferenceMuteEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredVideoConferenceMuteEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredZoomItEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredZoomItEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMouseWithoutBordersEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredMouseWithoutBordersEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredPeekEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredPeekEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetDisableNewUpdateToastValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getDisableNewUpdateToastValue());
    }
    GpoRuleConfigured GPOWrapper::GetDisableAutomaticUpdateDownloadValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getDisableAutomaticUpdateDownloadValue());
    }
    GpoRuleConfigured GPOWrapper::GetDisableShowWhatsNewAfterUpdatesValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getDisableShowWhatsNewAfterUpdatesValue());
    }
    GpoRuleConfigured GPOWrapper::GetAllowExperimentationValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getAllowExperimentationValue());
    }
    GpoRuleConfigured GPOWrapper::GetRunPluginEnabledValue(winrt::hstring const& pluginID)
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getRunPluginEnabledValue(winrt::to_string(pluginID)));
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredEnvironmentVariablesEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredEnvironmentVariablesEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredQoiPreviewEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredQoiPreviewEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredQoiThumbnailsEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredQoiThumbnailsEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetAllowedAdvancedPasteOnlineAIModelsValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getAllowedAdvancedPasteOnlineAIModelsValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredNewPlusEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredNewPlusEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredWorkspacesEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredWorkspacesEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMwbClipboardSharingEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredMwbClipboardSharingEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMwbFileTransferEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredMwbFileTransferEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMwbUseOriginalUserInterfaceValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredMwbUseOriginalUserInterfaceValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMwbDisallowBlockingScreensaverValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredMwbDisallowBlockingScreensaverValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMwbSameSubnetOnlyValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredMwbSameSubnetOnlyValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMwbValidateRemoteIpValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredMwbValidateRemoteIpValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMwbDisableUserDefinedIpMappingRulesValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredMwbDisableUserDefinedIpMappingRulesValue());
    }
    winrt::hstring GPOWrapper::GetConfiguredMwbPolicyDefinedIpMappingRules()
    {
        // Assuming powertoys_gpo::getConfiguredMwbPolicyDefinedIpMappingRules() returns a std::wstring
        std::wstring rules = powertoys_gpo::getConfiguredMwbPolicyDefinedIpMappingRules();

        // Convert std::wstring to winrt::hstring
        return to_hstring(rules.c_str());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredNewPlusHideTemplateFilenameExtensionValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredNewPlusHideTemplateFilenameExtensionValue());
    }
    GpoRuleConfigured GPOWrapper::GetAllowDataDiagnosticsValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getAllowDataDiagnosticsValue());
    }
}
