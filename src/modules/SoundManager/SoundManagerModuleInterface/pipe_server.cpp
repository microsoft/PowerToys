#include "pch.h"

#include "pipe_server.h"

#include "audio_engine.h"

#include <common/logger/logger.h>
#include <common/utils/json.h>
#include <common/utils/process_path.h>

#include <sddl.h>

using namespace SoundManager;

namespace
{
    constexpr DWORD PIPE_BUFFER_SIZE = 64 * 1024;

    // The piece of a message that is read at once.
    constexpr DWORD READ_BUFFER_SIZE = 4096;

    // The pipe is only for the user who runs PowerToys and for the system itself.
    constexpr wchar_t PIPE_SECURITY[] = L"D:P(A;;GA;;;OW)(A;;GA;;;SY)";

    // The programs that are allowed to talk to the pipe.
    constexpr wchar_t CLIENT_EXE[] = L"\\WinUI3Apps\\PowerToys.SoundManager.exe";
    constexpr wchar_t SETTINGS_CLIENT_EXE[] = L"\\WinUI3Apps\\PowerToys.Settings.exe";

    DWORD GetClientProcessId(HANDLE pipe)
    {
        DWORD processId = 0;
        if (!GetNamedPipeClientProcessId(pipe, &processId))
        {
            return 0;
        }

        return processId;
    }

    // Whether the program that connected is one of ours. The pipe belongs to the user, so any program of that user could connect and has to be checked.
    bool IsSoundManagerClient(HANDLE pipe)
    {
        const DWORD processId = GetClientProcessId(pipe);
        if (processId == 0)
        {
            return false;
        }

        wil::unique_handle process(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId));
        if (!process)
        {
            return false;
        }

        wchar_t path[MAX_PATH] = {};
        DWORD size = MAX_PATH;
        if (QueryFullProcessImageNameW(process.get(), 0, path, &size) == 0)
        {
            return false;
        }

        const std::wstring folder = get_module_folderpath();

        std::wstring expected = folder + CLIENT_EXE;
        if (_wcsicmp(path, expected.c_str()) == 0)
        {
            return true;
        }

