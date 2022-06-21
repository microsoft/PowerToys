#pragma once

#define COMPOSITION
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <strsafe.h>
#include <hIdUsage.h>
#include <shellapi.h>

#ifdef COMPOSITION
#include <windows.ui.composition.interop.h>
#include <DispatcherQueue.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.Composition.Desktop.h>
#endif

#include <thread>

#include <winrt/Windows.Foundation.Collections.h>
#include <ProjectTelemetry.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/logger/logger.h>
