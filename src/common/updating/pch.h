#pragma once

#ifndef PCH_H
#define PCH_H

#pragma warning(disable : 5205)
#include <winrt/base.h>
#pragma warning(default : 5205)
#define WIN32_LEAN_AND_MEAN
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

#include <expected.hpp>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Networking.Connectivity.h>
#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Management.Deployment.h>

#endif //PCH_H

