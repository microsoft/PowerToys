#pragma once

#include "targetver.h"

#define WIN32_LEAN_AND_MEAN // Exclude rarely-used stuff from Windows headers
#include <unknwn.h>
#include <windows.h>
#include <shellapi.h>

#include <windows.ui.xaml.hosting.desktopwindowxamlsource.h>

#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.Metadata.h>
#include <winrt/Windows.UI.Core.h>
#include <winrt/Windows.UI.Text.h>
#include <winrt/Windows.ApplicationModel.Core.h>

#pragma push_macro("GetCurrentTime")
#undef GetCurrentTime
#include <winrt/Windows.UI.Xaml.Automation.h>
#include <winrt/Windows.UI.Xaml.Automation.Peers.h>
#include <winrt/Windows.UI.Xaml.Controls.Primitives.h>
#include <winrt/Windows.UI.Xaml.Hosting.h>
#include <winrt/Windows.UI.Xaml.Interop.h>
#include <winrt/Windows.ui.xaml.media.h>
#include <winrt/Microsoft.UI.Xaml.Controls.h>
#pragma pop_macro("GetCurrentTime")

#include <common/logger/logger.h>
#include <common/utils/resources.h>

#include <ProjectTelemetry.h>

#include <keyboardmanager/KeyboardManagerEditor/Generated Files/resource.h>
//#include <Generated Files/resource.h>

using namespace winrt;
using namespace Windows::UI;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Xaml::Hosting;
using namespace Windows::Foundation::Numerics;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;
namespace muxc = Microsoft::UI::Xaml::Controls;