#pragma once

#define WIN32_LEAN_AND_MEAN

#include <Windows.h>
#include <shellapi.h>
#include <sddl.h>
#include <shldisp.h>
#include <shlobj.h>
#include <Shlwapi.h>
#include <exdisp.h>
#include <atlbase.h>
#include <comdef.h>
#include <appxpackaging.h>

#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Management.Deployment.h>
#include <wrl/client.h>

#include <string>
#include <vector>
#include <optional>
#include <filesystem>
#include <algorithm>
#include <regex>
#include <fstream>
#include <exception>
#include <functional>

#include <wil/result.h>
#include <wil/com.h>
#include <wil/resource.h>

#include <common/logger/logger.h>
#include <common/version/version.h>