        expected = folder + SETTINGS_CLIENT_EXE;
        return _wcsicmp(path, expected.c_str()) == 0;
    }

    std::string Trim(const std::string& text)
    {
        const auto first = text.find_first_not_of(" \t\r\n");
        if (first == std::string::npos)
        {
            return {};
        }

        return text.substr(first, text.find_last_not_of(" \t\r\n") - first + 1);
    }

    bool WriteMessage(HANDLE pipe, const std::string& message)
    {
        std::string payload = message;
        payload.push_back('\n');

        DWORD written = 0;
        return WriteFile(pipe, payload.data(), static_cast<DWORD>(payload.size()), &written, nullptr) != FALSE &&
               written == payload.size();
    }

    // The pipe is in message mode, so a message that is bigger than the buffer is read in pieces.
    bool ReadMessage(HANDLE pipe, std::string& message)
    {
        message.clear();

        char buffer[READ_BUFFER_SIZE];
        for (;;)
        {
            DWORD bytesRead = 0;
            if (ReadFile(pipe, buffer, sizeof(buffer), &bytesRead, nullptr))
            {
                message.append(buffer, bytesRead);
                return true;
            }

            if (GetLastError() == ERROR_MORE_DATA)
            {
                message.append(buffer, bytesRead);
                continue;
            }

            return false;
        }
    }

    json::JsonObject ParseMessage(const std::string& text)
    {
        json::JsonObject command;
        try
        {
            command = json::JsonValue::Parse(winrt::to_hstring(text)).GetObjectW();
        }
        catch (const winrt::hresult_error&)
        {
            Logger::error(L"Sound Manager received a message that is not valid JSON");
        }

        return command;
    }

    std::wstring GetString(const json::JsonObject& command, const wchar_t* name)
    {
        std::wstring value;
        json::get(command, name, value);
        return value;
    }

    // The commands are read with these, so that a command that is missing a value is refused instead of being carried out with a default.
    bool TryGetString(const json::JsonObject& command, const wchar_t* name, std::wstring& value)
    {
        try
        {
            value = command.GetNamedString(name);
            return true;
        }
        catch (const winrt::hresult_error&)
        {
            return false;
        }
    }

    bool TryGetNumber(const json::JsonObject& command, const wchar_t* name, float& value)
    {
        try
        {
            value = static_cast<float>(command.GetNamedNumber(name));
            return true;
        }
        catch (const winrt::hresult_error&)
        {
            return false;
        }
    }

    bool TryGetFlag(const json::JsonObject& command, const wchar_t* name, bool& value)
    {
        try
        {
            value = command.GetNamedBoolean(name);
            return true;
        }
        catch (const winrt::hresult_error&)
        {
            return false;
        }
    }

    bool TryGetProcessId(const json::JsonObject& command, DWORD& value)
    {
        try
        {
            value = static_cast<DWORD>(command.GetNamedNumber(L"processId"));
            return true;
        }
        catch (const winrt::hresult_error&)
        {
            return false;
        }
    }

    struct AppCommand
    {
        DWORD processId = 0;
        bool isCapture = false;
    };

    bool TryGetAppCommand(const json::JsonObject& command, AppCommand& target)
    {
        return TryGetProcessId(command, target.processId) && TryGetFlag(command, L"isCapture", target.isCapture);
    }

    UINT32 GetRequestId(const json::JsonObject& command)
    {
        UINT32 value = 0;
        json::get(command, L"id", value);
        return value;
    }

    json::JsonArray BuildDevices(const std::vector<DeviceVolume>& devices)
    {
        json::JsonArray array;
        for (const auto& device : devices)
        {
            json::JsonObject entry;
            entry.SetNamedValue(L"id", json::value(device.device.id.c_str()));
            entry.SetNamedValue(L"name", json::value(device.device.name.c_str()));
            entry.SetNamedValue(L"isDefault", json::value(device.device.isDefault));
            entry.SetNamedValue(L"isDefaultCommunications", json::value(device.device.isDefaultCommunications));
            entry.SetNamedValue(L"volume", json::value(device.volume));
            entry.SetNamedValue(L"muted", json::value(device.muted));
            entry.SetNamedValue(L"listening", json::value(device.listening));
            entry.SetNamedValue(L"balanceLeft", json::value(device.balanceLeft));
            entry.SetNamedValue(L"balanceRight", json::value(device.balanceRight));
            array.Append(entry.as<json::JsonValue>());
        }
        return array;
    }

    json::JsonArray BuildApps(const std::vector<AppVolume>& apps)
    {
        json::JsonArray array;
        for (const auto& app : apps)
        {
            json::JsonObject entry;
            entry.SetNamedValue(L"sessionId", json::value(app.sessionId.c_str()));
            entry.SetNamedValue(L"processPath", json::value(app.processPath.c_str()));
            entry.SetNamedValue(L"displayName", json::value(app.displayName.c_str()));
            entry.SetNamedValue(L"processId", json::value(static_cast<UINT32>(app.processId)));
            entry.SetNamedValue(L"volume", json::value(app.volume));
            entry.SetNamedValue(L"muted", json::value(app.muted));
            entry.SetNamedValue(L"isActive", json::value(app.isActive));
            entry.SetNamedValue(L"isCapture", json::value(app.isCapture));
            entry.SetNamedValue(L"outputDeviceId", json::value(app.outputDeviceId.c_str()));
            entry.SetNamedValue(L"balanceLeft", json::value(app.balanceLeft));
            entry.SetNamedValue(L"balanceRight", json::value(app.balanceRight));
            array.Append(entry.as<json::JsonValue>());
        }
        return array;
    }

    json::JsonArray BuildPeakLevels(const std::vector<PeakLevel>& peaks)
    {
        json::JsonArray array;
        for (const auto& peak : peaks)
        {
            json::JsonObject entry;
            entry.SetNamedValue(L"sessionId", json::value(peak.sessionId.c_str()));
            entry.SetNamedValue(L"peak", json::value(peak.peak));
            array.Append(entry.as<json::JsonValue>());
        }
        return array;
    }

    bool ApplyCommand(AudioEngine& engine, const json::JsonObject& command)
    {
        const std::wstring name = GetString(command, L"command");
        if (name.empty())
        {
            Logger::error(L"Sound Manager received a message without a command");
            return false;
        }

        if (name == L"set_app_volume")
        {
            AppCommand app;
            float value = 0.0f;

            if (!TryGetAppCommand(command, app) || !TryGetNumber(command, L"value", value))
            {
                Logger::error(L"Sound Manager received a volume command without the values it needs");
                return false;
            }

            return engine.SetApplicationVolume(app.processId, app.isCapture, value);
        }

        if (name == L"set_app_mute")
        {
            AppCommand app;
            bool muted = false;

            if (!TryGetAppCommand(command, app) || !TryGetFlag(command, L"value", muted))
            {
                Logger::error(L"Sound Manager received a mute command without the values it needs");
                return false;
            }

            return engine.SetApplicationMute(app.processId, app.isCapture, muted);
        }

        if (name == L"set_app_balance")
        {
            AppCommand app;
            float left = 0.0f;
            float right = 0.0f;

            if (!TryGetAppCommand(command, app) || !TryGetNumber(command, L"value", left) ||
                !TryGetNumber(command, L"secondValue", right))
            {
                Logger::error(L"Sound Manager received a balance command without the values it needs");
                return false;
            }

            return engine.SetApplicationBalance(app.processId, app.isCapture, left, right);
        }

        if (name == L"set_app_device")
        {
            AppCommand app;
            std::wstring deviceId;

            if (!TryGetAppCommand(command, app) || !TryGetString(command, L"deviceId", deviceId))
            {
                Logger::error(L"Sound Manager received a routing command without the values it needs");
                return false;
            }

            return engine.SetApplicationDevice(app.processId, deviceId);
        }

        if (name == L"set_device_volume")
        {
            std::wstring deviceId;
            float value = 0.0f;

            if (!TryGetString(command, L"deviceId", deviceId) || !TryGetNumber(command, L"value", value))
            {
                Logger::error(L"Sound Manager received a device volume command without the values it needs");
                return false;
            }

            return engine.SetDeviceVolume(deviceId, value);
        }

        if (name == L"set_device_mute")
        {
            std::wstring deviceId;
            bool muted = false;

            if (!TryGetString(command, L"deviceId", deviceId) || !TryGetFlag(command, L"value", muted))
            {
                Logger::error(L"Sound Manager received a device mute command without the values it needs");
                return false;
            }

            return engine.SetDeviceMute(deviceId, muted);
        }

        if (name == L"set_default_device")
        {
            std::wstring deviceId;
            bool communications = false;

            if (!TryGetString(command, L"deviceId", deviceId) || !TryGetFlag(command, L"communications", communications))
            {
                Logger::error(L"Sound Manager received a default device command without the values it needs");
                return false;
            }

            return engine.SetDefaultDevice(deviceId, communications);
        }

        if (name == L"set_device_listen")
        {
            std::wstring deviceId;
            bool listening = false;

            if (!TryGetString(command, L"deviceId", deviceId) || !TryGetFlag(command, L"value", listening))
            {
                Logger::error(L"Sound Manager received a listen command without the values it needs");
                return false;
            }

            return engine.SetDeviceListening(deviceId, listening);
        }

        if (name == L"set_device_balance")
        {
            std::wstring deviceId;
            float left = 0.0f;
            float right = 0.0f;

            if (!TryGetString(command, L"deviceId", deviceId) || !TryGetNumber(command, L"value", left) ||
                !TryGetNumber(command, L"secondValue", right))
            {
                Logger::error(L"Sound Manager received a device balance command without the values it needs");
                return false;
            }

            return engine.SetDeviceBalance(deviceId, left, right);
        }

        if (name == L"test_device")
        {
            std::wstring deviceId;

            if (!TryGetString(command, L"deviceId", deviceId))
            {
                Logger::error(L"Sound Manager received a test sound command without the device it needs");
                return false;
            }

            return engine.PlayTestSound(deviceId);
        }

        if (name == L"set_ducking")
        {
            float value = 0.0f;

            if (!TryGetNumber(command, L"value", value))
            {
                Logger::error(L"Sound Manager received a ducking command without the value it needs");
                return false;
            }

            return engine.SetDuckingMode(static_cast<DuckingMode>(static_cast<int>(value)));
        }

        Logger::error(L"Sound Manager received a command it does not know: {}", name);
        return false;
    }

    // The answer to a command that changes something. It tells the window whether Windows took it.
    std::string BuildResult(const json::JsonObject& command, bool ok)
    {
        json::JsonObject root;
        root.SetNamedValue(L"id", json::value(GetRequestId(command)));
        root.SetNamedValue(L"ok", json::value(ok));
        return winrt::to_string(root.Stringify());
    }
}

