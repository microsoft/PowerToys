// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.
#pragma once
#define WIN32_LEAN_AND_MEAN
// add headers that you want to pre-compile here
#include <Unknwn.h>
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <Windows.h>
#include <Endpointvolume.h>
#include <vector>
#include <UIAutomation.h>
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
#include <filesystem>
#include <common/utils/excluded_apps.h>
#include <common/utils/process_path.h>
#include <../SettingsAPI/settings_objects.h>
