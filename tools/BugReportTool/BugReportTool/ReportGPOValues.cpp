#include "ReportGPOValues.h"
#include <common/utils/gpo.h>
#include <fstream>
#include <unordered_map>
#include <string>
#include <regex>

std::wstring gpo_rule_configured_to_string(powertoys_gpo::gpo_rule_configured_t gpo_rule)
{
    switch (gpo_rule)
    {
    case powertoys_gpo::gpo_rule_configured_wrong_value:
        return L"wrong_value";
    case powertoys_gpo::gpo_rule_configured_unavailable:
        return L"can't_access";
    case powertoys_gpo::gpo_rule_configured_not_configured:
        return L"not_configured";
    case powertoys_gpo::gpo_rule_configured_disabled:
        return L"disabled";
    case powertoys_gpo::gpo_rule_configured_enabled:
        return L"enabled";
    default:
        return L"Unrecognized gpo_rule_configured_t value.";
    }
}

std::wstring gpo_string_to_string(const std::wstring &gpo_value)
{
    if (gpo_value == L"")
    {
        return L"not_configured";
    }
    else
    {
        return std::regex_replace(gpo_value, std::wregex(L"\r\n"), std::wstring(L"|"));
    }
}

void ReportGPOValues(const std::filesystem::path &tmpDir)
{
    auto reportPath = tmpDir;
    reportPath.append(L"gpo-configuration-info.txt");
    std::wofstream report(reportPath);
    report << "GPO policies configuration" << std::endl;
    report << "getConfiguredAdvancedPasteEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredAdvancedPasteEnabledValue()) << std::endl;
    report << "getConfiguredAlwaysOnTopEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredAlwaysOnTopEnabledValue()) << std::endl;
    report << "getConfiguredAwakeEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredAwakeEnabledValue()) << std::endl;
    report << "getConfiguredCmdPalEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredCmdPalEnabledValue()) << std::endl;
    report << "getConfiguredColorPickerEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredColorPickerEnabledValue()) << std::endl;
    report << "getConfiguredCropAndLockEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredCropAndLockEnabledValue()) << std::endl;
    report << "getConfiguredFancyZonesEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredFancyZonesEnabledValue()) << std::endl;
    report << "getConfiguredFileLocksmithEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredFileLocksmithEnabledValue()) << std::endl;
    report << "getConfiguredLightSwitchEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredLightSwitchEnabledValue()) << std::endl;
    report << "getConfiguredSvgPreviewEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredSvgPreviewEnabledValue()) << std::endl;
    report << "getConfiguredMarkdownPreviewEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMarkdownPreviewEnabledValue()) << std::endl;
    report << "getConfiguredMonacoPreviewEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMonacoPreviewEnabledValue()) << std::endl;
    report << "getConfiguredPdfPreviewEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredPdfPreviewEnabledValue()) << std::endl;
    report << "getConfiguredGcodePreviewEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredGcodePreviewEnabledValue()) << std::endl;
    report << "getConfiguredBgcodePreviewEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredBgcodePreviewEnabledValue()) << std::endl;
    report << "getConfiguredSvgThumbnailsEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredSvgThumbnailsEnabledValue()) << std::endl;
    report << "getConfiguredPdfThumbnailsEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredPdfThumbnailsEnabledValue()) << std::endl;
    report << "getConfiguredGcodeThumbnailsEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredGcodeThumbnailsEnabledValue()) << std::endl;
    report << "getConfiguredBgcodeThumbnailsEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredBgcodeThumbnailsEnabledValue()) << std::endl;
    report << "getConfiguredStlThumbnailsEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredStlThumbnailsEnabledValue()) << std::endl;
    report << "getConfiguredHostsFileEditorEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredHostsFileEditorEnabledValue()) << std::endl;
    report << "getConfiguredImageResizerEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredImageResizerEnabledValue()) << std::endl;
    report << "getConfiguredKeyboardManagerEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredKeyboardManagerEnabledValue()) << std::endl;
    report << "getConfiguredFindMyMouseEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredFindMyMouseEnabledValue()) << std::endl;
    report << "getConfiguredMouseHighlighterEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMouseHighlighterEnabledValue()) << std::endl;
    report << "getConfiguredMouseJumpEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMouseJumpEnabledValue()) << std::endl;
    report << "getConfiguredMousePointerCrosshairsEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMousePointerCrosshairsEnabledValue()) << std::endl;
    report << "getConfiguredMouseWithoutBordersEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMouseWithoutBordersEnabledValue()) << std::endl;
    report << "getConfiguredPowerRenameEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredPowerRenameEnabledValue()) << std::endl;
    report << "getConfiguredPowerLauncherEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredPowerLauncherEnabledValue()) << std::endl;
    report << "getConfiguredWorkspacesEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredWorkspacesEnabledValue()) << std::endl;
    report << "getConfiguredQuickAccentEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredQuickAccentEnabledValue()) << std::endl;
    report << "getConfiguredScreenRulerEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredScreenRulerEnabledValue()) << std::endl;
    report << "getConfiguredShortcutGuideEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredShortcutGuideEnabledValue()) << std::endl;
    report << "getConfiguredTextExtractorEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredTextExtractorEnabledValue()) << std::endl;
    report << "getConfiguredPeekEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredPeekEnabledValue()) << std::endl;
    report << "getConfiguredZoomItEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredZoomItEnabledValue()) << std::endl;
    report << "getDisableAutomaticUpdateDownloadValue: " << gpo_rule_configured_to_string(powertoys_gpo::getDisableAutomaticUpdateDownloadValue()) << std::endl;
    report << "getSuspendNewUpdateToastValue: " << gpo_rule_configured_to_string(powertoys_gpo::getSuspendNewUpdateToastValue()) << std::endl;
    report << "getDisableNewUpdateToastValue: " << gpo_rule_configured_to_string(powertoys_gpo::getDisableNewUpdateToastValue()) << std::endl;
    report << "getDisableShowWhatsNewAfterUpdatesValue: " << gpo_rule_configured_to_string(powertoys_gpo::getDisableShowWhatsNewAfterUpdatesValue()) << std::endl;
    report << "getAllowExperimentationValue: " << gpo_rule_configured_to_string(powertoys_gpo::getAllowExperimentationValue()) << std::endl;
    report << "getConfiguredQoiPreviewEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredQoiPreviewEnabledValue()) << std::endl;
    report << "getConfiguredQoiThumbnailsEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredQoiThumbnailsEnabledValue()) << std::endl;
    report << "getAllowedAdvancedPasteOnlineAIModelsValue: " << gpo_rule_configured_to_string(powertoys_gpo::getAllowedAdvancedPasteOnlineAIModelsValue()) << std::endl;
    report << "getConfiguredMwbClipboardSharingEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMwbClipboardSharingEnabledValue()) << std::endl;
    report << "getConfiguredMwbFileTransferEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMwbFileTransferEnabledValue()) << std::endl;
    report << "getConfiguredMwbUseOriginalUserInterfaceValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMwbUseOriginalUserInterfaceValue()) << std::endl;
    report << "getConfiguredMwbDisallowBlockingScreensaverValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMwbDisallowBlockingScreensaverValue()) << std::endl;
    report << "getConfiguredMwbAllowServiceModeValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMwbAllowServiceModeValue()) << std::endl;
    report << "getConfiguredMwbSameSubnetOnlyValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMwbSameSubnetOnlyValue()) << std::endl;
    report << "getConfiguredMwbValidateRemoteIpValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMwbValidateRemoteIpValue()) << std::endl;
    report << "getConfiguredMwbDisableUserDefinedIpMappingRulesValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMwbDisableUserDefinedIpMappingRulesValue()) << std::endl;
    report << "getConfiguredMwbPolicyDefinedIpMappingRules: " << gpo_string_to_string(powertoys_gpo::getConfiguredMwbPolicyDefinedIpMappingRules()) << std::endl;
    report << "getConfiguredNewPlusEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredNewPlusEnabledValue()) << std::endl;
    report << "getConfiguredNewPlusHideTemplateFilenameExtensionValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredNewPlusHideTemplateFilenameExtensionValue()) << std::endl;
    report << "getAllowDataDiagnosticsValue: " << gpo_rule_configured_to_string(powertoys_gpo::getAllowDataDiagnosticsValue()) << std::endl;
    report << "getConfiguredRunAtStartupValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredRunAtStartupValue()) << std::endl;
}