PipeServer::PipeServer(std::wstring pipeName) :
    m_pipeName(std::move(pipeName))
{
}

PipeServer::~PipeServer()
{
    Stop();
}

void PipeServer::Start()
{
    if (m_running.exchange(true))
    {
        return;
    }

    m_thread = std::thread(&PipeServer::Serve, this);
}

void PipeServer::Stop()
{
    if (!m_running.exchange(false))
    {
        return;
    }

    // The thread waits for a client to connect or reads from a connected client, and both have to be interrupted to let it finish.
    CancelSynchronousIo(m_thread.native_handle());

    // The cancel only reaches an operation that is already running, so the pipe is opened once from here to let the waiting thread come back.
    WakeUp();

    if (m_thread.joinable())
    {
        m_thread.join();
    }
}

// Lets the thread that waits for a client come back. The pipe may not exist yet, because the thread is on its way to create it.
void PipeServer::WakeUp()
{
    const std::wstring fullPipeName = L"\\\\.\\pipe\\" + m_pipeName;

    for (int attempt = 0; attempt < 20; attempt++)
    {
        const HANDLE client = CreateFileW(fullPipeName.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
        if (client != INVALID_HANDLE_VALUE)
        {
            CloseHandle(client);
            return;
        }

        Sleep(25);
    }
}

void PipeServer::Serve()
{
    CoInitializeEx(nullptr, COINIT_MULTITHREADED);

    {
        AudioEngine engine;
        if (!engine.Initialize())
        {
            Logger::error(L"Sound Manager could not open the audio devices");
            CoUninitialize();
            return;
        }

        PSECURITY_DESCRIPTOR descriptor = nullptr;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(PIPE_SECURITY, SDDL_REVISION_1, &descriptor, nullptr))
        {
            Logger::error(L"Sound Manager could not build the security of the pipe");
            CoUninitialize();
            return;
        }

        SECURITY_ATTRIBUTES security{};
        security.nLength = sizeof(security);
        security.lpSecurityDescriptor = descriptor;
        security.bInheritHandle = FALSE;

        const std::wstring fullPipeName = L"\\\\.\\pipe\\" + m_pipeName;

        bool reportedFailure = false;
        bool firstInstance = true;

        while (m_running)
        {
            DWORD flags = PIPE_ACCESS_DUPLEX;
            if (firstInstance)
            {
                // Only the first instance may use FILE_FLAG_FIRST_PIPE_INSTANCE.
                flags |= FILE_FLAG_FIRST_PIPE_INSTANCE;
            }

            HANDLE pipe = CreateNamedPipeW(fullPipeName.c_str(),
                                           flags,
                                           PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
                                           PIPE_UNLIMITED_INSTANCES,
                                           PIPE_BUFFER_SIZE,
                                           PIPE_BUFFER_SIZE,
                                           0,
                                           &security);
            if (pipe == INVALID_HANDLE_VALUE)
            {
                if (!reportedFailure)
                {
                    reportedFailure = true;
                    Logger::error(L"Sound Manager could not open the pipe, error 0x{:08X}", static_cast<uint32_t>(GetLastError()));
                }

                Sleep(100);
                continue;
            }

            firstInstance = false;

            if (!m_running)
            {
                DisconnectNamedPipe(pipe);
                CloseHandle(pipe);
                break;
            }

            const BOOL connected = ConnectNamedPipe(pipe, nullptr) ? TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);
            if (connected && m_running)
            {
                if (GetClientProcessId(pipe) == GetCurrentProcessId())
                {
                    // The server wakes itself up by connecting to its own pipe when it is stopped.
                    DisconnectNamedPipe(pipe);
                    CloseHandle(pipe);
                    continue;
                }

                if (IsSoundManagerClient(pipe))
                {
                    // Every client is served on a thread of its own.
                    std::lock_guard lock(m_clientMutex);
                    ReapFinishedClients();
                    m_clientThreads.emplace_back([this, pipe, &engine] { ServeClient(pipe, engine); });
                }
                else
                {
                    Logger::error(L"Sound Manager refused a connection from a program that is not one of ours");
                    DisconnectNamedPipe(pipe);
                    CloseHandle(pipe);
                }
            }
            else
            {
                DisconnectNamedPipe(pipe);
                CloseHandle(pipe);
            }
        }

        // The clients are done before the engine is gone, so none is left with a dangling pointer.
        {
            std::lock_guard lock(m_clientMutex);

            for (auto& clientThread : m_clientThreads)
            {
                CancelSynchronousIo(clientThread.native_handle());
            }

            for (auto& clientThread : m_clientThreads)
            {
                if (clientThread.joinable())
                {
                    clientThread.join();
                }
            }

            m_clientThreads.clear();
        }

        LocalFree(descriptor);
    }

    // The engine is gone by now, so everything it held is released while the apartment is still there.
    CoUninitialize();
}

