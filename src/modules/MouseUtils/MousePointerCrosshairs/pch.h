#pragma once

#define COMPOSITION
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <windows.ui.composition.interop.h>
#include <DispatcherQueue.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.Composition.Desktop.h>
#include <ProjectTelemetry.h>
#include <thread>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
