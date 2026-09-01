#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <strsafe.h>
#include <thread>

#include <d2d1_3.h>
#include <d3d11_4.h>
#include <dcomp.h>
#include <dxgi1_3.h>
#include <shellscalingapi.h>

// Raw input from the pen digitizer. The pen has to be read below the pointer stack:
// once an app consumes pointer input (any scrollable surface does), Windows stops
// promoting pen to mouse messages and a low level mouse hook goes blind.
// hidsdi.h is the documented user-space entry point: it defines NTSTATUS and then pulls
// in hidusage.h and hidpi.h in the order they expect. Including hidpi.h directly fails
// to compile, because its inline helpers return NTSTATUS before anything defines it.
#include <hidsdi.h>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.h>

#include <common/SettingsAPI/settings_helpers.h>
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