void PipeServer::ServeClient(HANDLE pipe, AudioEngine& engine)
{
    HandleClient(pipe, engine);

    DisconnectNamedPipe(pipe);
    CloseHandle(pipe);
}

void PipeServer::HandleClient(HANDLE pipe, AudioEngine& engine)
{
    while (m_running)
    {
        std::string message;
        if (!ReadMessage(pipe, message))
        {
            break;
        }

        // Two commands written one after the other can arrive in one piece, so the message is cut into its lines and each one is carried out.
        size_t start = 0;
        while (start < message.size())
        {
            const size_t end = message.find('\n', start);
            const std::string line = message.substr(start, end == std::string::npos ? std::string::npos : end - start);
            start = end == std::string::npos ? message.size() : end + 1;

            HandleCommand(pipe, engine, line);
        }
    }
}

void PipeServer::HandleCommand(HANDLE pipe, AudioEngine& engine, const std::string& line)
{
    const std::string trimmed = Trim(line);
    if (trimmed.empty())
    {
        return;
    }

    const json::JsonObject command = ParseMessage(trimmed);
    const std::wstring name = GetString(command, L"command");

    if (name == L"get_snapshot")
    {
        WriteMessage(pipe, BuildSnapshot(engine));
        return;
    }

    if (name == L"get_peaks")
    {
        WriteMessage(pipe, BuildPeaks(engine));
        return;
    }

    if (name == L"get_ducking")
    {
        // The ducking mode is asked for on its own, without a whole snapshot of the mixer.
        json::JsonObject root;
        root.SetNamedValue(L"ducking", json::value(static_cast<int>(engine.GetDuckingMode())));
        WriteMessage(pipe, winrt::to_string(root.Stringify()));
        return;
    }

    WriteMessage(pipe, BuildResult(command, ApplyCommand(engine, command)));
}

