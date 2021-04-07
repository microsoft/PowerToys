#pragma once

#include "targetver.h"

// #define WIN32_LEAN_AND_MEAN // Exclude rarely-used stuff from Windows headers
#include <unknwn.h>
#include <windows.h>
#include <cstdlib>
#include <cstring>
#include <thread>
#include <string>
#include <mutex>

#include <winrt/base.h>
#include <winrt/Windows.system.h>

#include <windows.ui.xaml.hosting.desktopwindowxamlsource.h>

#pragma push_macro("GetCurrentTime")
#undef GetCurrentTime
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Automation.h>
#include <winrt/windows.ui.xaml.controls.h>
#include <winrt/Windows.UI.Xaml.Controls.Primitives.h>
#include <winrt/Windows.UI.Xaml.Hosting.h>
#include <winrt/Windows.UI.Xaml.Interop.h>
#include <winrt/Windows.ui.xaml.media.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Numerics.h>
#include <winrt/Windows.UI.Text.h>
#include <winrt/Windows.UI.Core.h>
#pragma pop_macro("GetCurrentTime")

#include <common/logger/logger.h>

using namespace winrt;
using namespace Windows::UI;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Xaml::Hosting;
using namespace Windows::Foundation::Numerics;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;