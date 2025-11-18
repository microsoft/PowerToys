#include "pch.h"

#include "trace.h"
#include <common/Telemetry/ProjectTelemetry.h>

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

void Trace::EventToggleOnOff(_In_ const bool enabled) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "NewPlus_EventToggleOnOff",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

void Trace::EventChangedTemplateLocation() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "NewPlus_ChangedTemplateLocation",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::EventShowTemplateItems(const size_t number_of_templates) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "NewPlus_EventShowTemplateItems",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(number_of_templates, "Count"));
}

void Trace::EventCopyTemplate(_In_ const std::wstring template_file_extension) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "NewPlus_EventCopyTemplate",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingWideString(template_file_extension.c_str(), "Ext"));
}

void Trace::EventCopyTemplateResult(_In_ const HRESULT hr) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "NewPlus_EventCopyTemplateResult",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingHResult(hr),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::EventOpenTemplates() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "NewPlus_EventOpenTemplates",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}
