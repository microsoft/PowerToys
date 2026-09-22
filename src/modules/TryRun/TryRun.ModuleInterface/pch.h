// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
// Match existing module PCH ordering: ShellExecute's Unicode macro must be in
// place before a transitive include declares IShellDispatch2.
#include <shellapi.h>
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <wil/resource.h>

#include <atomic>
#include <condition_variable>
#include <mutex>
#include <optional>
#include <string>
#include <thread>
