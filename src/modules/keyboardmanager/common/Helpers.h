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

// Function to split a wstring based on a delimiter and return a vector of split strings
std::vector<std::wstring> splitwstring(const std::wstring& input, wchar_t delimiter);

// Function to return the next sibling element for an element under a stack panel
IInspectable getSiblingElement(IInspectable const& element);

// Function to convert an unsigned int vector to hstring by concatenating them
template<typename T>
hstring convertVectorToHstring(const std::vector<T>& input)
{
    hstring output;
    for (int i = 0; i < input.size(); i++)
    {
        output = output + to_hstring((unsigned int)input[i]) + to_hstring(L" ");
    }
    return output;
}

// Function to convert a wstring vector to a integer vector
template<typename T>
std::vector<T> convertWStringVectorToIntegerVector(const std::vector<std::wstring>& input)
{
    std::vector<T> typeVector;
    for (int i = 0; i < input.size(); i++)
    {
        typeVector.push_back((T)std::stoi(input[i]));
    }

    return typeVector;
}

