#pragma once

#define COMPOSITION
#define WIN32_LEAN_AND_MEAN // Exclude rarely-used stuff from Windows headers
#include <windows.h>
#include <strsafe.h>
#include <hIdUsage.h>

#ifdef COMPOSITION
#include <microsoft.ui.composition.interop.h>
#include <DispatcherQueue.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Microsoft.UI.Composition.h>
#include <winrt/Microsoft.UI.h>
#endif

#include <winrt/Windows.Foundation.Collections.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/logger/logger.h>
