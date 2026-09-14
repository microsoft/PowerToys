// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <windows.h>
#include <atomic>
#include <functional>
#include <mutex>
#include <string>
#include <thread>
#include <wil/resource.h>
#include <common/interop/pipe_caller_auth.h>

enum class LauncherIpcFailure
{
    Disconnected,
    InvalidMessage,
    Timeout,
    Authentication,
};

class LauncherIpcServer
{
public:
    static constexpr DWORD MaxMessageBytes = 4 * 1024 * 1024;

    LauncherIpcServer(std::function<void(const std::wstring&)> receive,
                      std::function<void(LauncherIpcFailure)> failed);
    ~LauncherIpcServer();

    bool Initialize();
    bool Start(HANDLE peer, const interop_auth::CallerPolicy& policy);
    bool Send(const std::wstring& message);
    bool IsConnected() const noexcept;
    // Safe inside a receive callback; Stop separately owns joining the reader.
    void Disconnect();
    void Stop();
    const std::wstring& Name() const noexcept;

private:
    std::function<void(const std::wstring&)> m_receive;
    std::function<void(LauncherIpcFailure)> m_failed;
    std::wstring m_name;
    wil::unique_hfile m_pipe;
    wil::unique_handle m_peer;
    wil::unique_event m_closed;
    std::thread m_reader;
    std::timed_mutex m_writeMutex;
    std::atomic<bool> m_connected{};
    std::atomic<bool> m_stopping{};
    std::atomic<bool> m_failureReported{};
    interop_auth::CallerPolicy m_policy;
    interop_auth::VerificationCache m_authCache;

    void ReadLoop();
    bool FinishIo(OVERLAPPED& operation, DWORD timeout, DWORD& bytes);
    bool ReadExact(void* buffer, DWORD size, DWORD timeout);
    bool WriteExact(const void* buffer, DWORD size);
    void Fail(LauncherIpcFailure failure);
};
