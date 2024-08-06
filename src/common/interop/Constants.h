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
        static hstring ShowAdvancedPasteSharedEvent();
        static hstring AdvancedPasteMarkdownEvent();
        static hstring AdvancedPasteJsonEvent();
        static hstring ShowPowerOCRSharedEvent();
        static hstring MouseJumpShowPreviewEvent();
        static hstring AwakeExitEvent();
        static hstring ShowPeekEvent();
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
        static hstring CropAndLockThumbnailEvent();
        static hstring CropAndLockReparentEvent();
        static hstring ShowEnvironmentVariablesSharedEvent();
        static hstring ShowEnvironmentVariablesAdminSharedEvent();
    };
}

namespace winrt::PowerToys::Interop::factory_implementation
{
    struct Constants : ConstantsT<Constants, implementation::Constants>
    {
    };
}
