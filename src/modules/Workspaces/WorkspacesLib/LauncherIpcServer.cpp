// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "LauncherIpcServer.h"

#include <sddl.h>
#include <algorithm>
#include <chrono>
#include <vector>
#include <common/logger/logger.h>
#include <workspaces-common/GuidUtils.h>

namespace
{
    constexpr DWORD IoTimeoutMs = 5000;
    constexpr DWORD ConnectTimeoutMs = 10000;

    DWORD Remaining(ULONGLONG start, DWORD timeout)
    {
        if (timeout == INFINITE)
        {
            return INFINITE;
        }
        const auto elapsed = GetTickCount64() - start;
        return elapsed >= timeout ? 0 : timeout - static_cast<DWORD>(elapsed);
    }
}

LauncherIpcServer::LauncherIpcServer(std::function<void(const std::wstring&)> receive,
                                     std::function<void(LauncherIpcFailure)> failed) :
    m_receive(std::move(receive)),
    m_failed(std::move(failed))
{
}

LauncherIpcServer::~LauncherIpcServer()
{
    Stop();
}

bool LauncherIpcServer::Initialize()
{
    if (m_pipe || m_stopping)
    {
        Logger::error(L"Workspaces UI channels cannot be reused");
        return false;
    }
    const auto id = CreateGuidString();
    if (id.empty())
    {
        Logger::error(L"Unable to generate the Workspaces UI channel identifier");
        return false;
    }
    m_name = L"PowerToys.Workspaces.Launcher." + id;
    m_closed.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
    wil::unique_handle token;
    if (!m_closed || !OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, token.put()))
    {
        Logger::error(L"Unable to initialize the Workspaces UI channel: {}", GetLastError());
        return false;
    }
    DWORD size = 0;
    GetTokenInformation(token.get(), TokenUser, nullptr, 0, &size);
    std::vector<BYTE> user(size);
    if (!size || !GetTokenInformation(token.get(), TokenUser, user.data(), size, &size))
    {
        Logger::error(L"Unable to obtain the UI channel owner: {}", GetLastError());
        return false;
    }
    LPWSTR sid = nullptr;
    if (!ConvertSidToStringSidW(reinterpret_cast<TOKEN_USER*>(user.data())->User.Sid, &sid))
    {
        Logger::error(L"Unable to obtain the UI channel SID: {}", GetLastError());
        return false;
    }
    auto freeSid = wil::scope_exit([&] { LocalFree(sid); });
    const std::wstring sddl = L"D:P(A;;GA;;;SY)(A;;GA;;;" + std::wstring(sid) + L")";
    PSECURITY_DESCRIPTOR descriptor = nullptr;
    if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1, &descriptor, nullptr))
    {
        Logger::error(L"Unable to secure the Workspaces UI channel: {}", GetLastError());
        return false;
    }
    auto freeDescriptor = wil::scope_exit([&] { LocalFree(descriptor); });
    SECURITY_ATTRIBUTES attributes{ sizeof(attributes), descriptor, FALSE };
    const auto path = L"\\\\.\\pipe\\" + m_name;
    m_pipe.reset(CreateNamedPipeW(path.c_str(), PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED | FILE_FLAG_FIRST_PIPE_INSTANCE, PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS, 1, 65536, 65536, 0, &attributes));
    if (!m_pipe)
    {
        Logger::error(L"Unable to create the Workspaces UI channel: {}", GetLastError());
        return false;
    }
    return true;
}

