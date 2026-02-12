// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include "pch.h"
#include <common/utils/OnThreadExecutor.h>
#include <atlfile.h>
#include <string>
#include <atomic>
#include <memory>
#include <functional>

/// <summary>
/// Manages the PowerDisplay.exe process and Named Pipe communication.
/// Based on AdvancedPasteProcessManager pattern.
/// </summary>
class PowerDisplayProcessManager
{
public:
    PowerDisplayProcessManager() = default;
    PowerDisplayProcessManager(const PowerDisplayProcessManager&) = delete;
    PowerDisplayProcessManager& operator=(const PowerDisplayProcessManager&) = delete;

    /// <summary>
    /// Enable the module - starts the PowerDisplay.exe process.
    /// </summary>
    void start();

    /// <summary>
    /// Disable the module - terminates the PowerDisplay.exe process.
    /// </summary>
    void stop();

    /// <summary>
    /// Send a message to PowerDisplay.exe via Named Pipe.
    /// </summary>
    /// <param name="message_type">The message type (e.g., "Toggle", "ApplyProfile")</param>
    /// <param name="message_arg">Optional message argument</param>
    void send_message(const std::wstring& message_type, const std::wstring& message_arg = L"");

    /// <summary>
    /// Bring the PowerDisplay window to the foreground.
    /// </summary>
    void bring_to_front();

    /// <summary>
    /// Check if PowerDisplay.exe process is running.
    /// </summary>
    bool is_running() const;

private:
    void submit_task(std::function<void()> task);
    bool is_process_running() const;
    void terminate_process();
    HRESULT start_process(const std::wstring& pipe_name);
    HRESULT start_named_pipe_server(const std::wstring& pipe_name);
    void refresh();
    void send_named_pipe_message(const std::wstring& message_type, const std::wstring& message_arg = L"");

    OnThreadExecutor m_thread_executor; // all internal operations are done on background thread with task queue
    std::atomic<bool> m_enabled = false; // written on main thread, read on background thread
    HANDLE m_hProcess = 0;
    std::unique_ptr<CAtlFile> m_write_pipe;
};
