#pragma once

#include <Windows.h>
#include <optional>
#include <vector>
#include <string>

namespace powertoys_gpo
{
    enum gpo_rule_configured_t
    {
        gpo_rule_configured_wrong_value = -3, // The policy is set to an unrecognized value
        gpo_rule_configured_unavailable = -2, // Couldn't access registry
        gpo_rule_configured_not_configured = -1, // Policy is not configured
        gpo_rule_configured_disabled = 0, // Policy is disabled
        gpo_rule_configured_enabled = 1, // Policy is enabled
    };

    // Registry path where gpo policy values are stored.
    const std::wstring POLICIES_PATH = L"SOFTWARE\\Policies\\PowerToys";
    const std::wstring POWER_LAUNCHER_INDIVIDUAL_PLUGIN_ENABLED_LIST_PATH = POLICIES_PATH + L"\\PowerLauncherIndividualPluginEnabledList";

    // Registry scope where gpo policy values are stored.
    const HKEY POLICIES_SCOPE_MACHINE = HKEY_LOCAL_MACHINE;
    const HKEY POLICIES_SCOPE_USER = HKEY_CURRENT_USER;

    // The registry value names for PowerToys utilities enabled and disabled policies.
    const std::wstring POLICY_CONFIGURE_ENABLED_GLOBAL_ALL_UTILITIES = L"ConfigureGlobalUtilityEnabledState";
    const std::wstring POLICY_CONFIGURE_ENABLED_ALWAYS_ON_TOP = L"ConfigureEnabledUtilityAlwaysOnTop";
    const std::wstring POLICY_CONFIGURE_ENABLED_AWAKE = L"ConfigureEnabledUtilityAwake";
    const std::wstring POLICY_CONFIGURE_ENABLED_CMD_NOT_FOUND = L"ConfigureEnabledUtilityCmdNotFound";
    const std::wstring POLICY_CONFIGURE_ENABLED_COLOR_PICKER = L"ConfigureEnabledUtilityColorPicker";
    const std::wstring POLICY_CONFIGURE_ENABLED_CROP_AND_LOCK = L"ConfigureEnabledUtilityCropAndLock";
    const std::wstring POLICY_CONFIGURE_ENABLED_LIGHT_SWITCH = L"ConfigureEnabledUtilityLightSwitch";
    const std::wstring POLICY_CONFIGURE_ENABLED_POWER_DISPLAY = L"ConfigureEnabledUtilityPowerDisplay";
    const std::wstring POLICY_CONFIGURE_ENABLED_FANCYZONES = L"ConfigureEnabledUtilityFancyZones";
    const std::wstring POLICY_CONFIGURE_ENABLED_FILE_LOCKSMITH = L"ConfigureEnabledUtilityFileLocksmith";
    const std::wstring POLICY_CONFIGURE_ENABLED_SVG_PREVIEW = L"ConfigureEnabledUtilityFileExplorerSVGPreview";
    const std::wstring POLICY_CONFIGURE_ENABLED_MARKDOWN_PREVIEW = L"ConfigureEnabledUtilityFileExplorerMarkdownPreview";
    const std::wstring POLICY_CONFIGURE_ENABLED_MONACO_PREVIEW = L"ConfigureEnabledUtilityFileExplorerMonacoPreview";
    const std::wstring POLICY_CONFIGURE_ENABLED_PDF_PREVIEW = L"ConfigureEnabledUtilityFileExplorerPDFPreview";
    const std::wstring POLICY_CONFIGURE_ENABLED_GCODE_PREVIEW = L"ConfigureEnabledUtilityFileExplorerGcodePreview";
    const std::wstring POLICY_CONFIGURE_ENABLED_BGCODE_PREVIEW = L"ConfigureEnabledUtilityFileExplorerBgcodePreview";
    const std::wstring POLICY_CONFIGURE_ENABLED_SVG_THUMBNAILS = L"ConfigureEnabledUtilityFileExplorerSVGThumbnails";
    const std::wstring POLICY_CONFIGURE_ENABLED_PDF_THUMBNAILS = L"ConfigureEnabledUtilityFileExplorerPDFThumbnails";
    const std::wstring POLICY_CONFIGURE_ENABLED_GCODE_THUMBNAILS = L"ConfigureEnabledUtilityFileExplorerGcodeThumbnails";
    const std::wstring POLICY_CONFIGURE_ENABLED_BGCODE_THUMBNAILS = L"ConfigureEnabledUtilityFileExplorerBgcodeThumbnails";
    const std::wstring POLICY_CONFIGURE_ENABLED_STL_THUMBNAILS = L"ConfigureEnabledUtilityFileExplorerSTLThumbnails";
    const std::wstring POLICY_CONFIGURE_ENABLED_HOSTS_FILE_EDITOR = L"ConfigureEnabledUtilityHostsFileEditor";
    const std::wstring POLICY_CONFIGURE_ENABLED_IMAGE_RESIZER = L"ConfigureEnabledUtilityImageResizer";
    const std::wstring POLICY_CONFIGURE_ENABLED_KEYBOARD_MANAGER = L"ConfigureEnabledUtilityKeyboardManager";
    const std::wstring POLICY_CONFIGURE_ENABLED_FIND_MY_MOUSE = L"ConfigureEnabledUtilityFindMyMouse";
    const std::wstring POLICY_CONFIGURE_ENABLED_MOUSE_HIGHLIGHTER = L"ConfigureEnabledUtilityMouseHighlighter";
    const std::wstring POLICY_CONFIGURE_ENABLED_MOUSE_JUMP = L"ConfigureEnabledUtilityMouseJump";
    const std::wstring POLICY_CONFIGURE_ENABLED_MOUSE_POINTER_CROSSHAIRS = L"ConfigureEnabledUtilityMousePointerCrosshairs";
    const std::wstring POLICY_CONFIGURE_ENABLED_CURSOR_WRAP = L"ConfigureEnabledUtilityCursorWrap";
    const std::wstring POLICY_CONFIGURE_ENABLED_POWER_RENAME = L"ConfigureEnabledUtilityPowerRename";
    const std::wstring POLICY_CONFIGURE_ENABLED_POWER_LAUNCHER = L"ConfigureEnabledUtilityPowerLauncher";
    const std::wstring POLICY_CONFIGURE_ENABLED_QUICK_ACCENT = L"ConfigureEnabledUtilityQuickAccent";
    const std::wstring POLICY_CONFIGURE_ENABLED_SCREEN_RULER = L"ConfigureEnabledUtilityScreenRuler";
    const std::wstring POLICY_CONFIGURE_ENABLED_SHORTCUT_GUIDE = L"ConfigureEnabledUtilityShortcutGuide";
    const std::wstring POLICY_CONFIGURE_ENABLED_TEXT_EXTRACTOR = L"ConfigureEnabledUtilityTextExtractor";
    const std::wstring POLICY_CONFIGURE_ENABLED_ADVANCED_PASTE = L"ConfigureEnabledUtilityAdvancedPaste";
    const std::wstring POLICY_CONFIGURE_ENABLED_CMD_PAL = L"ConfigureEnabledUtilityCmdPal";
    const std::wstring POLICY_CONFIGURE_ENABLED_ZOOM_IT = L"ConfigureEnabledUtilityZoomIt";
    const std::wstring POLICY_CONFIGURE_ENABLED_REGISTRY_PREVIEW = L"ConfigureEnabledUtilityRegistryPreview";
    const std::wstring POLICY_CONFIGURE_ENABLED_MOUSE_WITHOUT_BORDERS = L"ConfigureEnabledUtilityMouseWithoutBorders";
    const std::wstring POLICY_CONFIGURE_ENABLED_PEEK = L"ConfigureEnabledUtilityPeek";
    const std::wstring POLICY_CONFIGURE_ENABLED_ENVIRONMENT_VARIABLES = L"ConfigureEnabledUtilityEnvironmentVariables";
    const std::wstring POLICY_CONFIGURE_ENABLED_QOI_PREVIEW = L"ConfigureEnabledUtilityFileExplorerQOIPreview";
    const std::wstring POLICY_CONFIGURE_ENABLED_QOI_THUMBNAILS = L"ConfigureEnabledUtilityFileExplorerQOIThumbnails";
    const std::wstring POLICY_CONFIGURE_ENABLED_NEWPLUS = L"ConfigureEnabledUtilityNewPlus";
    const std::wstring POLICY_CONFIGURE_ENABLED_WORKSPACES = L"ConfigureEnabledUtilityWorkspaces";

