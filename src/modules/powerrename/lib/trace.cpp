#include "pch.h"
#include "trace.h"
#include "Settings.h"

TRACELOGGING_DEFINE_PROVIDER(
      g_hProvider,
      "Microsoft.PowerToys",
      // {38e8889b-9731-53f5-e901-e8a7c1753074}
      (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
      TraceLoggingOptionProjectTelemetry());

void Trace::RegisterProvider() noexcept
{
    TraceLoggingRegister(g_hProvider);
}

void Trace::UnregisterProvider() noexcept
{
    TraceLoggingUnregister(g_hProvider);
}

void Trace::Invoked() noexcept
{
  TraceLoggingWrite(
        g_hProvider,
        "PowerRename_Invoked",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::InvokedRet(_In_ HRESULT hr) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerRename_InvokedRet",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingHResult(hr),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::EnablePowerRename(_In_ bool enabled) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerRename_EnablePowerRename",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

void Trace::UIShownRet(_In_ HRESULT hr) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerRename_UIShownRet",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingHResult(hr),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::RenameOperation(_In_ UINT totalItemCount, _In_ UINT selectedItemCount, _In_ UINT renameItemCount, _In_ DWORD flags, _In_ PCWSTR extensionList) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerRename_RenameOperation",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingUInt32(totalItemCount, "TotalItemCount"),
        TraceLoggingUInt32(selectedItemCount, "SelectedItemCount"),
        TraceLoggingUInt32(renameItemCount, "RenameItemCount"),
        TraceLoggingInt32(flags, "Flags"),
        TraceLoggingWideString(extensionList, "ExtensionList"));
}

void Trace::SettingsChanged() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerRename_SettingsChanged",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(CSettingsInstance().GetEnabled(), "IsEnabled"),
        TraceLoggingBoolean(CSettingsInstance().GetShowIconOnMenu(), "ShowIconOnMenu"),
        TraceLoggingBoolean(CSettingsInstance().GetExtendedContextMenuOnly(), "ExtendedContextMenuOnly"),
        TraceLoggingBoolean(CSettingsInstance().GetPersistState(), "PersistState"),
        TraceLoggingBoolean(CSettingsInstance().GetMRUEnabled(), "IsMRUEnabled"),
        TraceLoggingUInt64(CSettingsInstance().GetMaxMRUSize(), "MaxMRUSize"),
        TraceLoggingBoolean(CSettingsInstance().GetUseBoostLib(), "UseBoostLib"),
        TraceLoggingUInt64(CSettingsInstance().GetFlags(), "Flags"));
}
