#pragma once
#include "pch.h"
#include <common/utils/OnThreadExecutor.h>
#include <atlfile.h>
#include <string>
#include <atomic>
#include <memory>
#include <functional>
#include <optional>

class AdvancedPasteProcessManager
{
public:
    AdvancedPasteProcessManager() = default;
    AdvancedPasteProcessManager(const AdvancedPasteProcessManager&) = delete;
    AdvancedPasteProcessManager& operator=(const AdvancedPasteProcessManager&) = delete;

    void start();
    void stop();
    void send_message(const std::wstring& message_type, const std::wstring& message_arg = L"");
    void bring_to_front();

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