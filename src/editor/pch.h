#pragma once
#include <Windows.h>
#pragma push_macro("GetCurrentTime")
#undef GetCurrentTime
// include winrt headers with fix for "warning C4002: Too many arguments for function-like macro invocation GetCurrentTime"
#include <winrt\windows.system.h>
#include <winrt\windows.web.ui.h>
#include <winrt\windows.web.ui.interop.h>
#include <winrt\windows.ui.xaml.controls.h>
#include <winrt\windows.foundation.h>
#include <winrt\windows.web.http.h>
#include <winrt\windows.web.http.headers.h>
#include <winrt\Windows.Storage.h>
#include <winrt\Windows.Storage.Streams.h>
#pragma pop_macro("GetCurrentTime")
#include <strsafe.h>
#include <Shlwapi.h>
