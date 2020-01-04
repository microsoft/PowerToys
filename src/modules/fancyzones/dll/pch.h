#pragma once

#define WIN32_LEAN_AND_MEAN // Exclude rarely-used stuff from Windows headers
#include <Unknwn.h>
#include <winrt/base.h>
#include <ProjectTelemetry.h>
#include <TraceLoggingActivity.h>
#include <wil\common.h>
#include <wil\result.h>


namespace winrt
{
    using namespace ::winrt;
}