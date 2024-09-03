//============================================================================
//
// Zoomit
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
// DemoType allows the presenter to synthesize keystrokes from a script
//
//============================================================================

#pragma once

#define MAX_INPUT_SIZE      1048576 // 1 MiB

#define MAX_TYPING_SPEED    10      // ms
#define MIN_TYPING_SPEED    100     // ms

#define ERROR_LOADING_FILE  1
#define NO_FILE_SPECIFIED   2
#define FILE_SIZE_OVERFLOW  3
#define UNKNOWN_FILE_DATA   4

void    ResetDemoTypeIndex();
int     StartDemoType( const TCHAR* filePath, const DWORD speedSlider, const BOOLEAN userDriven );