//============================================================================
//
// Zoomit
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
// Private window messages handled by the ZoomIt main window.
//
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//============================================================================
#pragma once

#define WM_USER_TRAY_ACTIVATE	(WM_USER+100)
#define WM_USER_TYPING_OFF		(WM_USER+101)
#define WM_USER_GET_ZOOM_LEVEL	(WM_USER+102)
#define WM_USER_GET_SOURCE_RECT	(WM_USER+103)
#define WM_USER_SET_ZOOM		(WM_USER+104)
#define WM_USER_STOP_RECORDING	(WM_USER+105)
#define WM_USER_SAVE_CURSOR		(WM_USER+106)
#define WM_USER_RESTORE_CURSOR	(WM_USER+107)
#define WM_USER_MAGNIFY_CURSOR	(WM_USER+108)
#define WM_USER_EXIT_MODE		(WM_USER+109)
#define WM_USER_RELOAD_SETTINGS	(WM_USER+110)
#define WM_USER_RECORDING_STARTED (WM_USER+111)
#define WM_USER_RECORDING_NO_FRAMES (WM_USER+112)
#define WM_USER_MIRROR_STOP		(WM_USER+113)
#define WM_USER_RECORDING_AUDIO_UNAVAILABLE (WM_USER+114)
