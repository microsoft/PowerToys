// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "ConsoleHost.h"
#include <iostream>

bool ConsoleHost::s_exitRequested = false;

ConsoleHost::ConsoleHost(ModuleLoader& moduleLoader, HotkeyManager& hotkeyManager)
    : m_moduleLoader(moduleLoader)
    , m_hotkeyManager(hotkeyManager)
{
}

ConsoleHost::~ConsoleHost()
{
}

BOOL WINAPI ConsoleHost::ConsoleCtrlHandler(DWORD ctrlType)
{
    switch (ctrlType)
    {
    case CTRL_C_EVENT:
    case CTRL_BREAK_EVENT:
    case CTRL_CLOSE_EVENT:
        std::wcout << L"\nCtrl+C received, shutting down...\n";
        s_exitRequested = true;
        
        // Post a quit message to break the message loop
        PostQuitMessage(0);
        return TRUE;

    default:
        return FALSE;
    }
}

void ConsoleHost::Run()
{
    // Install console control handler
    if (!SetConsoleCtrlHandler(ConsoleCtrlHandler, TRUE))
    {
        std::wcerr << L"Warning: Failed to set console control handler\n";
    }

    s_exitRequested = false;

    // Message loop
    MSG msg;
    while (!s_exitRequested)
    {
        // Wait for a message with a timeout so we can check s_exitRequested
        DWORD result = MsgWaitForMultipleObjects(0, nullptr, FALSE, 100, QS_ALLINPUT);

        if (result == WAIT_OBJECT_0)
        {
            // Process all pending messages
            while (PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE))
            {
                if (msg.message == WM_QUIT)
                {
                    s_exitRequested = true;
                    break;
                }

                if (msg.message == WM_HOTKEY)
                {
                    m_hotkeyManager.HandleHotkey(static_cast<int>(msg.wParam), m_moduleLoader);
                }

                TranslateMessage(&msg);
                DispatchMessage(&msg);
            }
        }
    }

    // Remove console control handler
    SetConsoleCtrlHandler(ConsoleCtrlHandler, FALSE);
}
