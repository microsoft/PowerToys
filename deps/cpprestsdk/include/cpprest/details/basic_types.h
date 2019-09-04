/***
 * Copyright (C) Microsoft. All rights reserved.
 * Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.
 *
 * =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
 *
 * Platform-dependent type definitions
 *
 * For the latest on this and related APIs, please see: https://github.com/Microsoft/cpprestsdk
 *
 * =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
 ****/

#pragma once

#include "cpprest/details/cpprest_compat.h"
#include <fstream>
#include <iostream>
#include <sstream>
#include <string>

#ifndef _WIN32
#ifndef __STDC_LIMIT_MACROS
#define __STDC_LIMIT_MACROS
#endif
#include <stdint.h>
#else
#include <cstdint>
#endif

#include "cpprest/details/SafeInt3.hpp"

namespace utility
{
#ifdef _WIN32
#define _UTF16_STRINGS
#endif

// We should be using a 64-bit size type for most situations that do
// not involve specifying the size of a memory allocation or buffer.
typedef uint64_t size64_t;

#ifndef _WIN32
typedef uint32_t HRESULT; // Needed for PPLX
#endif

#ifdef _UTF16_STRINGS
//
// On Windows, all strings are wide
//
typedef wchar_t char_t;
typedef std::wstring string_t;
#define _XPLATSTR(x) L##x
typedef std::wostringstream ostringstream_t;
typedef std::wofstream ofstream_t;
typedef std::wostream ostream_t;
typedef std::wistream istream_t;
typedef std::wifstream ifstream_t;
typedef std::wistringstream istringstream_t;
typedef std::wstringstream stringstream_t;
#define ucout std::wcout
#define ucin std::wcin
#define ucerr std::wcerr
#else
//
// On POSIX platforms, all strings are narrow
//
typedef char char_t;
typedef std::string string_t;
#define _XPLATSTR(x) x
typedef std::ostringstream ostringstream_t;
typedef std::ofstream ofstream_t;
typedef std::ostream ostream_t;
typedef std::istream istream_t;
typedef std::ifstream ifstream_t;
typedef std::istringstream istringstream_t;
typedef std::stringstream stringstream_t;
#define ucout std::cout
#define ucin std::cin
#define ucerr std::cerr
#endif // endif _UTF16_STRINGS

#ifndef _TURN_OFF_PLATFORM_STRING
// The 'U' macro can be used to create a string or character literal of the platform type, i.e. utility::char_t.
// If you are using a library causing conflicts with 'U' macro, it can be turned off by defining the macro
// '_TURN_OFF_PLATFORM_STRING' before including the C++ REST SDK header files, and e.g. use '_XPLATSTR' instead.
#define U(x) _XPLATSTR(x)
#endif // !_TURN_OFF_PLATFORM_STRING

} // namespace utility

typedef char utf8char;
typedef std::string utf8string;
typedef std::stringstream utf8stringstream;
typedef std::ostringstream utf8ostringstream;
typedef std::ostream utf8ostream;
typedef std::istream utf8istream;
typedef std::istringstream utf8istringstream;

#ifdef _UTF16_STRINGS
typedef wchar_t utf16char;
typedef std::wstring utf16string;
typedef std::wstringstream utf16stringstream;
typedef std::wostringstream utf16ostringstream;
typedef std::wostream utf16ostream;
typedef std::wistream utf16istream;
typedef std::wistringstream utf16istringstream;
#else
typedef char16_t utf16char;
typedef std::u16string utf16string;
typedef std::basic_stringstream<utf16char> utf16stringstream;
typedef std::basic_ostringstream<utf16char> utf16ostringstream;
typedef std::basic_ostream<utf16char> utf16ostream;
typedef std::basic_istream<utf16char> utf16istream;
typedef std::basic_istringstream<utf16char> utf16istringstream;
#endif

#if defined(_WIN32)
// Include on everything except Windows Desktop ARM, unless explicitly excluded.
#if !defined(CPPREST_EXCLUDE_WEBSOCKETS)
#if defined(WINAPI_FAMILY)
#if WINAPI_FAMILY_PARTITION(WINAPI_PARTITION_DESKTOP) && defined(_M_ARM)
#define CPPREST_EXCLUDE_WEBSOCKETS
#endif
#else
#if defined(_M_ARM)
#define CPPREST_EXCLUDE_WEBSOCKETS
#endif
#endif
#endif
#endif
