// Precompiled header file.

#pragma once

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#define NOMCX
#define NOHELP
#define NOCOMM

// Windows and STL
#include <Shobjidl.h>
#include <shlwapi.h>
#include <shellapi.h>
#include <Windows.h>
#include <shlobj.h>
#include <vector>
#include <system_error>
#include <memory>
#include <iostream>
#include <atlbase.h>
#include <wrl.h>
#include <wrl/module.h>
#include <wrl/client.h>
#include <Unknwn.h>
using namespace Microsoft::WRL;

// PowerToys project common
#include <ProjectTelemetry.h>
#include <common/utils/resources.h>
#include <common/logger/logger.h>
#include <common/logger/logger_settings.h>
#include <common/utils/logger_helper.h>
#include <common/Themes/theme_helpers.h>

// New project specific
#include "dll_main.h"
#include "template_folder.h"
#include "settings.h"
#include "new_utilities.h"