    // The registry value names for PowerToys installer and update policies.
    const std::wstring POLICY_DISABLE_PER_USER_INSTALLATION = L"PerUserInstallationDisabled";
    const std::wstring POLICY_DISABLE_AUTOMATIC_UPDATE_DOWNLOAD = L"AutomaticUpdateDownloadDisabled";
    const std::wstring POLICY_SUSPEND_NEW_UPDATE_TOAST = L"SuspendNewUpdateAvailableToast";
    const std::wstring POLICY_DISABLE_NEW_UPDATE_TOAST = L"DisableNewUpdateAvailableToast";
    const std::wstring POLICY_DISABLE_SHOW_WHATS_NEW_AFTER_UPDATES = L"DoNotShowWhatsNewAfterUpdates";

    // The registry value names for other PowerToys policies.
    const std::wstring POLICY_ALLOW_EXPERIMENTATION = L"AllowExperimentation";
    const std::wstring POLICY_ALLOW_DATA_DIAGNOSTICS = L"AllowDataDiagnostics";
    const std::wstring POLICY_CONFIGURE_RUN_AT_STARTUP = L"ConfigureRunAtStartup";
    const std::wstring POLICY_CONFIGURE_ENABLED_POWER_LAUNCHER_ALL_PLUGINS = L"PowerLauncherAllPluginsEnabledState";
    const std::wstring POLICY_ALLOW_ADVANCED_PASTE_ONLINE_AI_MODELS = L"AllowPowerToysAdvancedPasteOnlineAIModels";
    const std::wstring POLICY_ALLOW_ADVANCED_PASTE_OPENAI = L"AllowAdvancedPasteOpenAI";
    const std::wstring POLICY_ALLOW_ADVANCED_PASTE_AZURE_OPENAI = L"AllowAdvancedPasteAzureOpenAI";
    const std::wstring POLICY_ALLOW_ADVANCED_PASTE_AZURE_AI_INFERENCE = L"AllowAdvancedPasteAzureAIInference";
    const std::wstring POLICY_ALLOW_ADVANCED_PASTE_MISTRAL = L"AllowAdvancedPasteMistral";
    const std::wstring POLICY_ALLOW_ADVANCED_PASTE_GOOGLE = L"AllowAdvancedPasteGoogle";
    const std::wstring POLICY_ALLOW_ADVANCED_PASTE_OLLAMA = L"AllowAdvancedPasteOllama";
    const std::wstring POLICY_ALLOW_ADVANCED_PASTE_FOUNDRY_LOCAL = L"AllowAdvancedPasteFoundryLocal";
    const std::wstring POLICY_MWB_CLIPBOARD_SHARING_ENABLED = L"MwbClipboardSharingEnabled";
    const std::wstring POLICY_MWB_FILE_TRANSFER_ENABLED = L"MwbFileTransferEnabled";
    const std::wstring POLICY_MWB_USE_ORIGINAL_USER_INTERFACE = L"MwbUseOriginalUserInterface";
    const std::wstring POLICY_MWB_DISALLOW_BLOCKING_SCREENSAVER = L"MwbDisallowBlockingScreensaver";
    const std::wstring POLICY_MWB_ALLOW_SERVICE_MODE = L"MwbAllowServiceMode";
    const std::wstring POLICY_MWB_SAME_SUBNET_ONLY = L"MwbSameSubnetOnly";
    const std::wstring POLICY_MWB_VALIDATE_REMOTE_IP = L"MwbValidateRemoteIp";
    const std::wstring POLICY_MWB_DISABLE_USER_DEFINED_IP_MAPPING_RULES = L"MwbDisableUserDefinedIpMappingRules";
    const std::wstring POLICY_MWB_POLICY_DEFINED_IP_MAPPING_RULES = L"MwbPolicyDefinedIpMappingRules";
    const std::wstring POLICY_NEW_PLUS_HIDE_TEMPLATE_FILENAME_EXTENSION = L"NewPlusHideTemplateFilenameExtension";
    const std::wstring POLICY_NEW_PLUS_REPLACE_VARIABLES = L"NewPlusReplaceVariablesInTemplateFilenames";

