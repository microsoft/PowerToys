// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "pch.h"
#include "VirtualMirrorSession.h"

#include <algorithm>
#include <chrono>
#include <exception>
#include <stdexcept>
#include <vector>

namespace
{
    using namespace zoomit_mirror;
    using Clock = std::chrono::steady_clock;
    constexpr auto LayoutTimeout = std::chrono::seconds(5);
    constexpr auto StableLayoutDuration = std::chrono::milliseconds(500);
    constexpr auto PollInterval = std::chrono::milliseconds(100);
    constexpr int RequestedWidth = 1920;
    constexpr int RequestedHeight = 1080;

    struct MonitorEnumeration
    {
        std::vector<MonitorSnapshot> monitors;
        std::exception_ptr failure;
    };

    BOOL CALLBACK CollectMonitor(HMONITOR monitor, HDC, LPRECT, LPARAM parameter)
    {
        auto& enumeration = *reinterpret_cast<MonitorEnumeration*>(parameter);
        try
        {
            MONITORINFOEXW info{};
            info.cbSize = sizeof(info);
            if (!GetMonitorInfoW(monitor, &info))
            {
                throw std::runtime_error("The display layout changed while ZoomIt was reading it.");
            }
            enumeration.monitors.push_back({ monitor, info.rcMonitor, info.szDevice, {} });
            return TRUE;
        }
        catch (...)
        {
            // Exceptions must not unwind through the Win32 enumeration callback.
            enumeration.failure = std::current_exception();
            return FALSE;
        }
    }

    std::vector<MonitorSnapshot> ReadMonitors()
    {
        MonitorEnumeration enumeration;
        const bool enumerated = EnumDisplayMonitors(nullptr, nullptr, CollectMonitor, reinterpret_cast<LPARAM>(&enumeration)) != FALSE;
        if (enumeration.failure)
        {
            std::rethrow_exception(enumeration.failure);
        }
        if (!enumerated || enumeration.monitors.empty())
        {
            throw std::runtime_error("ZoomIt could not read the connected displays.");
        }

        for (int attempt = 0; attempt < 3; ++attempt)
        {
            UINT pathCount = 0;
            UINT modeCount = 0;
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, &pathCount, &modeCount) != ERROR_SUCCESS)
            {
                break;
            }
            std::vector<DISPLAYCONFIG_PATH_INFO> paths(pathCount);
            std::vector<DISPLAYCONFIG_MODE_INFO> modes(modeCount);
            const LONG result = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, &pathCount, paths.data(), &modeCount, modes.data(), nullptr);
            if (result == ERROR_INSUFFICIENT_BUFFER)
            {
                continue;
            }
            if (result != ERROR_SUCCESS)
            {
                break;
            }

            std::vector<std::vector<std::wstring>> identities(enumeration.monitors.size());
            for (UINT index = 0; index < pathCount; ++index)
            {
                DISPLAYCONFIG_SOURCE_DEVICE_NAME source{};
                source.header = { DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME, sizeof(source), paths[index].sourceInfo.adapterId, paths[index].sourceInfo.id };
                DISPLAYCONFIG_TARGET_DEVICE_NAME target{};
                target.header = { DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME, sizeof(target), paths[index].targetInfo.adapterId, paths[index].targetInfo.id };
                if (DisplayConfigGetDeviceInfo(&source.header) != ERROR_SUCCESS ||
                    DisplayConfigGetDeviceInfo(&target.header) != ERROR_SUCCESS || !target.monitorDevicePath[0])
                {
                    throw std::runtime_error("ZoomIt could not verify the identities of the connected displays.");
                }
                for (size_t monitorIndex = 0; monitorIndex < enumeration.monitors.size(); ++monitorIndex)
                {
                    if (_wcsicmp(enumeration.monitors[monitorIndex].deviceName.c_str(), source.viewGdiDeviceName) == 0)
                    {
                        identities[monitorIndex].push_back(target.monitorDevicePath);
                    }
                }
            }
            for (size_t index = 0; index < enumeration.monitors.size(); ++index)
            {
                auto& targets = identities[index];
                std::sort(targets.begin(), targets.end(), [](const auto& first, const auto& second) {
                    return _wcsicmp(first.c_str(), second.c_str()) < 0;
                });
                for (const auto& target : targets)
                {
                    enumeration.monitors[index].identity += target;
                    enumeration.monitors[index].identity += L'\n';
                }

                // Enumeration and QueryDisplayConfig are separate snapshots.
                // Reject a GDI renumbering or move that occurred between them.
                const auto& monitor = enumeration.monitors[index];
                MONITORINFOEXW current{};
                current.cbSize = sizeof(current);
                if (!GetMonitorInfoW(monitor.monitor, &current) ||
                    _wcsicmp(monitor.deviceName.c_str(), current.szDevice) != 0 ||
                    !AreSameBounds(monitor.bounds, current.rcMonitor))
                {
                    throw std::runtime_error("The display layout changed while ZoomIt was reading it.");
                }
            }
            return enumeration.monitors;
        }
        throw std::runtime_error("ZoomIt could not verify the current display configuration.");
    }

    const MonitorSnapshot* FindIdentity(const std::vector<MonitorSnapshot>& monitors, const MonitorSnapshot& expected)
    {
        const auto found = std::find_if(monitors.begin(), monitors.end(), [&](const auto& current) {
            return IsSameMonitorIdentity(expected, current);
        });
        return found == monitors.end() ? nullptr : &*found;
    }

    const MonitorSnapshot* FindDeviceName(const std::vector<MonitorSnapshot>& monitors, const std::wstring& deviceName)
    {
        const auto found = std::find_if(monitors.begin(), monitors.end(), [&](const auto& current) {
            return _wcsicmp(deviceName.c_str(), current.deviceName.c_str()) == 0;
        });
        return found == monitors.end() ? nullptr : &*found;
    }

    void DescribeException(std::wstring& error) noexcept
    {
        try
        {
            try
            {
                throw;
            }
            catch (const winrt::hresult_error& exception)
            {
                error = exception.message().c_str();
            }
            catch (const std::exception& exception)
            {
                // The native failures in this component use ASCII diagnostics.
                const std::string message = exception.what();
                error.assign(message.begin(), message.end());
            }
            catch (...)
            {
                error = L"ZoomIt could not create the temporary mirror display.";
            }
        }
        catch (...)
        {
            // Resource cleanup must still run if even allocating an error fails.
            error.clear();
        }
    }
}