bool LauncherIpcServer::Start(HANDLE peer, const interop_auth::CallerPolicy& policy)
{
    if (!m_pipe || m_stopping || m_failureReported || m_reader.joinable() || !peer || !policy.enabled || !policy.expectedClientPid)
    {
        Logger::error(L"Invalid Workspaces UI channel startup state");
        return false;
    }
    HANDLE duplicate = nullptr;
    if (!DuplicateHandle(GetCurrentProcess(), peer, GetCurrentProcess(), &duplicate, 0, FALSE, DUPLICATE_SAME_ACCESS))
    {
        Logger::error(L"Unable to track the Workspaces UI process: {}", GetLastError());
        return false;
    }
    m_peer.reset(duplicate);
    m_policy = policy;
    try
    {
        m_reader = std::thread([this] { ReadLoop(); });
    }
    catch (const std::system_error& error)
    {
        Logger::error("Unable to start the Workspaces UI channel: {}", error.what());
        Fail(LauncherIpcFailure::Disconnected);
        return false;
    }
    return true;
}

bool LauncherIpcServer::FinishIo(OVERLAPPED& operation, DWORD timeout, DWORD& bytes)
{
    const HANDLE handles[] = { m_closed.get(), m_peer.get(), operation.hEvent };
    const auto wait = WaitForMultipleObjects(ARRAYSIZE(handles), handles, FALSE, timeout);
    if (wait == WAIT_OBJECT_0 + 2)
    {
        return GetOverlappedResult(m_pipe.get(), &operation, &bytes, FALSE) != FALSE;
    }
    CancelIoEx(m_pipe.get(), &operation);
    GetOverlappedResult(m_pipe.get(), &operation, &bytes, TRUE);
    if (wait == WAIT_TIMEOUT)
    {
        Fail(LauncherIpcFailure::Timeout);
    }
    return false;
}

bool LauncherIpcServer::ReadExact(void* buffer, DWORD size, DWORD timeout)
{
    auto start = GetTickCount64();
    auto bytes = static_cast<BYTE*>(buffer);
    DWORD offset = 0;
    while (offset < size && !m_stopping)
    {
        wil::unique_event event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        if (!event)
        {
            return false;
        }
        OVERLAPPED operation{};
        operation.hEvent = event.get();
        DWORD received = 0;
        const auto success = ReadFile(m_pipe.get(), bytes + offset, size - offset, &received, &operation);
        if (!success && GetLastError() != ERROR_IO_PENDING)
        {
            return false;
        }
        if (!FinishIo(operation, Remaining(start, timeout), received) || received == 0)
        {
            return false;
        }
        offset += received;
        if (timeout == INFINITE)
        {
            timeout = IoTimeoutMs;
            start = GetTickCount64();
        }
    }
    return offset == size;
}

bool LauncherIpcServer::WriteExact(const void* buffer, DWORD size)
{
    const auto start = GetTickCount64();
    const auto bytes = static_cast<const BYTE*>(buffer);
    DWORD offset = 0;
    while (offset < size && !m_stopping)
    {
        wil::unique_event event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        if (!event)
        {
            return false;
        }
        OVERLAPPED operation{};
        operation.hEvent = event.get();
        DWORD written = 0;
        const auto success = WriteFile(m_pipe.get(), bytes + offset, size - offset, &written, &operation);
        if (!success && GetLastError() != ERROR_IO_PENDING)
        {
            return false;
        }
        if (!FinishIo(operation, Remaining(start, IoTimeoutMs), written) || written == 0)
        {
            return false;
        }
        offset += written;
    }
    return offset == size;
}

