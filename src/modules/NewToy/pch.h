#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <common/common.h>
#include <ProjectTelemetry.h>

#include "resource.h"
#include <Unknwn.h>
#include <winrt/base.h>
#include <windowsx.h>
#include <dwmapi.h>
#include <ProjectTelemetry.h>
#include <shellapi.h>
#include <strsafe.h>
#include <TraceLoggingActivity.h>
#include <windows.foundation.h>
#include <psapi.h>

#include "trace.h"
#include "common/common.h"

#pragma comment(lib, "windowsapp")

namespace winrt
{
    using namespace ::winrt;
}