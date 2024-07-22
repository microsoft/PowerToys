#pragma once

#include "targetver.h"

#define WIN32_LEAN_AND_MEAN // Exclude rarely-used stuff from Windows headers
#define NOMINMAX
// Windows Header Files:
#include <windows.h>

#include <algorithm>
#include <execution>
#include <cstdlib>
#include <malloc.h>
#include <memory.h>
#include <cwchar>
#include <atlbase.h>
#include <strsafe.h>
#include <pathcch.h>
#include <shobjidl.h>
#include <shellapi.h>
#include <shlwapi.h>
#include <ShlObj_core.h>
#include <filesystem>
#include <compare>
#include <regex>
#include <vector>
#include <variant>
#include <charconv>
#include <string>
#include <random>

#include <ProjectTelemetry.h>

#include <winrt/base.h>
