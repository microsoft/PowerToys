#pragma once

#ifndef PCH_H
#define PCH_H

#define WIN32_LEAN_AND_MEAN // Exclude rarely-used stuff from Windows headers
#include <windows.h>
#include <Unknwn.h>
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <ProjectTelemetry.h>
#include <TraceLoggingActivity.h>
#include <wil\common.h>
#include <wil\result.h>

#endif //PCH_H
