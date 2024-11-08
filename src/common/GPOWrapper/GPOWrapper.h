#pragma once
#include "GPOWrapper.g.h"
#include <common/utils/gpo.h>

namespace winrt::PowerToys::GPOWrapper::implementation
{
    struct GPOWrapper : GPOWrapperT<GPOWrapper>
    {
        GPOWrapper() = default;
        static GpoRuleConfigured GetConfiguredAlwaysOnTopEnabledValue();
        static GpoRuleConfigured GetConfiguredAwakeEnabledValue();
        static GpoRuleConfigured GetConfiguredCmdNotFoundEnabledValue();
        static GpoRuleConfigured GetConfiguredColorPickerEnabledValue();
        static GpoRuleConfigured GetConfiguredCropAndLockEnabledValue();
        static GpoRuleConfigured GetConfiguredFancyZonesEnabledValue();
        static GpoRuleConfigured GetConfiguredFileLocksmithEnabledValue();
        static GpoRuleConfigured GetConfiguredSvgPreviewEnabledValue();
        static GpoRuleConfigured GetConfiguredMarkdownPreviewEnabledValue();
        static GpoRuleConfigured GetConfiguredMonacoPreviewEnabledValue();
        static GpoRuleConfigured GetConfiguredMouseWithoutBordersEnabledValue();
        static GpoRuleConfigured GetConfiguredPdfPreviewEnabledValue();
        static GpoRuleConfigured GetConfiguredGcodePreviewEnabledValue();
        static GpoRuleConfigured GetConfiguredSvgThumbnailsEnabledValue();
        static GpoRuleConfigured GetConfiguredPdfThumbnailsEnabledValue();
        static GpoRuleConfigured GetConfiguredGcodeThumbnailsEnabledValue();
        static GpoRuleConfigured GetConfiguredStlThumbnailsEnabledValue();
        static GpoRuleConfigured GetConfiguredHostsFileEditorEnabledValue();
        static GpoRuleConfigured GetConfiguredImageResizerEnabledValue();
        static GpoRuleConfigured GetConfiguredKeyboardManagerEnabledValue();
        static GpoRuleConfigured GetConfiguredFindMyMouseEnabledValue();
        static GpoRuleConfigured GetConfiguredMouseHighlighterEnabledValue();
        static GpoRuleConfigured GetConfiguredMouseJumpEnabledValue();
        static GpoRuleConfigured GetConfiguredMousePointerCrosshairsEnabledValue();
        static GpoRuleConfigured GetConfiguredPowerRenameEnabledValue();
        static GpoRuleConfigured GetConfiguredPowerLauncherEnabledValue();
        static GpoRuleConfigured GetConfiguredQuickAccentEnabledValue();
        static GpoRuleConfigured GetConfiguredRegistryPreviewEnabledValue();
        static GpoRuleConfigured GetConfiguredScreenRulerEnabledValue();
        static GpoRuleConfigured GetConfiguredShortcutGuideEnabledValue();
        static GpoRuleConfigured GetConfiguredTextExtractorEnabledValue();
        static GpoRuleConfigured GetConfiguredAdvancedPasteEnabledValue();
        static GpoRuleConfigured GetConfiguredVideoConferenceMuteEnabledValue();
        static GpoRuleConfigured GetConfiguredZoomItEnabledValue();
        static GpoRuleConfigured GetConfiguredPeekEnabledValue();
        static GpoRuleConfigured GetDisableNewUpdateToastValue();
        static GpoRuleConfigured GetDisableAutomaticUpdateDownloadValue();
        static GpoRuleConfigured GetDisableShowWhatsNewAfterUpdatesValue();
        static GpoRuleConfigured GetAllowExperimentationValue();
        static GpoRuleConfigured GetRunPluginEnabledValue(winrt::hstring const& pluginID);
        static GpoRuleConfigured GetConfiguredEnvironmentVariablesEnabledValue();
        static GpoRuleConfigured GetConfiguredQoiPreviewEnabledValue();
        static GpoRuleConfigured GetConfiguredQoiThumbnailsEnabledValue();
        static GpoRuleConfigured GetAllowedAdvancedPasteOnlineAIModelsValue();
        static GpoRuleConfigured GetConfiguredNewPlusEnabledValue();
        static GpoRuleConfigured GetConfiguredWorkspacesEnabledValue();
        static GpoRuleConfigured GetConfiguredMwbClipboardSharingEnabledValue();
        static GpoRuleConfigured GetConfiguredMwbFileTransferEnabledValue();
        static GpoRuleConfigured GetConfiguredMwbUseOriginalUserInterfaceValue();
        static GpoRuleConfigured GetConfiguredMwbDisallowBlockingScreensaverValue();
        static GpoRuleConfigured GetConfiguredMwbSameSubnetOnlyValue();
        static GpoRuleConfigured GetConfiguredMwbValidateRemoteIpValue();
        static GpoRuleConfigured GetConfiguredMwbDisableUserDefinedIpMappingRulesValue();
        static winrt::hstring GPOWrapper::GetConfiguredMwbPolicyDefinedIpMappingRules();
        static GpoRuleConfigured GetConfiguredNewPlusHideTemplateFilenameExtensionValue();
        static GpoRuleConfigured GetAllowDataDiagnosticsValue();
    };
}

namespace winrt::PowerToys::GPOWrapper::factory_implementation
{
    struct GPOWrapper : GPOWrapperT<GPOWrapper, implementation::GPOWrapper>
    {
    };
}