// The clients that are done are given back, so that their threads do not pile up.
void PipeServer::ReapFinishedClients()
{
    for (auto it = m_clientThreads.begin(); it != m_clientThreads.end();)
    {
        if (WaitForSingleObject(it->native_handle(), 0) == WAIT_OBJECT_0)
        {
            it->join();
            it = m_clientThreads.erase(it);
            continue;
        }

        ++it;
    }
}

std::string PipeServer::BuildSnapshot(AudioEngine& engine)
{
    MixerSnapshot snapshot;
    engine.GetDevices(snapshot.outputs, snapshot.inputs);
    snapshot.apps = engine.GetSessions();

    const json::JsonArray outputs = BuildDevices(snapshot.outputs);
    const json::JsonArray inputs = BuildDevices(snapshot.inputs);
    const json::JsonArray apps = BuildApps(snapshot.apps);

    json::JsonObject root;
    root.SetNamedValue(L"outputs", outputs.as<json::JsonValue>());
    root.SetNamedValue(L"inputs", inputs.as<json::JsonValue>());
    root.SetNamedValue(L"apps", apps.as<json::JsonValue>());
    root.SetNamedValue(L"testingDeviceId", json::value(engine.GetTestingDeviceId().c_str()));

    return winrt::to_string(root.Stringify());
}

std::string PipeServer::BuildPeaks(AudioEngine& engine)
{
    std::vector<PeakLevel> peaks;
    engine.GetPeaks(peaks);

    const json::JsonArray array = BuildPeakLevels(peaks);

    json::JsonObject root;
    root.SetNamedValue(L"peaks", array.as<json::JsonValue>());

    return winrt::to_string(root.Stringify());
}

