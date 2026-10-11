// Thin C entry points over the shared PowerToys C++ headers, so Awake reuses the same group
// policy reader, telemetry provider (whose keyword values the release pipeline substitutes) and
// kernel object names as the rest of PowerToys instead of duplicating them.

#include <Windows.h>

#include <common/Telemetry/TraceBase.h>
#include <common/interop/shared_constants.h>
#include <common/utils/gpo.h>

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

// TraceLoggingWrite needs a literal event name, so each mode event is spelled out.
// All of Awake's events carry ProductAndServicePerformance, the privacy tag of every native
// PowerToys event and the only one TraceLoggingDefines.h defines. The .NET version declared
// ProductAndServiceUsage; taking the native tag instead is deliberate.
#define AWAKE_WRITE_MODE_EVENT(name)                                                      \
    TraceLoggingWriteWrapper(                                                             \
        g_hProvider,                                                                      \
        name,                                                                             \
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance), \
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),                                     \
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),                           \
        TraceLoggingWideString(version, "Version"))

namespace
{
    class AwakeTrace : public telemetry::TraceBase
    {
    public:
        // Event names and payload fields match the events the .NET version wrote.
        static void CliCommand(bool successful, const wchar_t* version) noexcept
        {
            TraceLoggingWriteWrapper(
                g_hProvider,
                "Awake_CLICommand",
                ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
                TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
                TraceLoggingWideString(version, "Version"),
                TraceLoggingWideString(L"awake", "CommandName"),
                TraceLoggingBoolean(successful, "Successful"));
        }

        static void ModeEntered(int mode, const wchar_t* version) noexcept
        {
            switch (mode)
            {
            case 0:
                AWAKE_WRITE_MODE_EVENT("AwakeNoKeepAwakeEvent");
                break;
            case 1:
                AWAKE_WRITE_MODE_EVENT("AwakeIndefinitelyKeepAwakeEvent");
                break;
            case 2:
                AWAKE_WRITE_MODE_EVENT("AwakeTimedKeepAwakeEvent");
                break;
            case 3:
                AWAKE_WRITE_MODE_EVENT("AwakeExpirableKeepAwakeEvent");
                break;
            default:
                break;
            }
        }
    };
}

extern "C" void awake_telemetry_register() noexcept
{
    AwakeTrace::RegisterProvider();
}

extern "C" void awake_telemetry_unregister() noexcept
{
    AwakeTrace::UnregisterProvider();
}

extern "C" void awake_telemetry_cli_command(bool successful, const wchar_t* version) noexcept
{
    AwakeTrace::CliCommand(successful, version);
}

extern "C" void awake_telemetry_mode_entered(int mode, const wchar_t* version) noexcept
{
    AwakeTrace::ModeEntered(mode, version);
}

extern "C" int awake_gpo_enabled_configuration() noexcept
{
    try
    {
        return static_cast<int>(powertoys_gpo::getConfiguredAwakeEnabledValue());
    }
    catch (...)
    {
        return static_cast<int>(powertoys_gpo::gpo_rule_configured_unavailable);
    }
}

extern "C" const wchar_t* awake_exit_event_name() noexcept
{
    return CommonSharedConstants::AWAKE_EXIT_EVENT;
}
