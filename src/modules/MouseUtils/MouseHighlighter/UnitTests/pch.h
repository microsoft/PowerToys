#pragma once

#ifndef PCH_H
#define PCH_H

#define COMPOSITION
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <strsafe.h>
#include <hIdUsage.h>
#include <thread>

#ifdef COMPOSITION
#include <windows.ui.composition.interop.h>
#include <DispatcherQueue.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.Composition.Desktop.h>
#include <winrt/Windows.Foundation.Collections.h>
#endif

#include <winrt/Windows.UI.h>

#endif // PCH_H
