#pragma once
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <Unknwn.h>

#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>

#include <wil/resource.h>
#include <wil/com.h>
#include <wil/filesystem.h>

#include <string_view>
#include <optional>
#include <iostream>
#include <fstream>
#include <vector>
#include <functional>
#include <algorithm>

#include <Shobjidl.h>
#include <Shlwapi.h>
