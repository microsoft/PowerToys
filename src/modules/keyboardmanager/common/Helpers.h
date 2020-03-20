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

template<typename T>
hstring convertVectorToHstring(std::vector<T>& input)
{
    hstring output;
    for (int i = 0; i < input.size(); i++)
    {
        output = output + to_hstring((unsigned int)input[i]) + to_hstring(L" ");
    }
    return output;
}

template<typename T>
std::vector<T> convertWStringVectorToNumberType(std::vector<std::wstring> input)
{
    std::vector<T> typeVector;
    for (int i = 0; i < input.size(); i++)
    {
        typeVector.push_back((T)std::stoi(input[i]));
    }

    return typeVector;
}

