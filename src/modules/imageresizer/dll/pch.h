#pragma once

#ifndef STRICT
#define STRICT
#endif

#define _ATL_APARTMENT_THREADED
#define _ATL_NO_AUTOMATIC_NAMESPACE
#define _ATL_CSTRING_EXPLICIT_CONSTRUCTORS
#define ATL_NO_ASSERT_ON_DESTROY_NONEXISTENT_WINDOW

#include "targetver.h"
#include "Generated Files/resource.h"

#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <atlbase.h>
#include <atlcom.h>
#include <atlfile.h>
#include <atlstr.h>
#include <windows.h>

#include <ShlObj.h>
#include <ProjectTelemetry.h>
