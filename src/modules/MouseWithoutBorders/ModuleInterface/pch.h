#pragma once

#define WIN32_LEAN_AND_MEAN // Exclude rarely-used stuff from Windows headers
#include <Unknwn.h>
#include <filesystem>
#include <Lmcons.h>
#include <shellapi.h>
#include <sddl.h>
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <ProjectTelemetry.h>
#include <TraceLoggingActivity.h>

#include <wil\common.h>
#include <wil\result.h>
#include <wil\resource.h>