void LauncherIpcServer::ReadLoop()
{
    const auto initialized = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(initialized))
    {
        Fail(LauncherIpcFailure::Disconnected);
        return;
    }
    auto uninitialize = wil::scope_exit([] { CoUninitialize(); });
    const auto start = GetTickCount64();
    while (!m_stopping && Remaining(start, ConnectTimeoutMs) != 0)
    {
        wil::unique_event event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        if (!event)
        {
            break;
        }
        OVERLAPPED operation{};
        operation.hEvent = event.get();
        const auto connected = ConnectNamedPipe(m_pipe.get(), &operation);
        const auto error = connected ? ERROR_SUCCESS : GetLastError();
        DWORD ignored = 0;
        if (error != ERROR_SUCCESS && error != ERROR_PIPE_CONNECTED &&
            (error != ERROR_IO_PENDING || !FinishIo(operation, Remaining(start, ConnectTimeoutMs), ignored)))
        {
            break;
        }
        if (interop_auth::AuthenticateClient(m_pipe.get(), m_policy, m_authCache).accepted)
        {
            m_connected = true;
            break;
        }
        DisconnectNamedPipe(m_pipe.get());
    }
    if (!m_connected)
    {
        Fail(LauncherIpcFailure::Authentication);
        return;
    }

    while (!m_stopping && m_connected)
    {
        DWORD length = 0;
        if (!ReadExact(&length, sizeof(length), INFINITE))
        {
            break;
        }
        if (length == 0 || length > MaxMessageBytes)
        {
            Fail(LauncherIpcFailure::InvalidMessage);
            return;
        }
        std::vector<char> buffer(length);
        if (!ReadExact(buffer.data(), length, IoTimeoutMs))
        {
            break;
        }
        const auto characters = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, buffer.data(), static_cast<int>(length), nullptr, 0);
        if (!characters || std::find(buffer.begin(), buffer.end(), '\0') != buffer.end())
        {
            Fail(LauncherIpcFailure::InvalidMessage);
            return;
        }
        std::wstring message(characters, L'\0');
        if (MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, buffer.data(), static_cast<int>(length), message.data(), characters) != characters)
        {
            Fail(LauncherIpcFailure::InvalidMessage);
            return;
        }
        m_receive(message);
    }
    Fail(LauncherIpcFailure::Disconnected);
}

bool LauncherIpcServer::Send(const std::wstring& message)
{
    if (!IsConnected())
    {
        return false;
    }
    if (message.empty() || message.size() > MaxMessageBytes || message.find(L'\0') != std::wstring::npos)
    {
        Fail(LauncherIpcFailure::InvalidMessage);
        return false;
    }
    const auto size = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, message.data(), static_cast<int>(message.size()), nullptr, 0, nullptr, nullptr);
    if (!size || size > MaxMessageBytes)
    {
        Fail(LauncherIpcFailure::InvalidMessage);
        return false;
    }
    std::vector<BYTE> frame(sizeof(DWORD) + size);
    const auto length = static_cast<DWORD>(size);
    memcpy(frame.data(), &length, sizeof(length));
    if (WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, message.data(), static_cast<int>(message.size()), reinterpret_cast<char*>(frame.data() + sizeof(length)), size, nullptr, nullptr) != size)
    {
        Fail(LauncherIpcFailure::InvalidMessage);
        return false;
    }
    std::unique_lock lock(m_writeMutex, std::defer_lock);
    if (!lock.try_lock_for(std::chrono::milliseconds(IoTimeoutMs)) || !IsConnected() ||
        !WriteExact(frame.data(), static_cast<DWORD>(frame.size())))
    {
        Fail(LauncherIpcFailure::Disconnected);
        return false;
    }
    return true;
}

void LauncherIpcServer::Fail(LauncherIpcFailure failure)
{
    const bool report = !m_stopping && !m_failureReported.exchange(true);
    Disconnect();
    if (report)
    {
        m_failed(failure);
    }
}

bool LauncherIpcServer::IsConnected() const noexcept
{
    return m_connected && !m_stopping && !m_failureReported;
}

const std::wstring& LauncherIpcServer::Name() const noexcept
{
    return m_name;
}

void LauncherIpcServer::Disconnect()
{
    m_connected = false;
    if (m_closed)
    {
        SetEvent(m_closed.get());
    }
    if (m_pipe)
    {
        CancelIoEx(m_pipe.get(), nullptr);
        DisconnectNamedPipe(m_pipe.get());
    }
}

void LauncherIpcServer::Stop()
{
    m_stopping = true;
    Disconnect();
    if (m_reader.joinable())
    {
        m_reader.join();
    }
}
