#pragma once

#define TraceLoggingOptionProjectTelemetry() TraceLoggingOptionGroup(0x42749043, 0x438c, 0x46a2, 0x82, 0xbe, 0xc6, 0xcb, 0xeb, 0x19, 0x2f, 0xf2)
#define ProjectTelemetryPrivacyDataTag(tag) TraceLoggingUInt64((tag), "Ignore")
#define ProjectTelemetryTag_ProductAndServicePerformance 0x0u
#define PROJECT_KEYWORD_MEASURE 0x0
