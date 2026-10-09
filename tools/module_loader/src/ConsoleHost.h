// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <Windows.h>
#include "ModuleLoader.h"
#include "HotkeyManager.h"

/// <summary>
/// Console host that runs the message loop and handles Ctrl+C
/// </summary>
class ConsoleHost
{
public:
    ConsoleHost(ModuleLoader& moduleLoader, HotkeyManager& hotkeyManager);
    ~ConsoleHost();

    // Prevent copying
    ConsoleHost(const ConsoleHost&) = delete;
    ConsoleHost& operator=(const ConsoleHost&) = delete;

    /// <summary>
    /// Run the message loop until Ctrl+C is pressed
    /// </summary>
    void Run();

private:
    ModuleLoader& m_moduleLoader;
    HotkeyManager& m_hotkeyManager;
    static bool s_exitRequested;

    /// <summary>
    /// Console control handler (for Ctrl+C)
    /// </summary>
    static BOOL WINAPI ConsoleCtrlHandler(DWORD ctrlType);
};
