#pragma once

#ifndef PCH_H
#define PCH_H

// Mirror the runner's pch.h includes so that when hotkey_conflict_detector.h
// includes the runner's pch.h, all headers are already present (#pragma once).
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <Windows.h>
#include <dxgi1_3.h>
#include <d3d11_2.h>
#include <d2d1_3.h>
#include <d2d1_3helper.h>
#include <d2d1helper.h>
#include <dwrite.h>
#include <dcomp.h>
#include <dwmapi.h>
#include <Shobjidl.h>
#include <Shlwapi.h>
#include <string>
#include <algorithm>
#include <chrono>
#include <mutex>
#include <thread>
#include <functional>
#include <condition_variable>
#include <stdexcept>
#include <tuple>
#include <unordered_set>
#include <unordered_map>
#include <set>

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Networking.Connectivity.h>
#include <winrt/Windows.Storage.h>
#include <winrt/Windows.Data.Json.h>

#include <wil/resource.h>
#include <wil/coroutine.h>

#include <optional>
#include <fstream>
#include <compare>
#include <cwchar>
#include <vector>
#include <Shlobj.h>

#endif // PCH_H
