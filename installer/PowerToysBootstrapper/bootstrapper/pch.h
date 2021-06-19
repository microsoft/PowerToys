#pragma once

#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <shellapi.h>
#include <commctrl.h>

#include <charconv>
#include <string_view>
#include <optional>
#include <fstream>
#include <wil/resource.h>
#include <Msi.h>

#include <unordered_set>
#include <thread>
#include <tuple>
#include <sstream>

#include <spdlog/spdlog.h>
#include <spdlog/sinks/basic_file_sink.h>
#include <spdlog/sinks/null_sink.h>
#include <spdlog/sinks/stdout_color_sinks.h>

#pragma warning(push, 0)
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#pragma warning(pop)

#include <cxxopts.hpp>
