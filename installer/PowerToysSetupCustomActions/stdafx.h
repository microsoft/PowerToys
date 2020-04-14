#pragma once

#define WIN32_LEAN_AND_MEAN // Exclude rarely-used stuff from Windows headers
// Windows Header Files:
#include <windows.h>
#include <strsafe.h>
#include <msiquery.h>
#include <Msi.h>

// WiX Header Files:
#include <wcautil.h>

#define SECURITY_WIN32
#include <Security.h>
#include <Lmcons.h>

#include <comdef.h>
#include <taskschd.h>
#include <iostream>
#include <strutil.h>
#include <string>
#include <optional>
#include <pathcch.h>