namespace zoomit_mirror
{
    VirtualMirrorSession::~VirtualMirrorSession()
    {
        Close();
    }

    bool VirtualMirrorSession::Begin(HWND notify, HMONITOR source, RECT screenRegion, std::wstring& error)
    {
        error.clear();
        if (m_pending || m_active || m_closing || m_worker.joinable())
        {
            error = L"A temporary mirror display is already starting or active.";
            return false;
        }
        try
        {
            if (!IsWindow(notify))
            {
                throw std::runtime_error("The ZoomIt window is no longer available.");
            }
            const auto monitors = ReadMonitors();
            const auto selected = std::find_if(monitors.begin(), monitors.end(), [source](const auto& current) {
                return current.monitor == source;
            });
            if (selected == monitors.end() || selected->identity.empty() ||
                !IsRegionWithinMonitor(screenRegion, selected->bounds))
            {
                throw std::runtime_error("Select a region contained within one connected display and try again.");
            }

            m_originalSource = *selected;
            m_region = screenRegion;
            m_notify = notify;
            m_created = false;
            m_creationError.clear();
            m_cancel.store(false);
            m_pending = true;
            m_worker = std::thread(&VirtualMirrorSession::CreateDisplay, this);
            return true;
        }
        catch (...)
        {
            DescribeException(error);
            Close();
            return false;
        }
    }

    void VirtualMirrorSession::CreateDisplay() noexcept
    {
        try
        {
            if (m_display.Create(RequestedWidth, RequestedHeight, m_creationError, m_cancel))
            {
                const auto targetDeviceName = m_display.DeviceName();
                const auto deadline = Clock::now() + LayoutTimeout;
                auto stableSince = Clock::time_point{};
                MonitorSnapshot previousSource;
                MonitorSnapshot previousTarget;
                MonitorSnapshot ownedTarget;
                while (!m_cancel.load() && Clock::now() < deadline)
                {
                    try
                    {
                        const auto monitors = ReadMonitors();
                        const auto source = FindIdentity(monitors, m_originalSource);
                        const auto target = ownedTarget.identity.empty() ? FindDeviceName(monitors, targetDeviceName) : FindIdentity(monitors, ownedTarget);
                        if (ownedTarget.identity.empty() && target)
                        {
                            // Bind the backend's freshly created display to its
                            // PnP identity before allowing any GDI renumbering.
                            if (target->monitor != m_display.Monitor() || !AreSameBounds(target->bounds, m_display.Bounds()))
                            {
                                throw std::runtime_error("The temporary display changed before its identity could be verified.");
                            }
                            ownedTarget = *target;
                        }
                        if (source && target && IsMirrorLayoutValid(m_originalSource, *source, *target, m_region))
                        {
                            const auto now = Clock::now();
                            if (stableSince == Clock::time_point{} ||
                                !IsSameMonitorSnapshot(previousSource, *source) || !IsSameMonitorSnapshot(previousTarget, *target))
                            {
                                previousSource = *source;
                                previousTarget = *target;
                                stableSince = now;
                            }
                            else if (now - stableSince >= StableLayoutDuration)
                            {
                                m_createdTarget = *target;
                                m_created = true;
                                break;
                            }
                        }
                        else
                        {
                            stableSince = Clock::time_point{};
                        }
                    }
                    catch (...)
                    {
                        // PnP can temporarily leave enumeration inconsistent.
                        // Require a complete, unchanged layout before reporting ready.
                        stableSince = Clock::time_point{};
                    }
                    std::this_thread::sleep_for(PollInterval);
                }
                if (!m_created)
                {
                    m_creationError = m_cancel.load() ? L"Temporary display creation was cancelled." :
                        L"The selected display moved, disconnected, or did not settle after adding the temporary display. Select the region again.";
                }
            }
        }
        catch (...)
        {
            m_created = false;
            DescribeException(m_creationError);
        }

        if (m_cancel.load() || !m_created)
        {
            m_created = false;
            m_display.Close();
        }
        if (!m_cancel.load() && !PostMessageW(m_notify, WM_USER_MIRROR_DEVICE_READY, 0, 0))
        {
            // No UI continuation will take ownership if its notification fails.
            m_display.Close();
            m_created = false;
        }
    }

