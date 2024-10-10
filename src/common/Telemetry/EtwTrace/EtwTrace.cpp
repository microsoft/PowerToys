//
// Copyright (c) Microsoft Corporation.  All rights reserved.
//

#pragma once
#include "pch.h"

#include "ETWTrace.h"

#include <wil\stl.h>
#include <wil\win32_helpers.h>

namespace fs = std::filesystem;

namespace
{
    constexpr inline const wchar_t* DataDiagnosticsRegKey = L"Software\\Classes\\PowerToys";
    constexpr inline const wchar_t* ViewDataDiagnosticsRegValueName = L"DataDiagnosticsViewEnabled";

    inline std::wstring get_root_save_folder_location()
    {
        PWSTR local_app_path;
        winrt::check_hresult(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, NULL, &local_app_path));
        std::wstring result{ local_app_path };
        CoTaskMemFree(local_app_path);

        result += L"\\Microsoft\\PowerToys";
        std::filesystem::path save_path(result);
        if (!std::filesystem::exists(save_path))
        {
            std::filesystem::create_directories(save_path);
        }
        return result;
    }

    bool isViewDataDiagnosticEnabled()
    {
        HKEY key{};
        if (RegOpenKeyExW(HKEY_CURRENT_USER,
                          DataDiagnosticsRegKey,
                          0,
                          KEY_READ,
                          &key) != ERROR_SUCCESS)
        {
            return false;
        }

        DWORD isDataDiagnosticsEnabled = 0;
        DWORD size = sizeof(isDataDiagnosticsEnabled);

        if (RegGetValueW(
                HKEY_CURRENT_USER,
                DataDiagnosticsRegKey,
                ViewDataDiagnosticsRegValueName,
                RRF_RT_REG_DWORD,
                nullptr,
                &isDataDiagnosticsEnabled,
                &size) != ERROR_SUCCESS)
        {
            RegCloseKey(key);
            return false;
        }
        RegCloseKey(key);

        return isDataDiagnosticsEnabled;
    }

}

namespace Shared
{
    namespace Trace
    {
        ETWTrace::ETWTrace() :
            ETWTrace(PowerToysProviderGUID)
        {

        }

        ETWTrace::ETWTrace(const std::wstring& providerGUIDstr)
        {
            GUID id;
            if (SUCCEEDED(CLSIDFromString(providerGUIDstr.c_str(), &id)))
            {
                m_providerGUID = id;
            }

            fs::path outputFolder = get_root_save_folder_location();
            m_etwFolder = (outputFolder / c_etwFolderName);
        }

        ETWTrace::ETWTrace(const GUID& providerGUID) :
            m_providerGUID(providerGUID)
        {
            fs::path outputFolder = get_root_save_folder_location();
            m_etwFolder = (outputFolder / c_etwFolderName);
        }

        ETWTrace::~ETWTrace()
        {
            Stop();
            m_etwFolder.clear();
            m_providerGUID = {};
        }

        void ETWTrace::UpdateState(bool tracing)
        {
            if (tracing)
            {
                Start();
            }
            else
            {
                Stop();
            }
        }

        void ETWTrace::Flush()
        {
            if (m_tracing)
            {
                Control(EVENT_TRACE_CONTROL_FLUSH);
                Control(EVENT_TRACE_CONTROL_INCREMENT_FILE);
            }
        }

        void ETWTrace::CreateEtwFolderIfNeeded()
        {
            if (!std::filesystem::exists(m_etwFolder))
            {
                std::filesystem::create_directories(m_etwFolder);
            }
            else if (!std::filesystem::is_directory(m_etwFolder))
            {
                std::filesystem::remove(m_etwFolder);
                std::filesystem::create_directory(m_etwFolder);
            }

            THROW_HR_IF(E_UNEXPECTED, !std::filesystem::exists(m_etwFolder));
        }

