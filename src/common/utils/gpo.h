#pragma once

#include <Windows.h>
#include <optional>
#include <vector>

namespace powertoys_gpo {
    enum gpo_rule_configured_t {
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
    const std::wstring POLICY_CONFIGURE_ENABLED_FANCYZONES = L"ConfigureEnabledUtilityFancyZones";
    const std::wstring POLICY_CONFIGURE_ENABLED_FILE_LOCKSMITH = L"ConfigureEnabledUtilityFileLocksmith";
    const std::wstring POLICY_CONFIGURE_ENABLED_SVG_PREVIEW = L"ConfigureEnabledUtilityFileExplorerSVGPreview";
    const std::wstring POLICY_CONFIGURE_ENABLED_MARKDOWN_PREVIEW = L"ConfigureEnabledUtilityFileExplorerMarkdownPreview";
    const std::wstring POLICY_CONFIGURE_ENABLED_MONACO_PREVIEW = L"ConfigureEnabledUtilityFileExplorerMonacoPreview";
    const std::wstring POLICY_CONFIGURE_ENABLED_PDF_PREVIEW = L"ConfigureEnabledUtilityFileExplorerPDFPreview";
    const std::wstring POLICY_CONFIGURE_ENABLED_GCODE_PREVIEW = L"ConfigureEnabledUtilityFileExplorerGcodePreview";
    const std::wstring POLICY_CONFIGURE_ENABLED_SVG_THUMBNAILS = L"ConfigureEnabledUtilityFileExplorerSVGThumbnails";
    const std::wstring POLICY_CONFIGURE_ENABLED_PDF_THUMBNAILS = L"ConfigureEnabledUtilityFileExplorerPDFThumbnails";
    const std::wstring POLICY_CONFIGURE_ENABLED_GCODE_THUMBNAILS = L"ConfigureEnabledUtilityFileExplorerGcodeThumbnails";
    const std::wstring POLICY_CONFIGURE_ENABLED_STL_THUMBNAILS = L"ConfigureEnabledUtilityFileExplorerSTLThumbnails";
    const std::wstring POLICY_CONFIGURE_ENABLED_HOSTS_FILE_EDITOR = L"ConfigureEnabledUtilityHostsFileEditor";
    const std::wstring POLICY_CONFIGURE_ENABLED_IMAGE_RESIZER = L"ConfigureEnabledUtilityImageResizer";
    const std::wstring POLICY_CONFIGURE_ENABLED_KEYBOARD_MANAGER = L"ConfigureEnabledUtilityKeyboardManager";
    const std::wstring POLICY_CONFIGURE_ENABLED_FIND_MY_MOUSE = L"ConfigureEnabledUtilityFindMyMouse";
    const std::wstring POLICY_CONFIGURE_ENABLED_MOUSE_HIGHLIGHTER = L"ConfigureEnabledUtilityMouseHighlighter";
    const std::wstring POLICY_CONFIGURE_ENABLED_MOUSE_JUMP = L"ConfigureEnabledUtilityMouseJump";
    const std::wstring POLICY_CONFIGURE_ENABLED_MOUSE_POINTER_CROSSHAIRS = L"ConfigureEnabledUtilityMousePointerCrosshairs";
    const std::wstring POLICY_CONFIGURE_ENABLED_POWER_RENAME = L"ConfigureEnabledUtilityPowerRename";
    const std::wstring POLICY_CONFIGURE_ENABLED_POWER_LAUNCHER = L"ConfigureEnabledUtilityPowerLauncher";
    const std::wstring POLICY_CONFIGURE_ENABLED_QUICK_ACCENT = L"ConfigureEnabledUtilityQuickAccent";
    const std::wstring POLICY_CONFIGURE_ENABLED_SCREEN_RULER = L"ConfigureEnabledUtilityScreenRuler";
    const std::wstring POLICY_CONFIGURE_ENABLED_SHORTCUT_GUIDE = L"ConfigureEnabledUtilityShortcutGuide";
    const std::wstring POLICY_CONFIGURE_ENABLED_TEXT_EXTRACTOR = L"ConfigureEnabledUtilityTextExtractor";
    const std::wstring POLICY_CONFIGURE_ENABLED_ADVANCED_PASTE = L"ConfigureEnabledUtilityAdvancedPaste";
    const std::wstring POLICY_CONFIGURE_ENABLED_VIDEO_CONFERENCE_MUTE = L"ConfigureEnabledUtilityVideoConferenceMute";
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
    const std::wstring POLICY_CONFIGURE_ENABLED_POWER_LAUNCHER_ALL_PLUGINS = L"PowerLauncherAllPluginsEnabledState";
    const std::wstring POLICY_ALLOW_ADVANCED_PASTE_ONLINE_AI_MODELS = L"AllowPowerToysAdvancedPasteOnlineAIModels";
    const std::wstring POLICY_MWB_CLIPBOARD_SHARING_ENABLED = L"MwbClipboardSharingEnabled";
    const std::wstring POLICY_MWB_FILE_TRANSFER_ENABLED = L"MwbFileTransferEnabled";
    const std::wstring POLICY_MWB_USE_ORIGINAL_USER_INTERFACE = L"MwbUseOriginalUserInterface";
    const std::wstring POLICY_MWB_DISALLOW_BLOCKING_SCREENSAVER = L"MwbDisallowBlockingScreensaver";
    const std::wstring POLICY_MWB_SAME_SUBNET_ONLY = L"MwbSameSubnetOnly";
    const std::wstring POLICY_MWB_VALIDATE_REMOTE_IP = L"MwbValidateRemoteIp";
    const std::wstring POLICY_MWB_DISABLE_USER_DEFINED_IP_MAPPING_RULES = L"MwbDisableUserDefinedIpMappingRules";
    const std::wstring POLICY_MWB_POLICY_DEFINED_IP_MAPPING_RULES = L"MwbPolicyDefinedIpMappingRules";
    const std::wstring POLICY_NEW_PLUS_HIDE_TEMPLATE_FILENAME_EXTENSION = L"NewPlusHideTemplateFilenameExtension";

