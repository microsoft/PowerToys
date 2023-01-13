#pragma once
#include "Generated Files/resource.h"

#define WIN32_LEAN_AND_MEAN // Exclude rarely-used stuff from Windows headers
#include <Unknwn.h>
#include <winrt/base.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <windows.h>
#include <windowsx.h>
#include <dwmapi.h>
#include <ProjectTelemetry.h>
#include <shellapi.h>
#include <ShellScalingApi.h>
#include <strsafe.h>
#include <TraceLoggingActivity.h>
#include <wil\resource.h>
#include <wil\result.h>
#include <winrt/windows.foundation.h>
#include <psapi.h>
#include <shared_mutex>
#include <functional>
#include <unordered_set>
#include <ShObjIdl.h>
#include <optional>
