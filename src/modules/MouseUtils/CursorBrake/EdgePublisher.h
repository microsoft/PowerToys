// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <windows.h>

#include "DetectedEdges.h"

#include <mutex>
#include <optional>
#include <string>
#include <utility>
#include <vector>

namespace cursorbrake
{
    // Writes detected-edges.json on a thread-pool work item, so the hook thread never waits on
    // the disk. Only the latest snapshot matters: a write takes whatever snapshot is newest when
    // it starts, and the writes run one at a time, so an older snapshot never lands after a newer
    // one.
    class EdgePublisher
    {
    public:
        using Pairs = std::vector<std::pair<std::wstring, std::wstring>>;

        EdgePublisher();
        ~EdgePublisher();
        EdgePublisher(const EdgePublisher&) = delete;
        EdgePublisher& operator=(const EdgePublisher&) = delete;

        // Queues a write of `monitors`. `present` are the pairs to stamp with the current time,
        // `named` the pairs the overrides name, which start their retention when first recorded.
        void Publish(std::vector<DetectedMonitor> monitors, Pairs present, Pairs named);

    private:
        struct Snapshot
        {
            std::vector<DetectedMonitor> monitors;
            Pairs present;
            Pairs named;
        };

        static void CALLBACK Run(PTP_CALLBACK_INSTANCE instance, PVOID context, PTP_WORK work);
        void Write();

        std::mutex m_pendingMutex;
        std::optional<Snapshot> m_pending;
        std::mutex m_writeMutex;
        PTP_WORK m_work = nullptr;
    };
}
