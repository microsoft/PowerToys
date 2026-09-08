// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#include "CaptureMirror.h"
#include "Geometry.h"
#include "SelectionOverlay.h"
#include "VirtualDisplay.h"

#include <commctrl.h>
#include <shellapi.h>
#include <winrt/base.h>
#include <algorithm>
#include <atomic>
#include <chrono>
#include <filesystem>
#include <fstream>
#include <optional>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>

namespace
{
    constexpr UINT DeviceReady = WM_APP + 1;
    constexpr UINT CaptureFailed = WM_APP + 2;
    constexpr UINT BeginAutomatic = WM_APP + 3;
    constexpr int SelectButton = 101;
    constexpr int StartButton = 102;
    constexpr int StopButton = 103;
    constexpr int StopHotkey = 1;
    constexpr wchar_t ControlClass[] = L"PowerToys.RegionMirror.Control";
    constexpr wchar_t OutputClass[] = L"PowerToys.RegionMirror.Output";
    constexpr wchar_t ViewportClass[] = L"PowerToys.RegionMirror.Viewport";

    LRESULT CALLBACK ViewportProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
    {
        if (message == WM_ERASEBKGND)
        {
            return 1;
        }
        if (message == WM_NCHITTEST)
        {
            return HTTRANSPARENT;
        }
        if (message == WM_PAINT)
        {
            PAINTSTRUCT paint{};
            BeginPaint(window, &paint);
            EndPaint(window, &paint);
            return 0;
        }
        return DefWindowProcW(window, message, wParam, lParam);
    }

    struct MonitorInfo
    {
        HMONITOR handle{};
        RECT bounds{};
        std::wstring name;
        std::wstring identity;
    };

