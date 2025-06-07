#pragma once
#include "Constants.g.h"
namespace winrt::PowerToys::Interop::implementation
{
    struct Constants : ConstantsT<Constants>
    {
        Constants() = default;

        static uint32_t VK_WIN_BOTH();
        static hstring AppDataPath();
        static hstring PowerLauncherSharedEvent();
        static hstring PowerLauncherCentralizedHookSharedEvent();
        static hstring RunSendSettingsTelemetryEvent();
        static hstring RunExitEvent();
        static hstring FZEExitEvent();
        static hstring FZEToggleEvent();
        static hstring ColorPickerSendSettingsTelemetryEvent();
        static hstring ShowColorPickerSharedEvent();
        static hstring TerminateColorPickerSharedEvent();
        static hstring AdvancedPasteShowUIMessage();
        static hstring AdvancedPasteMarkdownMessage();
        static hstring AdvancedPasteJsonMessage();
        static hstring AdvancedPasteAdditionalActionMessage();
        static hstring AdvancedPasteCustomActionMessage();
        static hstring AdvancedPasteTerminateAppMessage();
        static hstring ShowPowerOCRSharedEvent();
        static hstring TerminatePowerOCRSharedEvent();
        static hstring MouseJumpShowPreviewEvent();
        static hstring TerminateMouseJumpSharedEvent();
        static hstring AwakeExitEvent();
        static hstring ShowPeekEvent();
        static hstring TerminatePeekEvent();
        static hstring PowerAccentExitEvent();
        static hstring ShortcutGuideTriggerEvent();
        static hstring RegistryPreviewTriggerEvent();
        static hstring MeasureToolTriggerEvent();
        static hstring GcodePreviewResizeEvent();
        static hstring QoiPreviewResizeEvent();
        static hstring DevFilesPreviewResizeEvent();
        static hstring MarkdownPreviewResizeEvent();
        static hstring PdfPreviewResizeEvent();
        static hstring SvgPreviewResizeEvent();
        static hstring ShowHostsSharedEvent();
        static hstring ShowHostsAdminSharedEvent();
        static hstring TerminateHostsSharedEvent();
        static hstring CropAndLockThumbnailEvent();
        static hstring CropAndLockReparentEvent();
        static hstring ShowEnvironmentVariablesSharedEvent();
        static hstring ShowEnvironmentVariablesAdminSharedEvent();
        static hstring WorkspacesLaunchEditorEvent();
        static hstring WorkspacesHotkeyEvent();
        static hstring PowerToysRunnerTerminateSettingsEvent();
        static hstring ShowCmdPalEvent();
    };
}

namespace winrt::PowerToys::Interop::factory_implementation
{
    struct Constants : ConstantsT<Constants, implementation::Constants>
    {
    };
}
