#pragma once

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <Windows.h>
#include <shellapi.h>
#include <shlobj.h>
#include <Shlwapi.h>
#include <sddl.h>
#include <DbgHelp.h>
#include <Msi.h>
#include <pathcch.h>
#include <atlbase.h>
#include <atlstr.h>

#include <algorithm>
#include <cassert>
#include <exception>
#include <filesystem>
#include <functional>
#include <memory>
#include <optional>
#include <regex>
#include <sstream>
#include <string>
#include <variant>
#include <vector>

#pragma warning(push)
#pragma warning(disable : 26471 26492 26493 26497)
#include <wil/resource.h>
#pragma warning(pop)

#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
