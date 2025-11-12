// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <functional>
#include <memory>
#include <thread>
#include <atlfile.h>
#include <common/utils/OnThreadExecutor.h>

/// <summary>
/// Manages PowerDisplay.exe process lifecycle and IPC communication
/// </summary>
class PowerDisplayProcessManager
{
private:
    HANDLE m_hProcess = nullptr;
    std::unique_ptr<CAtlFile> m_write_pipe;
    OnThreadExecutor m_thread_executor;
    bool m_enabled = false;

public:
    PowerDisplayProcessManager() = default;
    ~PowerDisplayProcessManager();

    /// <summary>
    /// Start PowerDisplay.exe process
    /// </summary>
    void start();

    /// <summary>
    /// Stop PowerDisplay.exe process
    /// </summary>
    void stop();

    /// <summary>
    /// Send message to PowerDisplay.exe
    /// </summary>
    void send_message_to_powerdisplay(const std::wstring& message);

private:
    /// <summary>
    /// Submit task to thread executor
    /// </summary>
    void submit_task(std::function<void()> task);

    /// <summary>
    /// Check if PowerDisplay.exe is running
    /// </summary>
    bool is_process_running() const;

    /// <summary>
    /// Terminate PowerDisplay.exe process
    /// </summary>
    void terminate_process();

    /// <summary>
    /// Start PowerDisplay.exe with command line arguments
    /// </summary>
    HRESULT start_process(const std::wstring& pipe_uuid);

    /// <summary>
    /// Create named pipe for sending commands to PowerDisplay
    /// </summary>
    HRESULT start_command_pipe(const std::wstring& pipe_uuid);

    /// <summary>
    /// Refresh - start or stop process based on m_enabled state
    /// </summary>
    void refresh();
};