    std::vector<MonitorInfo> Monitors()
    {
        std::vector<MonitorInfo> result;
        EnumDisplayMonitors(nullptr, nullptr, [](HMONITOR monitor, HDC, LPRECT, LPARAM parameter) -> BOOL {
            MONITORINFOEXW info{};
            info.cbSize = sizeof(info);
            if (GetMonitorInfoW(monitor, &info))
            {
                reinterpret_cast<std::vector<MonitorInfo>*>(parameter)->push_back({ monitor, info.rcMonitor, info.szDevice });
            }
            return TRUE; }, reinterpret_cast<LPARAM>(&result));
        // GDI names (DISPLAY1, DISPLAY2, ...) can change when an adapter arrives.
        // Keep the target's PnP interface path as the stable physical monitor identity.
        for (int attempt = 0; attempt < 3; ++attempt)
        {
            UINT pathCount{};
            UINT modeCount{};
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, &pathCount, &modeCount) != ERROR_SUCCESS)
            {
                break;
            }
            std::vector<DISPLAYCONFIG_PATH_INFO> paths(pathCount);
            std::vector<DISPLAYCONFIG_MODE_INFO> modes(modeCount);
            const auto query = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, &pathCount, paths.data(), &modeCount, modes.data(), nullptr);
            if (query == ERROR_INSUFFICIENT_BUFFER)
            {
                continue;
            }
            if (query != ERROR_SUCCESS)
            {
                break;
            }
            for (UINT index = 0; index < pathCount; ++index)
            {
                DISPLAYCONFIG_SOURCE_DEVICE_NAME source{};
                source.header = { DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME, sizeof(source), paths[index].sourceInfo.adapterId, paths[index].sourceInfo.id };
                DISPLAYCONFIG_TARGET_DEVICE_NAME target{};
                target.header = { DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME, sizeof(target), paths[index].targetInfo.adapterId, paths[index].targetInfo.id };
                if (DisplayConfigGetDeviceInfo(&source.header) != ERROR_SUCCESS ||
                    DisplayConfigGetDeviceInfo(&target.header) != ERROR_SUCCESS)
                {
                    continue;
                }
                for (auto& monitor : result)
                {
                    if (_wcsicmp(monitor.name.c_str(), source.viewGdiDeviceName) == 0)
                    {
                        monitor.identity = target.monitorDevicePath;
                    }
                }
            }
            break;
        }
        return result;
    }

    MonitorInfo FindMonitor(const std::wstring& name)
    {
        for (const auto& monitor : Monitors())
        {
            if (_wcsicmp(monitor.name.c_str(), name.c_str()) == 0)
            {
                return monitor;
            }
        }
        throw std::runtime_error("The selected display is no longer connected.");
    }

    MonitorInfo FindStableMonitor(const std::wstring& identity)
    {
        for (const auto& monitor : Monitors())
        {
            if (!identity.empty() && _wcsicmp(monitor.identity.c_str(), identity.c_str()) == 0)
            {
                return monitor;
            }
        }
        throw std::runtime_error("The selected monitor identity is not currently connected.");
    }

    std::wstring CurrentError()
    {
        try
        {
            throw;
        }
        catch (const winrt::hresult_error& error)
        {
            return error.message().c_str();
        }
        catch (const std::exception& error)
        {
            return winrt::to_hstring(error.what()).c_str();
        }
        catch (...)
        {
            return L"Unexpected error.";
        }
    }

    struct Options
    {
        std::optional<RECT> region;
        std::wstring targetDisplay;
        std::filesystem::path report;
        unsigned duration{};
        bool listDisplays{};
    };

    Options ParseOptions()
    {
        int count{};
        auto arguments = CommandLineToArgvW(GetCommandLineW(), &count);
        if (!arguments)
        {
            throw std::runtime_error("Unable to read command-line arguments.");
        }
        Options options;
        try
        {
            for (int i = 1; i < count; ++i)
            {
                const std::wstring_view option(arguments[i]);
                if (option == L"--list-displays")
                {
                    options.listDisplays = true;
                    continue;
                }
                if (++i >= count)
                {
                    throw std::runtime_error("An option is missing its value.");
                }
                const std::wstring_view value(arguments[i]);
                if (option == L"--region")
                {
                    RECT region{};
                    if (!RegionMirror::TryParseRegion(value, region))
                    {
                        throw std::runtime_error("Expected --region x,y,width,height, with positive dimensions up to 16384.");
                    }
                    options.region = region;
                }
                else if (option == L"--target-display")
                {
                    options.targetDisplay = value;
                }
                else if (option == L"--report")
                {
                    options.report = value;
                }
                else if (option == L"--duration")
                {
                    unsigned seconds{};
                    if (value.empty() || value.size() > 4)
                    {
                        throw std::runtime_error("Duration must be between 1 and 3600 seconds.");
                    }
                    for (const wchar_t character : value)
                    {
                        if (character < L'0' || character > L'9')
                        {
                            throw std::runtime_error("Duration must contain decimal digits.");
                        }
                        seconds = seconds * 10 + static_cast<unsigned>(character - L'0');
                    }
                    if (seconds == 0 || seconds > 3600)
                    {
                        throw std::runtime_error("Duration must be between 1 and 3600 seconds.");
                    }
                    options.duration = seconds;
                }
                else
                {
                    throw std::runtime_error("Unknown option. See the RegionMirror README for usage.");
                }
            }
        }
        catch (...)
        {
            LocalFree(arguments);
            throw;
        }
        LocalFree(arguments);
        if (options.listDisplays && options.report.empty())
        {
            throw std::runtime_error("--list-displays requires --report <path>.");
        }
        return options;
    }

    std::string JsonString(const std::wstring& text)
    {
        std::string result = "\"";
        for (const unsigned char value : winrt::to_string(text))
        {
            switch (value)
            {
            case '"':
                result += "\\\"";
                break;
            case '\\':
                result += "\\\\";
                break;
            case '\n':
                result += "\\n";
                break;
            case '\r':
                result += "\\r";
                break;
            case '\t':
                result += "\\t";
                break;
            default:
                if (value < 32)
                {
                    char escaped[7]{};
                    sprintf_s(escaped, "\\u%04x", value);
                    result += escaped;
                }
                else
                {
                    result += static_cast<char>(value);
                }
            }
        }
        return result + "\"";
    }

    void WriteMonitors(std::ostream& stream)
    {
        stream << "[";
        bool first = true;
        for (const auto& monitor : Monitors())
        {
            if (!first)
            {
                stream << ",";
            }
            first = false;
            stream << "{\"device\":" << JsonString(monitor.name) << ",\"identity\":" << JsonString(monitor.identity) << ",\"x\":" << monitor.bounds.left
                   << ",\"y\":" << monitor.bounds.top << ",\"width\":" << monitor.bounds.right - monitor.bounds.left
                   << ",\"height\":" << monitor.bounds.bottom - monitor.bounds.top << "}";
        }
        stream << "]";
    }

    // Both DXGI and display reconfiguration can send synchronous window messages.
    // Service sent messages while waiting; do not dispatch commands that could restart a session.
    void JoinWorker(std::thread& worker)
    {
        if (!worker.joinable())
        {
            return;
        }
        HANDLE handle = worker.native_handle();
        while (WaitForSingleObject(handle, 0) == WAIT_TIMEOUT)
        {
            const auto wait = MsgWaitForMultipleObjectsEx(1, &handle, INFINITE, QS_SENDMESSAGE, MWMO_INPUTAVAILABLE);
            if (wait == WAIT_OBJECT_0 + 1)
            {
                MSG sent{};
                PeekMessageW(&sent, nullptr, 0, 0, PM_NOREMOVE);
            }
            else if (wait != WAIT_OBJECT_0)
            {
                break;
            }
        }
        worker.join();
    }

    class Application
    {
    public:
        explicit Application(Options options) : m_options(std::move(options)) {}
        ~Application()
        {
            Stop();
            if (m_font)
            {
                DeleteObject(m_font);
            }
        }

        int Run(int show)
        {
            WNDCLASSW control{};
            control.hInstance = GetModuleHandleW(nullptr);
            control.lpfnWndProc = WindowProc;
            control.lpszClassName = ControlClass;
            control.hCursor = LoadCursorW(nullptr, IDC_ARROW);
            control.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
            winrt::check_bool(RegisterClassW(&control));
            WNDCLASSW output = control;
            output.lpszClassName = OutputClass;
            output.hbrBackground = static_cast<HBRUSH>(GetStockObject(BLACK_BRUSH));
            winrt::check_bool(RegisterClassW(&output));
            WNDCLASSW viewport = control;
            viewport.lpszClassName = ViewportClass;
            viewport.lpfnWndProc = ViewportProc;
            viewport.hbrBackground = nullptr;
            winrt::check_bool(RegisterClassW(&viewport));
            m_window = CreateWindowExW(0, ControlClass, L"Region Mirror - local PoC", WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX, CW_USEDEFAULT, CW_USEDEFAULT, 620, 360, nullptr, nullptr, GetModuleHandleW(nullptr), this);
            winrt::check_bool(m_window != nullptr);
            ShowWindow(m_window, show);
            if (m_options.region)
            {
                PostMessageW(m_window, BeginAutomatic, 0, 0);
            }
            MSG message{};
            while (GetMessageW(&message, nullptr, 0, 0) > 0)
            {
                if (!IsDialogMessageW(m_window, &message))
                {
                    TranslateMessage(&message);
                    DispatchMessageW(&message);
                }
            }
            return m_exitCode;
        }

    private:
        enum class Phase
        {
            Idle,
            Selecting,
            Creating,
            Mirroring,
            Stopping
        };
        Options m_options;
        HWND m_window{};
        HWND m_status{};
        HWND m_selectionText{};
        HWND m_selectButton{};
        HWND m_startButton{};
        HWND m_stopButton{};
        HWND m_output{};
        HWND m_viewport{};
        HWND m_border{};
        HFONT m_font{};
        bool m_hotkey{};
        Phase m_phase = Phase::Idle;
        std::optional<RegionMirror::Selection> m_selection;
        MonitorInfo m_source;
        MonitorInfo m_target;
        RegionMirror::VirtualDisplay m_virtual;
        RegionMirror::CaptureMirror m_capture;
        std::thread m_deviceWorker;
        std::atomic_bool m_cancel{};
        std::wstring m_deviceError;
        std::wstring m_error;
        std::wstring m_ownedInstance;
        ULONGLONG m_started{};
        uint64_t m_finalFrames{};
        int m_exitCode{};
        bool m_reportWritten{};
        bool m_captureStarted{};

        void SetStatus(const std::wstring& text) { SetWindowTextW(m_status, text.c_str()); }

        HWND AddControl(const wchar_t* className, const wchar_t* text, DWORD style, int id, int x, int y, int width, int height)
        {
            const UINT dpi = GetDpiForWindow(m_window);
            const auto scale = [dpi](int value) { return MulDiv(value, static_cast<int>(dpi), 96); };
            auto control = CreateWindowExW(0, className, text, WS_CHILD | WS_VISIBLE | style, scale(x), scale(y), scale(width), scale(height), m_window, reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)), GetModuleHandleW(nullptr), nullptr);
            winrt::check_bool(control != nullptr);
            SendMessageW(control, WM_SETFONT, reinterpret_cast<WPARAM>(m_font), TRUE);
            return control;
        }

        void CreateControls()
        {
            const UINT dpi = GetDpiForWindow(m_window);
            m_font = CreateFontW(-MulDiv(15, static_cast<int>(dpi), 96), 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH, L"Segoe UI");
            RECT client{ 0, 0, MulDiv(590, static_cast<int>(dpi), 96), MulDiv(285, static_cast<int>(dpi), 96) };
            AdjustWindowRectExForDpi(&client, WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX, FALSE, 0, dpi);
            SetWindowPos(m_window, nullptr, 0, 0, client.right - client.left, client.bottom - client.top, SWP_NOMOVE | SWP_NOZORDER);
            AddControl(L"STATIC", L"Share a screen region through a temporary virtual monitor", SS_LEFT, 0, 20, 18, 550, 40);
            m_selectionText = AddControl(L"STATIC", L"1. Select a region on one physical screen.", SS_LEFT, 0, 20, 66, 550, 32);
            m_selectButton = AddControl(L"BUTTON", L"Select region", BS_PUSHBUTTON | WS_TABSTOP, SelectButton, 20, 108, 145, 36);
            m_startButton = AddControl(L"BUTTON", L"Start mirror", BS_DEFPUSHBUTTON | WS_TABSTOP, StartButton, 180, 108, 145, 36);
            m_stopButton = AddControl(L"BUTTON", L"Stop", BS_PUSHBUTTON | WS_TABSTOP, StopButton, 340, 108, 110, 36);
            m_status = AddControl(L"STATIC", L"Ready. The virtual display driver must be staged before Start.\r\nLaunch elevated to create the temporary software device.", SS_LEFT, 0, 20, 161, 550, 78);
            m_hotkey = RegisterHotKey(m_window, StopHotkey, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 'Q') != FALSE;
            AddControl(L"STATIC", m_hotkey ? L"Stop at any time: Ctrl+Alt+Q. Closing this window also cleans up." : L"Ctrl+Alt+Q is in use. Use Stop or close this window to clean up.", SS_LEFT, 0, 20, 247, 550, 25);
            UpdateButtons();
            SetTimer(m_window, 1, 250, nullptr);
        }

        void UpdateButtons()
        {
            const bool idle = m_phase == Phase::Idle;
            EnableWindow(m_selectButton, idle);
            EnableWindow(m_startButton, idle && m_selection.has_value());
            EnableWindow(m_stopButton, m_phase == Phase::Creating || m_phase == Phase::Mirroring);
        }

        void SetSelection(RegionMirror::Selection selection)
        {
            if (!RegionMirror::IsValidRegion(selection.region))
            {
                throw std::runtime_error("The selected region is empty or too large.");
            }
            for (const auto& monitor : Monitors())
            {
                if (monitor.handle == selection.monitor && RegionMirror::IsContained(selection.region, monitor.bounds))
                {
                    m_source = monitor;
                    m_selection = selection;
                    const auto& region = selection.region;
                    const auto label = L"Region: " + std::to_wstring(region.right - region.left) + L" x " +
                                       std::to_wstring(region.bottom - region.top) + L" at (" + std::to_wstring(region.left) +
                                       L", " + std::to_wstring(region.top) + L")";
                    SetWindowTextW(m_selectionText, label.c_str());
                    UpdateButtons();
                    return;
                }
            }
            throw std::runtime_error("The region must be contained in one connected source display.");
        }

        void Select()
        {
            if (m_phase != Phase::Idle)
            {
                return;
            }
            m_phase = Phase::Selecting;
            ShowWindow(m_window, SW_HIDE);
            try
            {
                const auto selection = RegionMirror::SelectRegion(m_window);
                m_phase = Phase::Idle;
                ShowWindow(m_window, SW_SHOW);
                SetForegroundWindow(m_window);
                if (selection)
                {
                    SetSelection(*selection);
                    SetStatus(L"Region selected. Start mirror creates a virtual display and begins capture.");
                }
                else
                {
                    SetStatus(L"Selection cancelled.");
                }
            }
            catch (...)
            {
                m_phase = Phase::Idle;
                ShowWindow(m_window, SW_SHOW);
                throw;
            }
            UpdateButtons();
        }

        void Start()
        {
            if (m_phase != Phase::Idle || !m_selection)
            {
                return;
            }
            m_error.clear();
            m_ownedInstance.clear();
            m_finalFrames = 0;
            m_reportWritten = false;
            m_captureStarted = false;
            m_exitCode = 0;
            m_cancel.store(false);
            m_deviceError.clear();
            m_phase = Phase::Creating;
            UpdateButtons();
            SetStatus(L"Creating the temporary virtual monitor...");
            const auto selectedSource = m_source;
            if (selectedSource.identity.empty())
            {
                throw std::runtime_error("Windows did not provide a stable identity for the selected source monitor.");
            }
            const int width = m_selection->region.right - m_selection->region.left;
            const int height = m_selection->region.bottom - m_selection->region.top;
            m_deviceWorker = std::thread([this, width, height, selectedSource] {
                try
                {
                    if (m_options.targetDisplay.empty())
                    {
                        m_virtual.Create(width, height, m_cancel);
                        m_target = { m_virtual.Monitor(), m_virtual.Bounds(), m_virtual.DeviceName() };
                        m_ownedInstance = m_virtual.InstanceId();
                    }
                    else
                    {
                        // Explicit diagnostic mode. Never selected implicitly and never detached on Stop.
                        m_target = FindMonitor(m_options.targetDisplay);
                    }
                    WaitForStableDisplays(selectedSource);
                }
                catch (...)
                {
                    m_deviceError = CurrentError();
                }
                PostMessageW(m_window, DeviceReady, 0, 0);
            });
        }

        void WaitForStableDisplays(const MonitorInfo& selectedSource)
        {
            const auto deadline = GetTickCount64() + 15000;
            ULONGLONG stableSince{};
            MonitorInfo previousSource;
            MonitorInfo previousTarget;
            while (GetTickCount64() < deadline && !m_cancel.load())
            {
                const auto targetName = m_options.targetDisplay.empty() ? m_virtual.DeviceName() : m_target.name;
                const auto displays = Monitors();
                std::optional<MonitorInfo> source;
                std::optional<MonitorInfo> target;
                for (const auto& display : displays)
                {
                    if (!display.identity.empty() && _wcsicmp(display.identity.c_str(), selectedSource.identity.c_str()) == 0)
                    {
                        source = display;
                    }
                    if (!targetName.empty() && _wcsicmp(display.name.c_str(), targetName.c_str()) == 0 && !display.identity.empty())
                    {
                        target = display;
                    }
                }
                if (source && target && EqualRect(&source->bounds, &selectedSource.bounds) &&
                    RegionMirror::IsValidRegion(target->bounds))
                {
                    const bool same = source->handle == previousSource.handle && target->handle == previousTarget.handle &&
                                      source->identity == previousSource.identity && target->identity == previousTarget.identity &&
                                      EqualRect(&source->bounds, &previousSource.bounds) && EqualRect(&target->bounds, &previousTarget.bounds);
                    if (!same || !stableSince)
                    {
                        stableSince = GetTickCount64();
                    }
                    previousSource = *source;
                    previousTarget = *target;
                    if (GetTickCount64() - stableSince >= 1000)
                    {
                        m_source = *source;
                        m_target = *target;
                        return;
                    }
                }
                else
                {
                    stableSince = 0;
                }
                std::this_thread::sleep_for(std::chrono::milliseconds(100));
            }
            if (!m_cancel.load())
            {
                throw std::runtime_error("The original source monitor and owned target did not settle within 15 seconds. Select the region again after the display layout stabilizes.");
            }
        }

        void BeginCapture()
        {
            JoinWorker(m_deviceWorker);
            if (m_phase != Phase::Creating || m_cancel.load())
            {
                return;
            }
            if (!m_deviceError.empty())
            {
                Fail(m_deviceError);
                return;
            }
            auto currentSource = FindStableMonitor(m_source.identity);
            if (!EqualRect(&currentSource.bounds, &m_source.bounds))
            {
                throw std::runtime_error("The source display moved during device creation. Select the region again.");
            }
            m_source = currentSource;
            m_target = FindStableMonitor(m_target.identity);
            RECT overlap{};
            if (m_source.handle == m_target.handle || IntersectRect(&overlap, &m_selection->region, &m_target.bounds))
            {
                throw std::runtime_error("The output display must be separate from the selected source region.");
            }
            m_output = CreateWindowExW(WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
                                       OutputClass,
                                       L"Region Mirror output",
                                       WS_POPUP | WS_CLIPCHILDREN,
                                       m_target.bounds.left,
                                       m_target.bounds.top,
                                       m_target.bounds.right - m_target.bounds.left,
                                       m_target.bounds.bottom - m_target.bounds.top,
                                       nullptr,
                                       nullptr,
                                       GetModuleHandleW(nullptr),
                                       this);
            winrt::check_bool(m_output != nullptr);
            RECT client{};
            GetClientRect(m_output, &client);
            const auto fitted = RegionMirror::FitAspect(m_selection->region, client);
            if (!RegionMirror::IsValidRegion(fitted))
            {
                throw std::runtime_error("The region cannot fit the target display.");
            }
            m_viewport = CreateWindowExW(0, ViewportClass, L"", WS_CHILD | WS_VISIBLE, fitted.left, fitted.top, fitted.right - fitted.left, fitted.bottom - fitted.top, m_output, nullptr, GetModuleHandleW(nullptr), nullptr);
            winrt::check_bool(m_viewport != nullptr);
            ShowWindow(m_output, SW_SHOWNOACTIVATE);
            m_border = RegionMirror::CreateRegionBorder(m_selection->region);
            SetWindowDisplayAffinity(m_window, WDA_EXCLUDEFROMCAPTURE);
            m_capture.Start(m_viewport, m_source.handle, m_selection->region, m_window, CaptureFailed);
            m_captureStarted = true;
            m_started = GetTickCount64();
            m_phase = Phase::Mirroring;
            UpdateButtons();
            Tick();
        }

        void Stop() noexcept
        {
            if (m_phase == Phase::Stopping)
            {
                return;
            }
            m_phase = Phase::Stopping;
            m_cancel.store(true);
            JoinWorker(m_deviceWorker);
            m_capture.Stop();
            if (m_window)
            {
                SetWindowDisplayAffinity(m_window, WDA_NONE);
            }
            if (m_captureStarted)
            {
                m_finalFrames = m_capture.FramesPresented();
                m_captureStarted = false;
            }
            if (m_border)
            {
                DestroyWindow(m_border);
                m_border = nullptr;
            }
            if (m_output)
            {
                DestroyWindow(m_output);
                m_output = nullptr;
                m_viewport = nullptr;
            }
            m_virtual.Close();
            MSG stale{};
            while (m_window && PeekMessageW(&stale, m_window, DeviceReady, DeviceReady, PM_REMOVE))
            {
            }
            m_phase = Phase::Idle;
            UpdateButtons();
        }

        void Fail(const std::wstring& message)
        {
            m_error = message;
            m_exitCode = 1;
            Stop();
            SetStatus(L"Stopped: " + message);
            if (m_options.region)
            {
                PostMessageW(m_window, WM_CLOSE, 0, 0);
            }
        }

        void Tick()
        {
            if (m_phase != Phase::Mirroring)
            {
                return;
            }
            const auto frames = m_capture.FramesPresented();
            const auto elapsed = GetTickCount64() - m_started;
            if (!frames && elapsed > 10000)
            {
                Fail(L"No capture frames arrived within 10 seconds.");
                return;
            }
            const auto target = FindStableMonitor(m_target.identity);
            if (!EqualRect(&target.bounds, &m_target.bounds))
            {
                Fail(L"The output display changed. Restart mirroring.");
                return;
            }
            SetStatus(L"Mirroring to " + m_target.name + L" (" +
                      std::to_wstring(m_target.bounds.right - m_target.bounds.left) + L" x " +
                      std::to_wstring(m_target.bounds.bottom - m_target.bounds.top) + L").\r\n" +
                      L"Select this screen in your meeting app. Frames presented: " + std::to_wstring(frames));
            if (m_options.duration && elapsed >= static_cast<ULONGLONG>(m_options.duration) * 1000)
            {
                if (!frames)
                {
                    Fail(L"Capture ended without presenting a frame.");
                }
                else
                {
                    PostMessageW(m_window, WM_CLOSE, 0, 0);
                }
            }
        }

        void Report()
        {
            if (m_options.report.empty() || m_reportWritten)
            {
                return;
            }
            std::ofstream stream(m_options.report, std::ios::binary);
            if (!stream)
            {
                throw std::runtime_error("Unable to write the requested report file.");
            }
            stream << "{\"exitCode\":" << m_exitCode << ",\"framesPresented\":" << m_finalFrames
                   << ",\"sourceDisplay\":" << JsonString(m_source.name)
                   << ",\"sourceIdentity\":" << JsonString(m_source.identity)
                   << ",\"targetDisplay\":" << JsonString(m_target.name)
                   << ",\"targetIdentity\":" << JsonString(m_target.identity)
                   << ",\"ownedDeviceInstance\":" << JsonString(m_ownedInstance)
                   << ",\"error\":" << JsonString(m_error) << ",\"displaysAfterStop\":";
            WriteMonitors(stream);
            stream << "}\n";
            stream.flush();
            if (!stream)
            {
                throw std::runtime_error("Unable to finish writing the report file.");
            }
            m_reportWritten = true;
        }

        LRESULT Dispatch(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
        {
            if (window != m_window)
            {
                if (message == WM_CLOSE)
                {
                    PostMessageW(m_window, WM_CLOSE, 0, 0);
                    return 0;
                }
                if (message == WM_NCHITTEST)
                {
                    return HTTRANSPARENT;
                }
                return DefWindowProcW(window, message, wParam, lParam);
            }
            switch (message)
            {
            case WM_CREATE:
                CreateControls();
                return 0;
            case WM_COMMAND:
                if (LOWORD(wParam) == SelectButton)
                {
                    Select();
                }
                else if (LOWORD(wParam) == StartButton)
                {
                    Start();
                }
                else if (LOWORD(wParam) == StopButton)
                {
                    Stop();
                    SetStatus(L"Stopped. The owned virtual device was released.");
                }
                return 0;
            case BeginAutomatic:
                SetSelection({ *m_options.region, MonitorFromRect(&*m_options.region, MONITOR_DEFAULTTONULL) });
                Start();
                return 0;
            case DeviceReady:
                BeginCapture();
                return 0;
            case CaptureFailed:
                Fail(m_capture.Error());
                return 0;
            case WM_HOTKEY:
                if (wParam == StopHotkey)
                {
                    Stop();
                    SetStatus(L"Stopped with Ctrl+Alt+Q.");
                }
                return 0;
            case WM_TIMER:
                Tick();
                return 0;
            case WM_DPICHANGED:
                // Use the suggested physical bounds; child controls retain the PoC's initial DPI sizing.
                if (const auto suggested = reinterpret_cast<RECT*>(lParam))
                {
                    SetWindowPos(window, nullptr, suggested->left, suggested->top, suggested->right - suggested->left, suggested->bottom - suggested->top, SWP_NOZORDER);
                }
                return 0;
            case WM_QUERYENDSESSION:
                return TRUE;
            case WM_ENDSESSION:
                if (wParam)
                {
                    Stop();
                }
                return 0;
            case WM_CLOSE:
                Stop();
                try
                {
                    Report();
                }
                catch (...)
                {
                    m_exitCode = 1;
                    OutputDebugStringW(CurrentError().c_str());
                }
                DestroyWindow(window);
                return 0;
            case WM_DESTROY:
                KillTimer(window, 1);
                if (m_hotkey)
                {
                    UnregisterHotKey(window, StopHotkey);
                }
                PostQuitMessage(m_exitCode);
                return 0;
            default:
                return DefWindowProcW(window, message, wParam, lParam);
            }
        }

        static LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
        {
            auto app = reinterpret_cast<Application*>(GetWindowLongPtrW(window, GWLP_USERDATA));
            if (message == WM_NCCREATE)
            {
                app = static_cast<Application*>(reinterpret_cast<CREATESTRUCTW*>(lParam)->lpCreateParams);
                SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(app));
                // The control is the first window created; its WM_CREATE needs the HWND immediately.
                if (!app->m_window)
                {
                    app->m_window = window;
                }
            }
            if (!app)
            {
                return DefWindowProcW(window, message, wParam, lParam);
            }
            try
            {
                return app->Dispatch(window, message, wParam, lParam);
            }
            catch (...)
            {
                app->Fail(CurrentError());
                return message == WM_CREATE ? -1 : 0;
            }
        }
    };
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int show)
{
    try
    {
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        INITCOMMONCONTROLSEX controls{ sizeof(controls), ICC_STANDARD_CLASSES };
        InitCommonControlsEx(&controls);
        auto options = ParseOptions();
        if (options.listDisplays)
        {
            std::ofstream stream(options.report, std::ios::binary);
            if (!stream)
            {
                throw std::runtime_error("Unable to write display diagnostics.");
            }
            WriteMonitors(stream);
            stream << "\n";
            return stream ? 0 : 1;
        }
        Application application(std::move(options));
        return application.Run(show);
    }
    catch (...)
    {
        const auto message = CurrentError();
        MessageBoxW(nullptr, message.c_str(), L"Region Mirror", MB_OK | MB_ICONERROR);
        return 1;
    }
}
