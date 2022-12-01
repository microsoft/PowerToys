#include "pch.h"
#include "GPOWrapper.h"
#include "GPOWrapper.g.cpp"

namespace winrt::PowerToys::GPOWrapper::implementation
{
    GpoRuleConfigured GPOWrapper::GetConfiguredAlwaysOnTopEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredAlwaysOnTopEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredAwakeEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredAwakeEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredColorPickerEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredColorPickerEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredFancyZonesEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredFancyZonesEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredFileLocksmithEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredFileLocksmithEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredSvgPreviewEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredSvgPreviewEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMarkdownPreviewEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredMarkdownPreviewEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMonacoPreviewEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredMonacoPreviewEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredPdfPreviewEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredPdfPreviewEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredGcodePreviewEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredGcodePreviewEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredSvgThumbnailsEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredSvgThumbnailsEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredPdfThumbnailsEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredPdfThumbnailsEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredGcodeThumbnailsEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredGcodeThumbnailsEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredStlThumbnailsEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredStlThumbnailsEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredHostsFileEditorEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredHostsFileEditorEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredImageResizerEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredImageResizerEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredKeyboardManagerEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredKeyboardManagerEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredFindMyMouseEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredFindMyMouseEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMouseHighlighterEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredMouseHighlighterEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredMousePointerCrosshairsEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredMousePointerCrosshairsEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredPowerRenameEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredPowerRenameEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredPowerLauncherEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredPowerLauncherEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredQuickAccentEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredQuickAccentEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredScreenRulerEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredScreenRulerEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredShortcutGuideEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredShortcutGuideEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredTextExtractorEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredTextExtractorEnabledValue();
    }
    GpoRuleConfigured GPOWrapper::GetConfiguredVideoConferenceMuteEnabledValue()
    {
        return (GpoRuleConfigured)powertoys_gpo::getConfiguredVideoConferenceMuteEnabledValue();
    }
}