    // Methods used for reading the registry
#pragma region ReadRegistryMethods
    inline std::optional<std::wstring> readRegistryStringValue(HKEY hRootKey, const std::wstring& subKey, const std::wstring& value_name, const bool is_multi_line_text = false)
    {
        // Set value type
        DWORD reg_value_type = REG_SZ;
        DWORD reg_flags = RRF_RT_REG_SZ;
        if (is_multi_line_text)
        {
            reg_value_type = REG_MULTI_SZ;
            reg_flags = RRF_RT_REG_MULTI_SZ;
        }

        DWORD string_buffer_capacity;
        // Request required buffer capacity / string length
        if (RegGetValueW(hRootKey, subKey.c_str(), value_name.c_str(), reg_flags, &reg_value_type, NULL, &string_buffer_capacity) != ERROR_SUCCESS)
        {
            return std::nullopt;
        }
        else if (string_buffer_capacity == 0)
        {
            return std::nullopt;
        }

        // RegGetValueW overshoots sometimes. Use a buffer first to not have characters past the string end.
        wchar_t* temp_buffer = new wchar_t[string_buffer_capacity / sizeof(wchar_t) + 1];
        // Read string
        if (RegGetValueW(hRootKey, subKey.c_str(), value_name.c_str(), reg_flags, &reg_value_type, temp_buffer, &string_buffer_capacity) != ERROR_SUCCESS)
        {
            delete temp_buffer;
            return std::nullopt;
        }

        // Convert buffer to std::wstring
        std::wstring string_value = L"";
        if (reg_value_type == REG_MULTI_SZ)
        {
            // If it is REG_MULTI_SZ handle this way
            wchar_t* currentString = temp_buffer;
            while (*currentString != L'\0')
            {
                // If first entry then assign the string, else add to the string
                string_value = (string_value == L"") ? currentString : (string_value + L"\r\n" + currentString);
                currentString += wcslen(currentString) + 1; // Move to the next string
            }
        }
        else
        {
            // If it is REG_SZ handle this way
            string_value = temp_buffer;
        }

        // delete buffer, return string value
        delete temp_buffer;
        return string_value;
    }

