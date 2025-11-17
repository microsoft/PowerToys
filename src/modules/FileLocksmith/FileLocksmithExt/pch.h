#pragma once

// add headers that you want to pre-compile here
#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files
#include <Windows.h>
#include <Shlwapi.h>
#include <ShlObj_core.h>
#include <atlbase.h>
#include <commctrl.h>

// C++ Standard library
#include <atomic>
#include <string>
#include <filesystem>
#include <fstream>
