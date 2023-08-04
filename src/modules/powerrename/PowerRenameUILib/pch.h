#pragma once

#define NOMINMAX

#include <windows.h>
#include <unknwn.h>
#include <restrictederrorinfo.h>
#include <hstring.h>
#include <unknwn.h>

// Undefine GetCurrentTime macro to prevent
// conflict with Storyboard::GetCurrentTime
#undef GetCurrentTime

#include <winrt/Windows.ApplicationModel.Activation.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.System.h>

// disable warning 26493 - Don't use C-style casts
#pragma warning(push)
#pragma warning(disable: 26493)
#include <winrt/Microsoft.UI.Composition.h>
#pragma warning(pop)

#include <winrt/Microsoft.UI.Xaml.h>
#include <winrt/Microsoft.UI.Xaml.Automation.Peers.h>
#include <winrt/Microsoft.UI.Xaml.Controls.h>
#include <winrt/Microsoft.UI.Xaml.Controls.AnimatedVisuals.h>
#include <winrt/Microsoft.UI.Xaml.Controls.Primitives.h>
#include <winrt/Microsoft.UI.Xaml.Data.h>
#include <winrt/Microsoft.UI.Xaml.Documents.h>
#include <winrt/Microsoft.UI.Xaml.Interop.h>
#include <winrt/Microsoft.UI.Xaml.Markup.h>
#include <winrt/Microsoft.UI.Xaml.Media.h>
#include <winrt/Microsoft.UI.Xaml.Navigation.h>
#include <winrt/Microsoft.UI.Xaml.Shapes.h>
#include <winrt/Microsoft.UI.Dispatching.h>
#include <winrt/Microsoft.Windows.ApplicationModel.Resources.h>
#include <wil/cppwinrt_helpers.h>
