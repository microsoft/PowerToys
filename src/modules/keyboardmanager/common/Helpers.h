#pragma once
#include <windows.h>
#include <stdlib.h>
#include <sstream>
#include <string>
#include <vector>
#include <winrt/Windows.system.h>
#include <winrt/windows.ui.xaml.hosting.h>
#include <windows.ui.xaml.hosting.desktopwindowxamlsource.h>
#include <winrt/windows.ui.xaml.controls.h>
#include <winrt/Windows.ui.xaml.media.h>
#include <winrt/Windows.Foundation.Collections.h>
#include "winrt/Windows.Foundation.h"
#include "winrt/Windows.Foundation.Numerics.h"
#include "winrt/Windows.UI.Xaml.Controls.Primitives.h"
#include "winrt/Windows.UI.Text.h"
#include "winrt/Windows.UI.Core.h"

using namespace winrt;
using namespace Windows::UI;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Xaml::Hosting;
using namespace Windows::Foundation::Numerics;
using namespace Windows::Foundation;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;

std::vector<std::wstring> splitwstring(std::wstring input, wchar_t delimiter);
IInspectable getSiblingElement(IInspectable const& element);