        void ETWTrace::InitEventTraceProperties()
        {
            const std::filesystem::path exePath(wil::GetModuleFileNameW<std::wstring>(nullptr));
            const auto exeName = exePath.stem().wstring();

            auto now = std::chrono::system_clock::now();
            auto timeNow = std::chrono::system_clock::to_time_t(now);
            std::wstringstream dateTime;
            struct tm timeInfo
            {
            };
            errno_t err = localtime_s(&timeInfo, &timeNow);
            if (err == 0)
            {
                dateTime << std::put_time(&timeInfo, L"-%m-%d-%Y__%H_%M_%S");
            }

            m_sessionName = wil::str_printf<std::wstring>(L"%ws-%d%ws", exeName.c_str(), GetCurrentProcessId(), dateTime.str().c_str());
            std::replace(m_sessionName.begin(), m_sessionName.end(), '.', '_');

            const ULONG etwSessionNameCharCount = static_cast<ULONG>(m_sessionName.size() + 1);
            const ULONG etwSessionNameByteSize = etwSessionNameCharCount * sizeof(m_sessionName[0]);

            auto etlFileNameFormattedCounter = m_sessionName + c_etwNewFileFormattedCounter;
            std::filesystem::path etlFilePath = m_etwFolder / etlFileNameFormattedCounter;
            etlFilePath.replace_extension(c_etwFileNameEnd);
            THROW_HR_IF(E_UNEXPECTED, etlFilePath.empty());

            const auto etlFilePathStr = etlFilePath.wstring();
            // std::string/wstring returns number of characters not including the null terminator, so add +1 for that.
            const ULONG etwFilePathCharCount = static_cast<ULONG>(etlFilePathStr.size() + 1);
            const ULONG etwFilePathByteSize = etwFilePathCharCount * sizeof(etlFilePathStr[0]);

            const ULONG bufferSizeInBytes = sizeof(EVENT_TRACE_PROPERTIES) + etwSessionNameByteSize + etwFilePathByteSize;
            auto eventTracePropertiesBuffer = std::make_unique<unsigned char[]>(bufferSizeInBytes);
            ZeroMemory(eventTracePropertiesBuffer.get(), bufferSizeInBytes);
            auto eventTraceProperties = reinterpret_cast<EVENT_TRACE_PROPERTIES*>(eventTracePropertiesBuffer.get());

            eventTraceProperties->Wnode.BufferSize = bufferSizeInBytes;
            eventTraceProperties->Wnode.Flags = WNODE_FLAG_TRACED_GUID;
            eventTraceProperties->Wnode.ClientContext = 1; // QPC clock resolution
            eventTraceProperties->Wnode.Guid = m_providerGUID;
            eventTraceProperties->BufferSize = 4; // 4KB, the minimum size
            eventTraceProperties->LogFileMode = EVENT_TRACE_PRIVATE_LOGGER_MODE | EVENT_TRACE_PRIVATE_IN_PROC | EVENT_TRACE_FILE_MODE_NEWFILE;
            eventTraceProperties->MaximumFileSize = 1; // 1 MB

            // LoggerName is placed at the end of EVENT_TRACE_PROPERTIES structure
            eventTraceProperties->LoggerNameOffset = sizeof(EVENT_TRACE_PROPERTIES);
            wcsncpy_s(reinterpret_cast<LPWSTR>(eventTracePropertiesBuffer.get() + eventTraceProperties->LoggerNameOffset), etwSessionNameCharCount, m_sessionName.c_str(), etwSessionNameCharCount);

            // LogFileName is placed at the end of the Logger Name
            eventTraceProperties->LogFileNameOffset = eventTraceProperties->LoggerNameOffset + etwSessionNameByteSize;
            wcsncpy_s(reinterpret_cast<LPWSTR>(eventTracePropertiesBuffer.get() + eventTraceProperties->LogFileNameOffset), etwFilePathCharCount, etlFilePathStr.c_str(), etwFilePathCharCount);

            m_eventTracePropertiesBuffer = std::move(eventTracePropertiesBuffer);
        }

        void ETWTrace::Start()
        {
            if (m_tracing)
            {
                return;
            }

            if (!isViewDataDiagnosticEnabled())
            {
                return;
            }

            CreateEtwFolderIfNeeded();
            InitEventTraceProperties();

            auto eventTraceProperties = reinterpret_cast<EVENT_TRACE_PROPERTIES*>(m_eventTracePropertiesBuffer.get());
            THROW_IF_WIN32_ERROR(StartTrace(&m_traceHandle, m_sessionName.c_str(), eventTraceProperties));
            Enable(EVENT_CONTROL_CODE_ENABLE_PROVIDER);

            m_tracing = true;
        }

        void ETWTrace::Stop()
        {
            if (!m_tracing)
            {
                return;
            }

            Enable(EVENT_CONTROL_CODE_DISABLE_PROVIDER);

            // ControlTrace with EVENT_TRACE_CONTROL_STOP on the trace handle,
            // which is equivalent to calling CloseTrace() on the trace handle.
            Control(EVENT_TRACE_CONTROL_STOP);

            m_traceHandle = INVALID_PROCESSTRACE_HANDLE;
            m_eventTracePropertiesBuffer.reset();
            m_tracing = false;
        }

        void ETWTrace::Control(ULONG traceControlCode)
        {
            auto eventTraceProperties = reinterpret_cast<EVENT_TRACE_PROPERTIES*>(m_eventTracePropertiesBuffer.get());
            const ULONG result = ControlTrace(m_traceHandle, m_sessionName.c_str(), eventTraceProperties, traceControlCode);
            THROW_IF_FAILED(HRESULT_FROM_WIN32(result));
        }

        void ETWTrace::Enable(ULONG eventControlCode)
        {
            // Control the main provider
            THROW_IF_WIN32_ERROR(EnableTraceEx2(m_traceHandle, &m_providerGUID, eventControlCode, TRACE_LEVEL_VERBOSE, 0, 0, 0, nullptr));
        }
    }
}