    bool VirtualMirrorSession::Complete(std::wstring& error)
    {
        error.clear();
        if (!m_pending)
        {
            return false;
        }
        try
        {
            m_worker.join();
            m_pending = false;
            if (!m_created || m_cancel.load())
            {
                error = m_creationError;
                Close();
                return false;
            }

            // HMONITOR values obtained before PnP, or on the creating worker,
            // must not be handed directly to Windows Graphics Capture.
            const auto monitors = ReadMonitors();
            const auto source = FindIdentity(monitors, m_originalSource);
            const auto target = FindIdentity(monitors, m_createdTarget);
            if (!source || !target || !IsMirrorLayoutValid(m_originalSource, *source, *target, m_region) ||
                !IsSameMonitorPlacement(m_createdTarget, *target))
            {
                throw std::runtime_error("The display layout changed before mirroring could start. Select the region again.");
            }
            m_source = *source;
            m_target = *target;
            m_active = true;
            return true;
        }
        catch (...)
        {
            DescribeException(error);
            Close();
            return false;
        }
    }

    void VirtualMirrorSession::Close() noexcept
    {
        if (m_closing)
        {
            return;
        }
        m_closing = true;
        m_pending = false;
        m_active = false;
        m_cancel.store(true);
        if (m_worker.joinable())
        {
            m_worker.join();
        }
        m_display.Close();

        // Join first: once drained, no old worker can enqueue another completion
        // for a subsequent request using this same notification window.
        if (m_notify)
        {
            MSG message{};
            bool quitReceived = false;
            int exitCode = 0;
            while (PeekMessageW(&message, m_notify, WM_USER_MIRROR_DEVICE_READY, WM_USER_MIRROR_DEVICE_READY, PM_REMOVE))
            {
                // WM_QUIT bypasses PeekMessage's message range filter.
                if (message.message == WM_QUIT)
                {
                    quitReceived = true;
                    exitCode = static_cast<int>(message.wParam);
                }
            }
            if (quitReceived)
            {
                PostQuitMessage(exitCode);
            }
        }
        m_notify = nullptr;
        m_pending = false;
        m_active = false;
        m_created = false;
        m_region = {};
        m_originalSource = {};
        m_source = {};
        m_target = {};
        m_createdTarget = {};
        m_creationError.clear();
        m_closing = false;
    }

    bool VirtualMirrorSession::IsPending() const noexcept
    {
        return m_pending;
    }

    bool VirtualMirrorSession::IsActive() const noexcept
    {
        return m_active;
    }

    bool VirtualMirrorSession::IsLayoutCurrent() const noexcept
    {
        if (!m_active)
        {
            return false;
        }
        try
        {
            const auto monitors = ReadMonitors();
            const auto source = FindIdentity(monitors, m_source);
            const auto target = FindIdentity(monitors, m_target);
            return source && target && IsSameMonitorSnapshot(m_source, *source) &&
                   IsSameMonitorSnapshot(m_target, *target) &&
                   IsMirrorLayoutValid(m_originalSource, *source, *target, m_region);
        }
        catch (...)
        {
            return false;
        }
    }

    HMONITOR VirtualMirrorSession::SourceMonitor() const noexcept
    {
        return m_active ? m_source.monitor : nullptr;
    }

    HMONITOR VirtualMirrorSession::TargetMonitor() const noexcept
    {
        return m_active ? m_target.monitor : nullptr;
    }

    RECT VirtualMirrorSession::Region() const noexcept
    {
        return m_region;
    }
}
