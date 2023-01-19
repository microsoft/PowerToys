#pragma once

#include <Windows.h>

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

    // Registry scope where gpo policy values are stored.
    const HKEY POLICIES_SCOPE_MACHINE = HKEY_LOCAL_MACHINE;
    const HKEY POLICIES_SCOPE_USER = HKEY_CURRENT_USER;

    // The registry value names for PowerToys utilities enabled and disabled policies.
    const std::wstring POLICY_CONFIGURE_ENABLED_ALWAYS_ON_TOP = L"ConfigureEnabledUtilityAlwaysOnTop";
    const std::wstring POLICY_CONFIGURE_ENABLED_AWAKE = L"ConfigureEnabledUtilityAwake";
    const std::wstring POLICY_CONFIGURE_ENABLED_COLOR_PICKER = L"ConfigureEnabledUtilityColorPicker";
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
    const std::wstring POLICY_CONFIGURE_ENABLED_MOUSE_POINTER_CROSSHAIRS = L"ConfigureEnabledUtilityMousePointerCrosshairs";
    const std::wstring POLICY_CONFIGURE_ENABLED_POWER_RENAME = L"ConfigureEnabledUtilityPowerRename";
    const std::wstring POLICY_CONFIGURE_ENABLED_POWER_LAUNCHER = L"ConfigureEnabledUtilityPowerLauncher";
    const std::wstring POLICY_CONFIGURE_ENABLED_QUICK_ACCENT = L"ConfigureEnabledUtilityQuickAccent";
    const std::wstring POLICY_CONFIGURE_ENABLED_SCREEN_RULER = L"ConfigureEnabledUtilityScreenRuler";
    const std::wstring POLICY_CONFIGURE_ENABLED_SHORTCUT_GUIDE = L"ConfigureEnabledUtilityShortcutGuide";
    const std::wstring POLICY_CONFIGURE_ENABLED_TEXT_EXTRACTOR = L"ConfigureEnabledUtilityTextExtractor";
    const std::wstring POLICY_CONFIGURE_ENABLED_VIDEO_CONFERENCE_MUTE = L"ConfigureEnabledUtilityVideoConferenceMute";

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

    inline gpo_rule_configured_t getConfiguredAlwaysOnTopEnabledValue() {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_ALWAYS_ON_TOP);
    }

    inline gpo_rule_configured_t getConfiguredAwakeEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_AWAKE);
    }

    inline gpo_rule_configured_t getConfiguredColorPickerEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_COLOR_PICKER);
    }

    inline gpo_rule_configured_t getConfiguredFancyZonesEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_FANCYZONES);
    }

    inline gpo_rule_configured_t getConfiguredFileLocksmithEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_FILE_LOCKSMITH);
    }

    inline gpo_rule_configured_t getConfiguredSvgPreviewEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_SVG_PREVIEW);
    }

    inline gpo_rule_configured_t getConfiguredMarkdownPreviewEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_MARKDOWN_PREVIEW);
    }

    inline gpo_rule_configured_t getConfiguredMonacoPreviewEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_MONACO_PREVIEW);
    }

    inline gpo_rule_configured_t getConfiguredPdfPreviewEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_PDF_PREVIEW);
    }

    inline gpo_rule_configured_t getConfiguredGcodePreviewEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_GCODE_PREVIEW);
    }

    inline gpo_rule_configured_t getConfiguredSvgThumbnailsEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_SVG_THUMBNAILS);
    }

    inline gpo_rule_configured_t getConfiguredPdfThumbnailsEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_PDF_THUMBNAILS);
    }

    inline gpo_rule_configured_t getConfiguredGcodeThumbnailsEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_GCODE_THUMBNAILS);
    }

    inline gpo_rule_configured_t getConfiguredStlThumbnailsEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_STL_THUMBNAILS);
    }

    inline gpo_rule_configured_t getConfiguredHostsFileEditorEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_HOSTS_FILE_EDITOR);
    }

    inline gpo_rule_configured_t getConfiguredImageResizerEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_IMAGE_RESIZER);
    }

    inline gpo_rule_configured_t getConfiguredKeyboardManagerEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_KEYBOARD_MANAGER);
    }

    inline gpo_rule_configured_t getConfiguredFindMyMouseEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_FIND_MY_MOUSE);
    }

    inline gpo_rule_configured_t getConfiguredMouseHighlighterEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_MOUSE_HIGHLIGHTER);
    }

    inline gpo_rule_configured_t getConfiguredMousePointerCrosshairsEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_MOUSE_POINTER_CROSSHAIRS);
    }

    inline gpo_rule_configured_t getConfiguredPowerRenameEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_POWER_RENAME);
    }

    inline gpo_rule_configured_t getConfiguredPowerLauncherEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_POWER_LAUNCHER);
    }

    inline gpo_rule_configured_t getConfiguredQuickAccentEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_QUICK_ACCENT);
    }

    inline gpo_rule_configured_t getConfiguredScreenRulerEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_SCREEN_RULER);
    }

    inline gpo_rule_configured_t getConfiguredShortcutGuideEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_SHORTCUT_GUIDE);
    }

    inline gpo_rule_configured_t getConfiguredTextExtractorEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_TEXT_EXTRACTOR);
    }

    inline gpo_rule_configured_t getConfiguredVideoConferenceMuteEnabledValue()
    {
        return getConfiguredValue(POLICY_CONFIGURE_ENABLED_VIDEO_CONFERENCE_MUTE);
    }

}