    // Methods used for reading the registry - declarations
    // Implementations are in gpo.cpp
    std::optional<std::wstring> readRegistryStringValue(HKEY hRootKey, const std::wstring& subKey, const std::wstring& value_name, const bool is_multi_line_text = false);
    gpo_rule_configured_t getConfiguredValue(const std::wstring& registry_value_name);
    std::optional<std::wstring> getPolicyListValue(const std::wstring& registry_list_path, const std::wstring& registry_list_value_name);
    gpo_rule_configured_t getUtilityEnabledValue(const std::wstring& utility_name);

    // Utility enabled state policies
    // (Always use 'getUtilityEnabledValue()'.)
#pragma region UtilityEnabledStatePolicies
    inline gpo_rule_configured_t getConfiguredAlwaysOnTopEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_ALWAYS_ON_TOP);
    }

    inline gpo_rule_configured_t getConfiguredAwakeEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_AWAKE);
    }

    inline gpo_rule_configured_t getConfiguredCmdNotFoundEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_CMD_NOT_FOUND);
    }

    inline gpo_rule_configured_t getConfiguredColorPickerEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_COLOR_PICKER);
    }

    inline gpo_rule_configured_t getConfiguredCropAndLockEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_CROP_AND_LOCK);
    }

    inline gpo_rule_configured_t getConfiguredLightSwitchEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_LIGHT_SWITCH);
    }

    inline gpo_rule_configured_t getConfiguredPowerDisplayEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_POWER_DISPLAY);
    }

    inline gpo_rule_configured_t getConfiguredFancyZonesEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_FANCYZONES);
    }

    inline gpo_rule_configured_t getConfiguredFileLocksmithEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_FILE_LOCKSMITH);
    }

    inline gpo_rule_configured_t getConfiguredSvgPreviewEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_SVG_PREVIEW);
    }

    inline gpo_rule_configured_t getConfiguredMarkdownPreviewEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_MARKDOWN_PREVIEW);
    }

    inline gpo_rule_configured_t getConfiguredMonacoPreviewEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_MONACO_PREVIEW);
    }

    inline gpo_rule_configured_t getConfiguredPdfPreviewEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_PDF_PREVIEW);
    }

    inline gpo_rule_configured_t getConfiguredGcodePreviewEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_GCODE_PREVIEW);
    }

    inline gpo_rule_configured_t getConfiguredBgcodePreviewEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_BGCODE_PREVIEW);
    }

    inline gpo_rule_configured_t getConfiguredSvgThumbnailsEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_SVG_THUMBNAILS);
    }

    inline gpo_rule_configured_t getConfiguredPdfThumbnailsEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_PDF_THUMBNAILS);
    }

    inline gpo_rule_configured_t getConfiguredGcodeThumbnailsEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_GCODE_THUMBNAILS);
    }

    inline gpo_rule_configured_t getConfiguredBgcodeThumbnailsEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_BGCODE_THUMBNAILS);
    }

    inline gpo_rule_configured_t getConfiguredStlThumbnailsEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_STL_THUMBNAILS);
    }

    inline gpo_rule_configured_t getConfiguredHostsFileEditorEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_HOSTS_FILE_EDITOR);
    }

    inline gpo_rule_configured_t getConfiguredImageResizerEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_IMAGE_RESIZER);
    }

    inline gpo_rule_configured_t getConfiguredKeyboardManagerEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_KEYBOARD_MANAGER);
    }

    inline gpo_rule_configured_t getConfiguredFindMyMouseEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_FIND_MY_MOUSE);
    }

    inline gpo_rule_configured_t getConfiguredMouseHighlighterEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_MOUSE_HIGHLIGHTER);
    }

    inline gpo_rule_configured_t getConfiguredMouseJumpEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_MOUSE_JUMP);
    }

    inline gpo_rule_configured_t getConfiguredMousePointerCrosshairsEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_MOUSE_POINTER_CROSSHAIRS);
    }

    inline gpo_rule_configured_t getConfiguredCursorWrapEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_CURSOR_WRAP);
    }

    inline gpo_rule_configured_t getConfiguredPowerRenameEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_POWER_RENAME);
    }

    inline gpo_rule_configured_t getConfiguredPowerLauncherEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_POWER_LAUNCHER);
    }

    inline gpo_rule_configured_t getConfiguredQuickAccentEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_QUICK_ACCENT);
    }

    inline gpo_rule_configured_t getConfiguredScreenRulerEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_SCREEN_RULER);
    }

    inline gpo_rule_configured_t getConfiguredShortcutGuideEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_SHORTCUT_GUIDE);
    }

    inline gpo_rule_configured_t getConfiguredTextExtractorEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_TEXT_EXTRACTOR);
    }

    inline gpo_rule_configured_t getConfiguredAdvancedPasteEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_ADVANCED_PASTE);
    }

    inline gpo_rule_configured_t getConfiguredCmdPalEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_CMD_PAL);
    }

    inline gpo_rule_configured_t getConfiguredWorkspacesEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_WORKSPACES);
    }

    inline gpo_rule_configured_t getConfiguredZoomItEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_ZOOM_IT);
    }

    inline gpo_rule_configured_t getConfiguredMouseWithoutBordersEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_MOUSE_WITHOUT_BORDERS);
    }

    inline gpo_rule_configured_t getConfiguredPeekEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_PEEK);
    }

    inline gpo_rule_configured_t getConfiguredRegistryPreviewEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_REGISTRY_PREVIEW);
    }

    inline gpo_rule_configured_t getConfiguredEnvironmentVariablesEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_ENVIRONMENT_VARIABLES);
    }

    inline gpo_rule_configured_t getConfiguredQoiPreviewEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_QOI_PREVIEW);
    }

    inline gpo_rule_configured_t getConfiguredQoiThumbnailsEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_QOI_THUMBNAILS);
    }

    inline gpo_rule_configured_t getConfiguredNewPlusEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_NEWPLUS);
    }
