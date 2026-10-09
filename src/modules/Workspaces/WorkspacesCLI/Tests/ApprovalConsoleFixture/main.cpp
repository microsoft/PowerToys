// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
// This fixture only exchanges decisions. It never executes a configured application or requests UAC.
#include <windows.h>
#include <algorithm>
#include <future>
#include <iostream>
#include <WorkspacesCLI/ApprovalChannel.h>
#include <WorkspacesCLI/ConsoleApproval.h>
#include <WorkspacesLib/CliCommands.h>

namespace
{
    HANDLE inputDuringOutput = nullptr;
    bool typeaheadInjected = false;
    DWORD outputWrites = 0;

    void Check(bool value, const char* message = "Isolated console assertion failed")
    {
        if (!value)
            throw std::runtime_error(message);
    }

    void Type(HANDLE input, std::wstring_view text)
    {
        std::vector<INPUT_RECORD> records;
        for (const auto value : text)
        {
            INPUT_RECORD record{};
            record.EventType = KEY_EVENT;
            record.Event.KeyEvent.bKeyDown = TRUE;
            record.Event.KeyEvent.wRepeatCount = 1;
            record.Event.KeyEvent.uChar.UnicodeChar = value;
            records.push_back(record);
        }
        DWORD written{};
        Check(WriteConsoleInputW(input, records.data(), static_cast<DWORD>(records.size()), &written) &&
              written == records.size());
    }

    BOOL WINAPI WriteConsoleWithTypeahead(HANDLE output, const void* buffer, DWORD count, LPDWORD written, LPVOID reserved)
    {
        const auto result = WriteConsoleW(output, buffer, count, written, reserved);
        ++outputWrites;
        if (result && !typeaheadInjected)
        {
            typeaheadInjected = true;
            Type(inputDuringOutput, L"A\r");
        }
        return result;
    }

