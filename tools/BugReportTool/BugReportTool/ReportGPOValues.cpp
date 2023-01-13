#include "ReportGPOValues.h"
#include <common/utils/gpo.h>
#include <fstream>
#include <unordered_map>
#include <string>

std::wstring gpo_rule_configured_to_string(powertoys_gpo::gpo_rule_configured_t gpo_rule)
{
    switch (gpo_rule) {
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

void ReportGPOValues(const std::filesystem::path& tmpDir)
{
    auto reportPath = tmpDir;
    reportPath.append(L"gpo-configuration-info.txt");
    std::wofstream report(reportPath);
    report << "GPO policies configuration" << std::endl;
    report << "getConfiguredAlwaysOnTopEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredAlwaysOnTopEnabledValue()) << std::endl;
    report << "getConfiguredAwakeEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredAwakeEnabledValue()) << std::endl;
    report << "getConfiguredColorPickerEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredColorPickerEnabledValue()) << std::endl;
    report << "getConfiguredFancyZonesEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredFancyZonesEnabledValue()) << std::endl;
    report << "getConfiguredFileLocksmithEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredFileLocksmithEnabledValue()) << std::endl;
    report << "getConfiguredSvgPreviewEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredSvgPreviewEnabledValue()) << std::endl;
    report << "getConfiguredMarkdownPreviewEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMarkdownPreviewEnabledValue()) << std::endl;
    report << "getConfiguredMonacoPreviewEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMonacoPreviewEnabledValue()) << std::endl;
    report << "getConfiguredPdfPreviewEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredPdfPreviewEnabledValue()) << std::endl;
    report << "getConfiguredGcodePreviewEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredGcodePreviewEnabledValue()) << std::endl;
    report << "getConfiguredSvgThumbnailsEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredSvgThumbnailsEnabledValue()) << std::endl;
    report << "getConfiguredPdfThumbnailsEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredPdfThumbnailsEnabledValue()) << std::endl;
    report << "getConfiguredGcodeThumbnailsEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredGcodeThumbnailsEnabledValue()) << std::endl;
    report << "getConfiguredStlThumbnailsEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredStlThumbnailsEnabledValue()) << std::endl;
    report << "getConfiguredHostsFileEditorEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredHostsFileEditorEnabledValue()) << std::endl;
    report << "getConfiguredImageResizerEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredImageResizerEnabledValue()) << std::endl;
    report << "getConfiguredKeyboardManagerEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredKeyboardManagerEnabledValue()) << std::endl;
    report << "getConfiguredFindMyMouseEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredFindMyMouseEnabledValue()) << std::endl;
    report << "getConfiguredMouseHighlighterEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMouseHighlighterEnabledValue()) << std::endl;
    report << "getConfiguredMousePointerCrosshairsEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredMousePointerCrosshairsEnabledValue()) << std::endl;
    report << "getConfiguredPowerRenameEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredPowerRenameEnabledValue()) << std::endl;
    report << "getConfiguredPowerLauncherEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredPowerLauncherEnabledValue()) << std::endl;
    report << "getConfiguredQuickAccentEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredQuickAccentEnabledValue()) << std::endl;
    report << "getConfiguredScreenRulerEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredScreenRulerEnabledValue()) << std::endl;
    report << "getConfiguredShortcutGuideEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredShortcutGuideEnabledValue()) << std::endl;
    report << "getConfiguredTextExtractorEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredTextExtractorEnabledValue()) << std::endl;
    report << "getConfiguredVideoConferenceMuteEnabledValue: " << gpo_rule_configured_to_string(powertoys_gpo::getConfiguredVideoConferenceMuteEnabledValue()) << std::endl;

}
