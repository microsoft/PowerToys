//
// Copyright (c) Microsoft Corporation.  All rights reserved.
//

#pragma once
#include "pch.h"

#include <evntrace.h>
#include <filesystem>
#include <thread>
#include <condition_variable>

namespace Shared
{
    namespace Trace
    {
        class ETWTrace
        {
        public:
            static inline const wchar_t* PowerToysProviderGUID = L"{38e8889b-9731-53f5-e901-e8a7c1753074}";

            ETWTrace();
            ETWTrace(const std::wstring& etlFileNameOverride);
            ~ETWTrace();

            void UpdateState(bool tracing);
            void Flush();

        private:
            void CreateEtwFolderIfNeeded();
            void InitEventTraceProperties();
            void Start();
            void Stop();
            void Control(const ULONG traceControlCode);
            void Enable(const ULONG eventControlCode);
            void FlushWorker();

            GUID m_providerGUID{};
            std::filesystem::path m_etwFolder;
            std::wstring m_sessionName;
            TRACEHANDLE m_traceHandle{ INVALID_PROCESSTRACE_HANDLE };
            std::unique_ptr<unsigned char[]> m_eventTracePropertiesBuffer;
            std::atomic_bool m_tracing{ false };
            std::wstring m_etlFileNameOverride{};
            std::thread m_flushing_thread;
            std::condition_variable m_terminate_flushing_thread;
            std::mutex m_mutex;

            static constexpr PCWSTR c_etwFolderName = L"etw";
            static constexpr PCWSTR c_etwNewFileFormattedCounter = L"-%d";
            static constexpr PCWSTR c_etwFileNameEnd = L".etl";
        };
    }
}