    void Run(std::wstring_view scenario)
    {
        wil::unique_handle input(CreateFileW(L"CONIN$", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr));
        wil::unique_handle output(CreateConsoleScreenBuffer(GENERIC_READ | GENERIC_WRITE,
                                                            FILE_SHARE_READ | FILE_SHARE_WRITE,
                                                            nullptr,
                                                            CONSOLE_TEXTMODE_BUFFER,
                                                            nullptr));
        Check(input && output);
        DWORD originalMode{};
        Check(GetConsoleMode(input.get(), &originalMode) != FALSE);
        auto redirected = WorkspacesCli::ApprovalPipe::Create();
        WorkspacesCli::ApprovalPromptText text{
            L"TEST warning", L"Application: ", L"Path: ", L"Arguments: ", L"Verification: ", L"Status: ", L"[A] Allow once [S] Skip (default): ", L"Invalid choice"
        };
        const bool duringOutput = scenario.starts_with(L"during-output-");
        if (duringOutput)
        {
            inputDuringOutput = input.get();
            // Force multiple writes so injected input arrives before the whole prompt is written.
            text.choices.append(8192, L' ');
            CONSOLE_SCREEN_BUFFER_INFO buffer{};
            Check(GetConsoleScreenBufferInfo(output.get(), &buffer) != FALSE);
            buffer.dwSize.Y = (std::max)(buffer.dwSize.Y, SHORT{ 512 });
            Check(SetConsoleScreenBufferSize(output.get(), buffer.dwSize) != FALSE);
        }
        WorkspacesCli::ConsoleApproval console(scenario == L"redirected-input" ? redirected.read.get() : input.get(),
                                               scenario == L"redirected-error" ? redirected.write.get() : output.get(),
                                               text, duringOutput ? &WriteConsoleWithTypeahead : &WriteConsoleW);
        auto requests = WorkspacesCli::ApprovalPipe::Create();
        auto replies = WorkspacesCli::ApprovalPipe::Create();
        wil::unique_handle cancel(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        Check(static_cast<bool>(cancel));
        WorkspacesCli::FrontendApproval frontend(requests.read.get(), replies.write.get(), console, cancel.get());
        const bool twoRequests = scenario == L"two-requests" || scenario == L"typeahead";
        const auto deadline = GetTickCount64() + (scenario == L"timeout" || scenario == L"typeahead" || scenario == L"during-output-timeout" ? 1500 : 5000);
        auto work = std::async(std::launch::async, [&] {
            winrt::init_apartment();
            WorkspacesCli::WorkerApproval worker(requests.write.get(), replies.read.get(), [&] {
                return WaitForSingleObject(cancel.get(), 0) == WAIT_OBJECT_0 || GetTickCount64() >= deadline;
            });
            std::vector<LaunchDecision> results;
            results.push_back(worker.Request(L"Test\x001b[2J\u202E", L"C:\\test.exe", L"--test\nargument", { SignatureVerification::Status::Unsigned, TRUST_E_NOSIGNATURE, {}, {} }));
            if (twoRequests)
                results.push_back(worker.Request(L"Second", L"C:\\second.exe", L"", { SignatureVerification::Status::Unsigned, TRUST_E_NOSIGNATURE, {}, {} }));
            return results;
        });
        auto stop = wil::scope_exit([&] {
            SetEvent(cancel.get());
            frontend.Stop();
            if (work.valid())
                work.wait();
        });
        int prompts = 0;
        bool wasActive = false;
        while (work.wait_for(std::chrono::milliseconds(0)) != std::future_status::ready)
        {
            if (GetTickCount64() >= deadline)
            {
                SetEvent(cancel.get());
                frontend.Stop();
            }
            else
            {
                frontend.Poll();
                if (console.Active() && !wasActive)
                {
                    ++prompts;
                    if (scenario == L"cancel")
                        SetEvent(cancel.get());
                    else if (scenario == L"timeout" || scenario == L"during-output-timeout")
                    {
                    }
                    else if (scenario == L"typeahead")
                    {
                        if (prompts == 1)
                            Type(input.get(), L"a\ra\r");
                    }
                    else if (scenario == L"allow" || scenario == L"during-output-allow")
                        Type(input.get(), L"A\r");
                    else if (scenario == L"invalid")
                        Type(input.get(), L"allow\rA\r");
                    else if (scenario == L"skip" || scenario == L"during-output-skip")
                        Type(input.get(), L"S\r");
                    else if (scenario == L"eof")
                        Type(input.get(), std::wstring_view(L"\x001a", 1));
                    else if (scenario == L"two-requests")
                        Type(input.get(), prompts == 1 ? L"a\r" : L"s\r");
                    else
                        Type(input.get(), L"\r");
                }
                wasActive = console.Active();
            }
            Check(GetTickCount64() < deadline + 3000);
            Sleep(1);
        }
        const auto decisions = work.get();
        frontend.Stop();
        DWORD restoredMode{};
        Check(GetConsoleMode(input.get(), &restoredMode) && restoredMode == originalMode);
        if (duringOutput)
            Check(typeaheadInjected && outputWrites >= 3 && prompts == 1, "During-output input was not discarded before accepting a fresh decision.");
        if (scenario == L"redirected-input" || scenario == L"redirected-error" || scenario == L"eof")
            Check(decisions[0] == LaunchDecision::UiUnavailable);
        else if (scenario == L"timeout" || scenario == L"cancel" || scenario == L"during-output-timeout")
            Check(decisions[0] == LaunchDecision::Canceled);
        else if (scenario == L"allow" || scenario == L"invalid" || scenario == L"during-output-allow" || twoRequests)
            Check(decisions[0] == LaunchDecision::Approved);
        else
            Check(decisions[0] == LaunchDecision::Skipped);
        if (twoRequests)
            Check(decisions[1] == (scenario == L"typeahead" ? LaunchDecision::Canceled : LaunchDecision::Skipped));
        if (prompts)
        {
            wchar_t characters[4096]{};
            DWORD read{};
            Check(ReadConsoleOutputCharacterW(output.get(), characters, ARRAYSIZE(characters), { 0, 0 }, &read));
            const std::wstring displayed(characters, read);
            Check(displayed.find(L"\\u001B[2J\\u202E") != std::wstring::npos);
        }
    }
}

int wmain(int argc, wchar_t** argv)
{
    try
    {
        winrt::init_apartment();
        Check(argc == 2);
        Run(argv[1]);
        std::cout << "{\"passed\":true}\n";
        return 0;
    }
    catch (const std::exception& error)
    {
        json::JsonObject result;
        result.SetNamedValue(L"passed", json::value(false));
        result.SetNamedValue(L"error", json::value(winrt::to_hstring(error.what())));
        std::cout << winrt::to_string(result.Stringify()) << '\n';
        return 1;
    }
    catch (const winrt::hresult_error&)
    {
        std::cout << "{\"passed\":false,\"windowsError\":true}\n";
        return 1;
    }
}
