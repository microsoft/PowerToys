// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
//
// pch.h
// Header for platform projection include files
//

#pragma once

#define NOMCX
#define NOHELP
#define NOCOMM

// Manually include til after we include Windows.Foundation to give it winrt superpowers
#define BLOCK_TIL

///////////////////////////////////////////////////////////////////////////////////
// Below is the contents of Terminal's LibraryIncludes.h

// C
#include <climits>
#include <cwchar>
#include <cwctype>

// STL

// Block minwindef.h min/max macros to prevent <algorithm> conflict
#define NOMINMAX
// Exclude rarely-used stuff from Windows headers
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include <algorithm>
#include <atomic>
#include <cmath>
#include <deque>
#include <filesystem>
#include <fstream>
#include <functional>
#include <iterator>
#include <list>
#include <map>
#include <memory_resource>
#include <memory>
#include <mutex>
#include <new>
#include <numeric>
#include <optional>
#include <queue>
#include <regex>
#include <set>
#include <shared_mutex>
#include <span>
#include <stdexcept>
#include <string_view>
#include <string>
#include <thread>
#include <tuple>
#include <unordered_map>
#include <unordered_set>
#include <utility>
#include <vector>

// WIL
#include <wil/com.h>
#include <wil/stl.h>
#include <wil/filesystem.h>
// Due to the use of RESOURCE_SUPPRESS_STL in result.h, we need to include resource.h first, which happens
// implicitly through the includes above. If RESOURCE_SUPPRESS_STL is gone, the order doesn't matter anymore.
#include <wil/result.h>
#include <wil/nt_result_macros.h>

#define BASIC_FACTORY(typeName)                                       \
    struct typeName : typeName##T<typeName, implementation::typeName> \
    {                                                                 \
    };

//////////////////////////////////////////////////////////////////////////////////////

// This is inexplicable, but for whatever reason, cppwinrt conflicts with the
//      SDK definition of this function, so the only fix is to undef it.
// from WinBase.h
// Windows::UI::Xaml::Media::Animation::IStoryboard::GetCurrentTime
#ifdef GetCurrentTime
#undef GetCurrentTime
#endif

#include <wil/cppwinrt.h>

#include <winrt/Windows.ApplicationModel.Resources.h>
#include <winrt/Windows.Foundation.h>

#include <winrt/Windows.Graphics.Imaging.h>
#include <Windows.Graphics.Imaging.Interop.h>

#include <winrt/Microsoft.UI.Text.h>
#include <winrt/Microsoft.UI.Xaml.Controls.h>
// #include <winrt/Microsoft.UI.Xaml.Markup.h>
#include <winrt/Microsoft.UI.Xaml.Media.h>
#include <winrt/Microsoft.UI.Xaml.Media.Imaging.h>

// #include <winrt/Microsoft.UI.Xaml.Controls.h>

// Manually include til after we include Windows.Foundation to give it winrt superpowers
//#include "til.h"
#define _TIL_INLINEPREFIX __declspec(noinline) inline
#include "til_string.h"

// #include <cppwinrt_utils.h>
#include <wil/cppwinrt_helpers.h> // must go after the CoreDispatcher type is defined
