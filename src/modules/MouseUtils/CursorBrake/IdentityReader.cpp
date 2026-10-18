// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "IdentityReader.h"

#include "CursorBrakeEdges.h"
#include "DisplayNames.h"

#include <common/Display/DisplayUtils.h>
#include <common/logger/logger.h>

#include <algorithm>
#include <chrono>

namespace cursorbrake
{
    IdentityReader::IdentityReader()
    {
        m_work = CreateThreadpoolWork(Run, this, nullptr);
        if (!m_work)
        {
            Logger::error(L"Failed to create the CursorBrake identity reader work item, error: {}. Monitor identities are read on the hook thread.", GetLastError());
        }
    }

    IdentityReader::~IdentityReader()
    {
        if (m_work)
        {
            // The callbacks run this object's code, so none may outlive it.
            WaitForThreadpoolWorkCallbacks(m_work, TRUE);
            CloseThreadpoolWork(m_work);
        }
    }

    bool IdentityReader::Request(uint64_t generation, std::vector<HMONITOR> handles, std::vector<std::wstring> devices, HWND window, UINT message)
    {
        if (!m_work || !window)
        {
            Identities result = TryRead(generation, handles, devices);
            std::lock_guard lock(m_mutex);
            m_result = std::move(result);
            return true;
        }
        {
            std::lock_guard lock(m_mutex);
            m_pending = Pending{ generation, std::move(handles), std::move(devices), window, message };
        }
        SubmitThreadpoolWork(m_work);
        return false;
    }

    std::optional<Identities> IdentityReader::Take()
    {
        std::lock_guard lock(m_mutex);
        std::optional<Identities> result;
        result.swap(m_result);
        return result;
    }

    void IdentityReader::Cancel()
    {
        {
            std::lock_guard lock(m_mutex);
            m_pending.reset();
        }
        if (m_work)
        {
            WaitForThreadpoolWorkCallbacks(m_work, TRUE);
        }
        std::lock_guard lock(m_mutex);
        m_result.reset();
    }

    void CALLBACK IdentityReader::Run(PTP_CALLBACK_INSTANCE /*instance*/, PVOID context, PTP_WORK /*work*/)
    {
        static_cast<IdentityReader*>(context)->ReadPending();
    }

    void IdentityReader::ReadPending()
    {
        std::lock_guard readLock(m_readMutex);
        std::optional<Pending> pending;
        {
            std::lock_guard lock(m_mutex);
            pending.swap(m_pending);
        }
        if (!pending)
        {
            return;
        }

        // Thread-pool threads are shared, so the awareness the hook thread uses is set for this
        // read only.
        const DPI_AWARENESS_CONTEXT previous = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        Identities result = TryRead(pending->generation, pending->handles, pending->devices);
        if (previous)
        {
            SetThreadDpiAwarenessContext(previous);
        }

        {
            std::lock_guard lock(m_mutex);
            m_result = std::move(result);
        }
        PostMessageW(pending->window, pending->message, 0, 0);
    }

    Identities IdentityReader::TryRead(uint64_t generation, const std::vector<HMONITOR>& handles, const std::vector<std::wstring>& devices)
    {
        try
        {
            return Read(generation, handles, devices);
        }
        catch (...)
        {
            // catch(...) because the display helpers can throw std::bad_alloc and
            // winrt::hresult_error alike; the monitors keep the identities they have, and the
            // incomplete result has the hook thread try again.
            Logger::error(L"Failed to read the CursorBrake monitor identities.");
            Identities failed;
            failed.generation = generation;
            failed.complete = false;
            return failed;
        }
    }

    Identities IdentityReader::Read(uint64_t generation, const std::vector<HMONITOR>& handles, const std::vector<std::wstring>& devices)
    {
        // GetDisplays waits on a helper thread once per monitor, so its cost is logged on every read.
        const auto started = std::chrono::steady_clock::now();
        const auto [complete, displays] = DisplayUtils::GetDisplays();
        const long long elapsed = std::chrono::duration_cast<std::chrono::microseconds>(std::chrono::steady_clock::now() - started).count();
        Logger::info(L"CursorBrake read {} display identities in {} us{}.", displays.size(), elapsed, complete ? L"" : L", some incomplete");
        const auto configStarted = std::chrono::steady_clock::now();
        const DisplayConfiguration configuration = GetDisplayConfiguration();
        const long long configElapsed = std::chrono::duration_cast<std::chrono::microseconds>(std::chrono::steady_clock::now() - configStarted).count();
        Logger::info(L"CursorBrake read the display configuration, {} active paths and {} available targets, in {} us.", configuration.active.size(), configuration.available.size(), configElapsed);

        // Each monitor's path is the first active one showing its GDI device. Device names are
        // ASCII, so the comparison folds ASCII case only.
        std::vector<const DisplayPath*> monitorPaths(handles.size(), nullptr);
        std::vector<std::optional<DisplayTarget>> targets(handles.size());
        for (size_t i = 0; i < handles.size() && i < devices.size(); ++i)
        {
            for (const DisplayPath& path : configuration.active)
            {
                if (!path.source.empty() && _wcsicmp(path.source.c_str(), devices[i].c_str()) == 0)
                {
                    monitorPaths[i] = &path;
                    targets[i] = path.target;
                    break;
                }
            }
        }

        Identities result;
        result.generation = generation;
        result.complete = complete;
        result.numbers = DisplayNumbers(targets, configuration.available);
        for (size_t i = 0; i < handles.size(); ++i)
        {
            std::wstring id;
            const auto display = std::find_if(displays.begin(), displays.end(), [&](const DisplayUtils::DisplayData& data) { return data.monitor == handles[i]; });
            if (display != displays.end())
            {
                id = MonitorId(display->id, display->instanceId);
            }

            // The name comes from the path showing this monitor's ID, and built-in from its own path.
            std::wstring name;
            for (const DisplayPath& path : configuration.active)
            {
                if (!id.empty() && SameMonitorId(path.id, id))
                {
                    name = path.name;
                    break;
                }
            }
            result.builtIn.push_back(monitorPaths[i] != nullptr && monitorPaths[i]->builtIn);
            result.ids.push_back(std::move(id));
            result.names.push_back(std::move(name));
        }

        // What each handle shows once the reads are done, so the hook thread can tell a read that a
        // display change overtook. A handle that now shows no monitor gets no device.
        for (const HMONITOR handle : handles)
        {
            MONITORINFOEXW info{};
            info.cbSize = sizeof(MONITORINFOEXW);
            if (GetMonitorInfoW(handle, &info))
            {
                result.devices.emplace_back(info.szDevice);
                result.rects.push_back(RectL{ info.rcMonitor.left, info.rcMonitor.top, info.rcMonitor.right, info.rcMonitor.bottom });
            }
            else
            {
                result.devices.emplace_back();
                result.rects.push_back(RectL{});
            }
        }
        return result;
    }
}