#pragma endregion UtilityEnabledStatePolicies

    // Individual module setting policies
    // (Never use 'getUtilityEnabledValue()'!)
#pragma region IndividualModuleSettingPolicies
    inline gpo_rule_configured_t getDisablePerUserInstallationValue()
    {
        return getConfiguredValue(POLICY_DISABLE_PER_USER_INSTALLATION);
    }

    inline gpo_rule_configured_t getDisableAutomaticUpdateDownloadValue()
    {
        return getConfiguredValue(POLICY_DISABLE_AUTOMATIC_UPDATE_DOWNLOAD);
    }

    inline gpo_rule_configured_t getSuspendNewUpdateToastValue()
    {
        return getConfiguredValue(POLICY_SUSPEND_NEW_UPDATE_TOAST);
    }

    inline gpo_rule_configured_t getDisableNewUpdateToastValue()
    {
        return getConfiguredValue(POLICY_DISABLE_NEW_UPDATE_TOAST);
    }

    inline gpo_rule_configured_t getDisableShowWhatsNewAfterUpdatesValue()
    {
        return getConfiguredValue(POLICY_DISABLE_SHOW_WHATS_NEW_AFTER_UPDATES);
    }

    inline gpo_rule_configured_t getAllowExperimentationValue()
    {
        return getConfiguredValue(POLICY_ALLOW_EXPERIMENTATION);
    }

    inline gpo_rule_configured_t getAllowDataDiagnosticsValue()
    {
        return getConfiguredValue(POLICY_ALLOW_DATA_DIAGNOSTICS);
    }

    inline gpo_rule_configured_t getConfiguredRunAtStartupValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_RUN_AT_STARTUP);
    }

    inline gpo_rule_configured_t getRunPluginEnabledValue(std::string pluginID)
    {
        if (pluginID == "" || pluginID == " ")
        {
            // this plugin id can't exist in the registry
            return gpo_rule_configured_not_configured;
        }

        std::wstring plugin_id(pluginID.begin(), pluginID.end());
        auto individual_plugin_setting = getPolicyListValue(POWER_LAUNCHER_INDIVIDUAL_PLUGIN_ENABLED_LIST_PATH, plugin_id);

        if (individual_plugin_setting.has_value())
        {
            if (*individual_plugin_setting == L"0")
            {
                // force disabled
                return gpo_rule_configured_disabled;
            }
            else if (*individual_plugin_setting == L"1")
            {
                // force enabled
                return gpo_rule_configured_enabled;
            }
            else if (*individual_plugin_setting == L"2")
            {
                // user takes control
                return gpo_rule_configured_not_configured;
            }
            else
            {
                return gpo_rule_configured_wrong_value;
            }
        }
        else
        {
            // If no individual plugin policy exists, we check the policy with the setting for all plugins.
            return getConfiguredValue(POLICY_CONFIGURE_ENABLED_POWER_LAUNCHER_ALL_PLUGINS);
        }
    }

    inline gpo_rule_configured_t getAllowedAdvancedPasteOnlineAIModelsValue()
    {
        return getConfiguredValue(POLICY_ALLOW_ADVANCED_PASTE_ONLINE_AI_MODELS);
    }

    inline gpo_rule_configured_t getAllowedAdvancedPasteOpenAIValue()
    {
        return getConfiguredValue(POLICY_ALLOW_ADVANCED_PASTE_OPENAI);
    }

    inline gpo_rule_configured_t getAllowedAdvancedPasteAzureOpenAIValue()
    {
        return getConfiguredValue(POLICY_ALLOW_ADVANCED_PASTE_AZURE_OPENAI);
    }

    inline gpo_rule_configured_t getAllowedAdvancedPasteAzureAIInferenceValue()
    {
        return getConfiguredValue(POLICY_ALLOW_ADVANCED_PASTE_AZURE_AI_INFERENCE);
    }

    inline gpo_rule_configured_t getAllowedAdvancedPasteMistralValue()
    {
        return getConfiguredValue(POLICY_ALLOW_ADVANCED_PASTE_MISTRAL);
    }

    inline gpo_rule_configured_t getAllowedAdvancedPasteGoogleValue()
    {
        return getConfiguredValue(POLICY_ALLOW_ADVANCED_PASTE_GOOGLE);
    }

    inline gpo_rule_configured_t getAllowedAdvancedPasteOllamaValue()
    {
        return getConfiguredValue(POLICY_ALLOW_ADVANCED_PASTE_OLLAMA);
    }

    inline gpo_rule_configured_t getAllowedAdvancedPasteFoundryLocalValue()
    {
        return getConfiguredValue(POLICY_ALLOW_ADVANCED_PASTE_FOUNDRY_LOCAL);
    }

    inline gpo_rule_configured_t getConfiguredMwbClipboardSharingEnabledValue()
    {
        return getConfiguredValue(POLICY_MWB_CLIPBOARD_SHARING_ENABLED);
    }

    inline gpo_rule_configured_t getConfiguredMwbFileTransferEnabledValue()
    {
        return getConfiguredValue(POLICY_MWB_FILE_TRANSFER_ENABLED);
    }

    inline gpo_rule_configured_t getConfiguredMwbUseOriginalUserInterfaceValue()
    {
        return getConfiguredValue(POLICY_MWB_USE_ORIGINAL_USER_INTERFACE);
    }

    inline gpo_rule_configured_t getConfiguredMwbDisallowBlockingScreensaverValue()
    {
        return getConfiguredValue(POLICY_MWB_DISALLOW_BLOCKING_SCREENSAVER);
    }

    inline gpo_rule_configured_t getConfiguredMwbAllowServiceModeValue()
    {
        return getConfiguredValue(POLICY_MWB_ALLOW_SERVICE_MODE);
    }

    inline gpo_rule_configured_t getConfiguredMwbSameSubnetOnlyValue()
    {
        return getConfiguredValue(POLICY_MWB_SAME_SUBNET_ONLY);
    }

    inline gpo_rule_configured_t getConfiguredMwbValidateRemoteIpValue()
    {
        return getConfiguredValue(POLICY_MWB_VALIDATE_REMOTE_IP);
    }

    inline gpo_rule_configured_t getConfiguredMwbDisableUserDefinedIpMappingRulesValue()
    {
        return getConfiguredValue(POLICY_MWB_DISABLE_USER_DEFINED_IP_MAPPING_RULES);
    }

    inline std::wstring getConfiguredMwbPolicyDefinedIpMappingRules()
    {
        // Important: HKLM has priority over HKCU
        auto mapping_rules = readRegistryStringValue(HKEY_LOCAL_MACHINE, POLICIES_PATH, POLICY_MWB_POLICY_DEFINED_IP_MAPPING_RULES, true);
        if (!mapping_rules.has_value())
        {
            mapping_rules = readRegistryStringValue(HKEY_CURRENT_USER, POLICIES_PATH, POLICY_MWB_POLICY_DEFINED_IP_MAPPING_RULES, true);
        }

        // return value
        if (mapping_rules.has_value())
        {
            return mapping_rules.value();
        }
        else
        {
            return std::wstring();
        }
    }

    inline gpo_rule_configured_t getConfiguredNewPlusHideTemplateFilenameExtensionValue()
    {
        return getConfiguredValue(POLICY_NEW_PLUS_HIDE_TEMPLATE_FILENAME_EXTENSION);
    }

    inline gpo_rule_configured_t getConfiguredNewPlusReplaceVariablesValue()
    {
        return getConfiguredValue(POLICY_NEW_PLUS_REPLACE_VARIABLES);
    }
    
#pragma endregion IndividualModuleSettingPolicies
}
