#pragma once

#ifndef PCH_H
#define PCH_H

#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <Unknwn.h>
#include <d3d11.h>
#include <dcommon.h>

#include <cinttypes>
#include <cassert>
#include <limits>
#include <chrono>
#include <algorithm>
#include <cmath>
#include <string>
#include <vector>
#include <sstream>
#include <iomanip>
#include <iostream>
#include <functional>
#include <thread>
#include <string_view>

// C++/WinRT base types (winrt::com_ptr used in BGRATextureView.h MappedTextureView)
#include <winrt/base.h>

#endif // PCH_H
