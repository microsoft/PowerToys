#pragma once

#define WIN32_LEAN_AND_MEAN
<<<<<<< HEAD
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

=======

#include <windows.h>
#include <shellapi.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Management.Deployment.h>
#include <Shlwapi.h>
#include <comdef.h>
#include <atlbase.h>
#include <comdef.h>
>>>>>>> 2978ce163a (dev)
#include <string>
#include <vector>
#include <optional>
#include <filesystem>
<<<<<<< HEAD
#include <regex>
#include <exception>
#include <functional>

=======
#include <algorithm>
#include <regex>
#include <fstream>
#include <wrl/client.h>
#include <wil/result.h>
#include <wil/com.h>
#include <wil/resource.h>
#include <optional>
#include <common/logger/logger.h>
#include <common/version/version.h>
#include <exception>
#include <functional>
>>>>>>> 2978ce163a (dev)
#include <common/logger/logger.h>
#include <common/version/version.h>