    inline gpo_rule_configured_t getConfiguredValue(const std::wstring& registry_value_name)
    {
        HKEY key{};
        DWORD value = 0xFFFFFFFE;
        DWORD valueSize = sizeof(value);

        bool machine_key_found = true;
        if (auto res = RegOpenKeyExW(POLICIES_SCOPE_MACHINE, POLICIES_PATH.c_str(), 0, KEY_READ, &key); res != ERROR_SUCCESS)
        {
            machine_key_found = false;
        }

        if(machine_key_found)
        {
            // If the path was found in the machine, we need to check if the value for the policy exists.
            auto res = RegQueryValueExW(key, registry_value_name.c_str(), nullptr, nullptr, reinterpret_cast<LPBYTE>(&value), &valueSize);

            RegCloseKey(key);

            if (res != ERROR_SUCCESS) {
                // Value not found on the path.
                machine_key_found=false;
            }
        }

        if (!machine_key_found)
        {
            // If there's no value found on the machine scope, try to get it from the user scope.
            if (auto res = RegOpenKeyExW(POLICIES_SCOPE_USER, POLICIES_PATH.c_str(), 0, KEY_READ, &key); res != ERROR_SUCCESS)
            {
                if (res == ERROR_FILE_NOT_FOUND) {
                    return gpo_rule_configured_not_configured;
                }
                return gpo_rule_configured_unavailable;
            }
            auto res = RegQueryValueExW(key, registry_value_name.c_str(), nullptr, nullptr, reinterpret_cast<LPBYTE>(&value), &valueSize);
            RegCloseKey(key);

            if (res != ERROR_SUCCESS) {
                return gpo_rule_configured_not_configured;
            }
        }

        switch (value)
        {
        case 0:
            return gpo_rule_configured_disabled;
        case 1:
            return gpo_rule_configured_enabled;
        default:
            return gpo_rule_configured_wrong_value;
        }
    }

    inline std::optional<std::wstring> getPolicyListValue(const std::wstring& registry_list_path, const std::wstring& registry_list_value_name)
    {
        // This function returns the value of an entry of an policy list. The user scope is only checked, if the list is not enabled for the machine to not mix the lists.

        HKEY key{};

        // Try to read from the machine list.
        bool machine_list_found = false;
        if (RegOpenKeyExW(POLICIES_SCOPE_MACHINE, registry_list_path.c_str(), 0, KEY_READ, &key) == ERROR_SUCCESS)
        {
            machine_list_found = true;
            RegCloseKey(key);

            // If the path exists in the machine registry, we try to read the value.
            auto regValueData = readRegistryStringValue(POLICIES_SCOPE_MACHINE, registry_list_path, registry_list_value_name);

            if (regValueData.has_value())
            {
                // Return the value from the machine list.
                return *regValueData;
            }
        }

        // If no list exists for machine, we try to read from the user list.
        if (!machine_list_found)
        {
            if (RegOpenKeyExW(POLICIES_SCOPE_USER, registry_list_path.c_str(), 0, KEY_READ, &key) == ERROR_SUCCESS)
            {
                RegCloseKey(key);

                // If the path exists in the user registry, we try to read the value.
                auto regValueData = readRegistryStringValue(POLICIES_SCOPE_USER, registry_list_path, registry_list_value_name);

                if (regValueData.has_value())
                {
                    // Return the value from the user list.
                    return *regValueData;
                }
            }
        }

        // No list exists for machine and user, or no value was found in the list, or an error ocurred while reading the value.
        return std::nullopt;
    }

    inline gpo_rule_configured_t getUtilityEnabledValue(const std::wstring& utility_name)
    {
        auto individual_value = getConfiguredValue(utility_name);

        if (individual_value == gpo_rule_configured_disabled || individual_value == gpo_rule_configured_enabled)
        {
            return individual_value;
        }
        else
        {
            return getConfiguredValue(POLICY_CONFIGURE_ENABLED_GLOBAL_ALL_UTILITIES);
        }
    }
#pragma endregion ReadRegistryMethods

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

    inline gpo_rule_configured_t getConfiguredWorkspacesEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_WORKSPACES);
    }

    inline gpo_rule_configured_t getConfiguredVideoConferenceMuteEnabledValue()
    {
        return getUtilityEnabledValue(POLICY_CONFIGURE_ENABLED_VIDEO_CONFERENCE_MUTE);
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
            return std::wstring ();
        }
    }

    inline gpo_rule_configured_t getConfiguredNewPlusHideTemplateFilenameExtensionValue()
    {
        return getConfiguredValue(POLICY_NEW_PLUS_HIDE_TEMPLATE_FILENAME_EXTENSION);
    }
#pragma endregion IndividualModuleSettingPolicies
}
