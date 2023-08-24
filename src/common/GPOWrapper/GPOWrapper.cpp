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
    GpoRuleConfigured GPOWrapper::GetConfiguredPastePlainEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredPastePlainEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredVideoConferenceMuteEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredVideoConferenceMuteEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMouseWithoutBordersEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredMouseWithoutBordersEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredPeekEnabledValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getConfiguredPeekEnabledValue());
    }
    GpoRuleConfigured GPOWrapper::GetDisableAutomaticUpdateDownloadValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getDisableAutomaticUpdateDownloadValue());
    }
    GpoRuleConfigured GPOWrapper::GetAllowExperimentationValue()
    {
        return static_cast<GpoRuleConfigured>(powertoys_gpo::getAllowExperimentationValue());
    }
}
