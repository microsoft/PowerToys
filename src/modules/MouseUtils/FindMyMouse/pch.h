#pragma once

#define COMPOSITION
#define WIN32_LEAN_AND_MEAN // Exclude rarely-used stuff from Windows headers
#include <windows.h>
#include <strsafe.h>
#include <hIdUsage.h>
// Required for IUnknown and DECLARE_INTERFACE_* used by interop headers
#include <Unknwn.h>

#ifdef COMPOSITION
#include <DispatcherQueue.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Microsoft.UI.Composition.h>
#include <winrt/Microsoft.UI.Composition.Effects.h>
#include <winrt/Microsoft.UI.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.Graphics.Effects.h>
// Win2D C++/WinRT projections
#include <winrt/Microsoft.Graphics.Canvas.h>
#include <winrt/Microsoft.Graphics.Canvas.Effects.h>
#endif

#include <winrt/Windows.Foundation.Collections.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/logger/logger.h>

#ifdef GetCurrentTime
#undef GetCurrentTime
#endif
