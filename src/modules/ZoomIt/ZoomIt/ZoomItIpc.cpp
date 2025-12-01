#include "pch.h"
#include "ZoomItIpc.h"

#include <format>
#include <thread>
#include <atomic>
#include <cctype>

using namespace std::literals;

namespace ZoomItIpc
{
    namespace
    {
        Command FromString(const std::string& action)
        {
            if (action == "zoom")
                return Command::Zoom;
            if (action == "draw")
                return Command::Draw;
            if (action == "break")
                return Command::Break;
            if (action == "livezoom")
                return Command::LiveZoom;
            if (action == "snip")
                return Command::Snip;
            if (action == "record")
                return Command::Record;
            return Command::Unknown;
        }

        Command ParseJson(const std::string& json)
        {
            // Very small parse: look for "action":"<value>"
            auto pos = json.find("\"action\"");
            if (pos == std::string::npos)
                return Command::Unknown;

            pos = json.find(':', pos);
            if (pos == std::string::npos)
                return Command::Unknown;

            pos = json.find('"', pos);
            if (pos == std::string::npos)
                return Command::Unknown;

            auto end = json.find('"', pos + 1);
            if (end == std::string::npos || end <= pos + 1)
                return Command::Unknown;

            auto value = json.substr(pos + 1, end - pos - 1);
            // normalize to lowercase
            std::transform(value.begin(), value.end(), value.begin(), [](unsigned char c) { return static_cast<char>(std::tolower(static_cast<int>(c))); });
            return FromString(value);
        }

        bool ReadPayload(HANDLE pipe, std::string& outPayload)
        {
            DWORD bytesRead = 0;
            char buffer[512] = {};
            if (!ReadFile(pipe, buffer, static_cast<DWORD>(sizeof(buffer) - 1), &bytesRead, nullptr) || bytesRead == 0)
            {
                return false;
            }

            buffer[bytesRead] = '\0';
            outPayload.assign(buffer, bytesRead);
            return true;
        }
    } // namespace

    Command ParseCommand(const std::string& payloadUtf8)
    {
        return ParseJson(payloadUtf8);
    }

    // ZoomIt.exe will call this; needs to hook into action dispatcher externally.
    bool RunServer()
    {
        std::thread([]() {
            for (;;)
            {
                auto hPipe = CreateNamedPipeW(
                    PIPE_NAME,
                    PIPE_ACCESS_DUPLEX,
                    PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
                    1,
                    1024,
                    1024,
                    0,
                    nullptr);

                if (hPipe == INVALID_HANDLE_VALUE)
                {
                    break;
                }

                if (!ConnectNamedPipe(hPipe, nullptr))
                {
                    CloseHandle(hPipe);
                    continue;
                }

                std::string payload;
                if (ReadPayload(hPipe, payload))
                {
                    auto cmd = ParseCommand(payload);
                    // Dispatch hook implemented elsewhere in ZoomIt.exe
                    extern void ZoomIt_DispatchCommand(Command cmd);
                    ZoomIt_DispatchCommand(cmd);
                }

                DisconnectNamedPipe(hPipe);
                CloseHandle(hPipe);
            }
        }).detach();

        return true;
    }

    bool SendCommand(Command cmd)
    {
        auto hPipe = CreateFileW(PIPE_NAME, GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
        if (hPipe == INVALID_HANDLE_VALUE)
        {
            return false;
        }

        const char* action = "unknown";
        switch (cmd)
        {
        case Command::Zoom:
            action = "zoom";
            break;
        case Command::Draw:
            action = "draw";
            break;
        case Command::Break:
            action = "break";
            break;
        case Command::LiveZoom:
            action = "livezoom";
            break;
        case Command::Snip:
            action = "snip";
            break;
        case Command::Record:
            action = "record";
            break;
        default:
            break;
        }

        auto payload = std::format("{{\"action\":\"{}\"}}", action);
        DWORD written = 0;
        const bool ok = WriteFile(hPipe, payload.data(), static_cast<DWORD>(payload.size()), &written, nullptr);
        CloseHandle(hPipe);
        return ok && written == payload.size();
    }
} // namespace ZoomItIpc
