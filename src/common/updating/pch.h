#pragma once

#ifndef PCH_H
#define PCH_H

#pragma warning(push)
#pragma warning(disable : 5205)
#include <winrt/base.h>
#pragma warning(pop)
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <Windows.h>
#include <MsiQuery.h>
#include <Shlwapi.h>
#include <Shobjidl.h>
#include <Knownfolders.h>
#include <ShlObj_core.h>
#include <shellapi.h>
#include <filesystem>
#include <msi.h>
#include <PathCch.h>

#include <optional>
#include <regex>
#include <charconv>

#include <expected.hpp>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.System.h>

#include <wil/resource.h>

#endif //PCH_H

namespace fs = std::filesystem;
