// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "EdgePublisher.h"
#include "ModuleConstants.h"

#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/json.h>

#include <chrono>

namespace
{
    const wchar_t DETECTED_EDGES_FILE[] = L"\\detected-edges.json";
}

namespace cursorbrake
{
    EdgePublisher::EdgePublisher()
    {
        m_work = CreateThreadpoolWork(Run, this, nullptr);
        if (!m_work)
        {
            Logger::error(L"Failed to create the CursorBrake edge publisher work item, error: {}. Settings will not see the monitor edges.", GetLastError());
        }
    }

    EdgePublisher::~EdgePublisher()
    {
        if (m_work)
        {
            // The callbacks run this object's code, so none may outlive it.
            WaitForThreadpoolWorkCallbacks(m_work, FALSE);
            CloseThreadpoolWork(m_work);
        }
    }

    void EdgePublisher::Publish(std::vector<DetectedMonitor> monitors, Pairs present, Pairs named)
    {
        if (!m_work)
        {
            return;
        }
        {
            std::lock_guard lock(m_pendingMutex);
            m_pending = Snapshot{ std::move(monitors), std::move(present), std::move(named) };
        }
        SubmitThreadpoolWork(m_work);
    }

    void CALLBACK EdgePublisher::Run(PTP_CALLBACK_INSTANCE /*instance*/, PVOID context, PTP_WORK /*work*/)
    {
        static_cast<EdgePublisher*>(context)->Write();
    }

    void EdgePublisher::Write()
    {
        std::lock_guard writeLock(m_writeMutex);
        std::optional<Snapshot> snapshot;
        {
            std::lock_guard pendingLock(m_pendingMutex);
            snapshot.swap(m_pending);
        }
        if (!snapshot)
        {
            return;
        }

        try
        {
            const std::wstring path = PTSettingsHelper::get_module_save_folder_location(MODULE_NAME) + DETECTED_EDGES_FILE;
            const int64_t now = std::chrono::duration_cast<std::chrono::seconds>(std::chrono::system_clock::now().time_since_epoch()).count();
            SeenRecord previous;
            if (const std::optional<json::JsonObject> file = json::from_file(path))
            {
                previous = SeenRecord{ ReadSeen(*file), ReadExpired(*file) };
            }
            const SeenRecord record = MergeSeen(previous, snapshot->present, snapshot->named, now);
            json::to_file(path, BuildDetectedEdges(snapshot->monitors, record.seen, record.expired));
            Logger::info(L"CursorBrake published {} monitors, {} seen pairs and {} expired pairs.", snapshot->monitors.size(), record.seen.size(), record.expired.size());
        }
        catch (...)
        {
            // catch(...) because the JSON and file helpers throw winrt::hresult_error and
            // std::filesystem errors alike; a failed write leaves Settings with the last file.
            Logger::error(L"Failed to write the CursorBrake detected edges file.");
        }
    }
}
