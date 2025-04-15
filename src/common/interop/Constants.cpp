#include "pch.h"
#include "Constants.h"
#include "Constants.g.cpp"
#include "shared_constants.h"
#include <ShlObj.h>

namespace winrt::PowerToys::Interop::implementation
{
    uint32_t Constants::VK_WIN_BOTH()
    {
        return CommonSharedConstants::VK_WIN_BOTH;
    }
    hstring Constants::AppDataPath()
    {
        PWSTR local_app_path;
        winrt::check_hresult(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, NULL, &local_app_path));
        winrt::hstring result{ local_app_path };
        CoTaskMemFree(local_app_path);
        result = result + L"\\" + CommonSharedConstants::APPDATA_PATH;
        return result;
    }
    hstring Constants::PowerLauncherSharedEvent()
    {
        return CommonSharedConstants::POWER_LAUNCHER_SHARED_EVENT;
    }
    hstring Constants::PowerLauncherCentralizedHookSharedEvent()
    {
        return CommonSharedConstants::POWER_LAUNCHER_CENTRALIZED_HOOK_SHARED_EVENT;
    }
    hstring Constants::RunSendSettingsTelemetryEvent()
    {
        return CommonSharedConstants::RUN_SEND_SETTINGS_TELEMETRY_EVENT;
    }
    hstring Constants::RunExitEvent()
    {
        return CommonSharedConstants::RUN_EXIT_EVENT;
    }
    hstring Constants::FZEExitEvent()
    {
        return CommonSharedConstants::FZE_EXIT_EVENT;
    }
    hstring Constants::FZEToggleEvent()
    {
        return CommonSharedConstants::FANCY_ZONES_EDITOR_TOGGLE_EVENT;
    }
    hstring Constants::ColorPickerSendSettingsTelemetryEvent()
    {
        return CommonSharedConstants::COLOR_PICKER_SEND_SETTINGS_TELEMETRY_EVENT;
    }
    hstring Constants::ShowColorPickerSharedEvent()
    {
        return CommonSharedConstants::SHOW_COLOR_PICKER_SHARED_EVENT;
    }
    hstring Constants::TerminateColorPickerSharedEvent()
    {
        return CommonSharedConstants::TERMINATE_COLOR_PICKER_SHARED_EVENT;
    }
    hstring Constants::AdvancedPasteShowUIMessage()
    {
        return CommonSharedConstants::ADVANCED_PASTE_SHOW_UI_MESSAGE;
    }
    hstring Constants::AdvancedPasteMarkdownMessage()
    {
        return CommonSharedConstants::ADVANCED_PASTE_MARKDOWN_MESSAGE;
    }
    hstring Constants::AdvancedPasteJsonMessage()
    {
        return CommonSharedConstants::ADVANCED_PASTE_JSON_MESSAGE;
    }
    hstring Constants::AdvancedPasteAdditionalActionMessage()
    {
        return CommonSharedConstants::ADVANCED_PASTE_ADDITIONAL_ACTION_MESSAGE;
    }
    hstring Constants::AdvancedPasteCustomActionMessage()
    {
        return CommonSharedConstants::ADVANCED_PASTE_CUSTOM_ACTION_MESSAGE;
    }
    hstring Constants::AdvancedPasteTerminateAppMessage()
    {
        return CommonSharedConstants::ADVANCED_PASTE_TERMINATE_APP_MESSAGE;
    }
    hstring Constants::ShowPowerOCRSharedEvent()
    {
        return CommonSharedConstants::SHOW_POWEROCR_SHARED_EVENT;
    }
    hstring Constants::TerminatePowerOCRSharedEvent()
    {
        return CommonSharedConstants::TERMINATE_POWEROCR_SHARED_EVENT;
    }
    hstring Constants::MouseJumpShowPreviewEvent()
    {
        return CommonSharedConstants::MOUSE_JUMP_SHOW_PREVIEW_EVENT;
    }
    hstring Constants::TerminateMouseJumpSharedEvent()
    {
        return CommonSharedConstants::TERMINATE_MOUSE_JUMP_SHARED_EVENT;
    }
    hstring Constants::AwakeExitEvent()
    {
        return CommonSharedConstants::AWAKE_EXIT_EVENT;
    }
    hstring Constants::ShowPeekEvent()
    {
        return CommonSharedConstants::SHOW_PEEK_SHARED_EVENT;
    }
    hstring Constants::TerminatePeekEvent()
    {
        return CommonSharedConstants::TERMINATE_PEEK_SHARED_EVENT;
    }
    hstring Constants::PowerAccentExitEvent()
    {
        return CommonSharedConstants::POWERACCENT_EXIT_EVENT;
    }
    hstring Constants::ShortcutGuideTriggerEvent()
    {
        return CommonSharedConstants::SHORTCUT_GUIDE_TRIGGER_EVENT;
    }
    hstring Constants::RegistryPreviewTriggerEvent()
    {
        return CommonSharedConstants::REGISTRY_PREVIEW_TRIGGER_EVENT;
    }
    hstring Constants::MeasureToolTriggerEvent()
    {
        return CommonSharedConstants::MEASURE_TOOL_TRIGGER_EVENT;
    }
    hstring Constants::GcodePreviewResizeEvent()
    {
        return CommonSharedConstants::GCODE_PREVIEW_RESIZE_EVENT;
    }
    hstring Constants::QoiPreviewResizeEvent()
    {
        return CommonSharedConstants::QOI_PREVIEW_RESIZE_EVENT;
    }
    hstring Constants::DevFilesPreviewResizeEvent()
    {
        return CommonSharedConstants::DEV_FILES_PREVIEW_RESIZE_EVENT;
    }
    hstring Constants::MarkdownPreviewResizeEvent()
    {
        return CommonSharedConstants::MARKDOWN_PREVIEW_RESIZE_EVENT;
    }
    hstring Constants::PdfPreviewResizeEvent()
    {
        return CommonSharedConstants::PDF_PREVIEW_RESIZE_EVENT;
    }
    hstring Constants::SvgPreviewResizeEvent()
    {
        return CommonSharedConstants::SVG_PREVIEW_RESIZE_EVENT;
    }
    hstring Constants::ShowHostsSharedEvent()
    {
        return CommonSharedConstants::SHOW_HOSTS_EVENT;
    }
    hstring Constants::ShowHostsAdminSharedEvent()
    {
        return CommonSharedConstants::SHOW_HOSTS_ADMIN_EVENT;
    }
    hstring Constants::TerminateHostsSharedEvent()
    {
        return CommonSharedConstants::TERMINATE_HOSTS_EVENT;
    }
    hstring Constants::CropAndLockThumbnailEvent()
    {
        return CommonSharedConstants::CROP_AND_LOCK_THUMBNAIL_EVENT;
    }
    hstring Constants::CropAndLockReparentEvent()
    {
        return CommonSharedConstants::CROP_AND_LOCK_REPARENT_EVENT;
    }
    hstring Constants::ShowEnvironmentVariablesSharedEvent()
    {
        return CommonSharedConstants::SHOW_ENVIRONMENT_VARIABLES_EVENT;
    }
    hstring Constants::ShowEnvironmentVariablesAdminSharedEvent()
    {
        return CommonSharedConstants::SHOW_ENVIRONMENT_VARIABLES_ADMIN_EVENT;
    }
    hstring Constants::WorkspacesLaunchEditorEvent()
    {
        return CommonSharedConstants::WORKSPACES_LAUNCH_EDITOR_EVENT;
    }
    hstring Constants::WorkspacesHotkeyEvent()
    {
        return CommonSharedConstants::WORKSPACES_HOTKEY_EVENT;
    }
    hstring Constants::PowerToysRunnerTerminateSettingsEvent()
    {
        return CommonSharedConstants::TERMINATE_SETTINGS_SHARED_EVENT;
    }
    hstring Constants::ShowCmdPalEvent()
    {
        return CommonSharedConstants::CMDPAL_SHOW_EVENT;
    }
}
