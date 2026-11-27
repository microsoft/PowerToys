// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "pch.h"

#include <Constants.h>
#include <common/utils/resources.h>
#include "../Strings/FileConverterResourceIds.h"
#include <FileConversionEngine.h>
#include <common/SettingsAPI/settings_objects.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/base.h>
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/utils/package.h>
#include <common/utils/process_path.h>
#include <common/utils/shell_ext_registration.h>
#include <interface/powertoy_module_interface.h>

#include <algorithm>
#include <atomic>
#include <condition_variable>
#include <cwctype>
#include <filesystem>
#include <mutex>
#include <optional>
#include <queue>
#include <string>
#include <thread>
#include <unordered_set>
#include <vector>

extern "C" IMAGE_DOS_HEADER __ImageBase;
namespace winrt_json = winrt::Windows::Data::Json;
namespace fc_constants = winrt::PowerToys::FileConverter::Constants;

namespace
{
    constexpr wchar_t MODULE_NAME_FALLBACK[] = L"File Converter";
    constexpr wchar_t MODULE_KEY[] = L"FileConverter";
    constexpr wchar_t CONTEXT_MENU_PACKAGE_DISPLAY_NAME[] = L"FileConverterContextMenu";
    constexpr wchar_t CONTEXT_MENU_PACKAGE_FILE_NAME[] = L"FileConverterContextMenuPackage.msix";
    constexpr wchar_t CONTEXT_MENU_PACKAGE_FILE_PREFIX[] = L"FileConverterContextMenuPackage";
    constexpr wchar_t CONTEXT_MENU_HANDLER_CLSID[] = L"{57EC18F5-24D5-4DC6-AE2E-9D0F7A39F8BA}";
    constexpr DWORD PIPE_READ_TIMEOUT_MS = 5000;
    struct ConversionRequest
    {
        file_converter::ImageFormat format = file_converter::ImageFormat::Png;
        std::vector<std::wstring> files;
        size_t skipped_entries = 0;
    };

    struct ConversionSummary
    {
        size_t succeeded = 0;
        size_t missing_inputs = 0;
        size_t failed = 0;
        std::wstring first_failed_path;
        std::wstring first_failed_error;
    };

    runtime_shell_ext::Spec BuildWin10ContextMenuSpec()
    {
        runtime_shell_ext::Spec spec;
        spec.clsid = CONTEXT_MENU_HANDLER_CLSID;
        spec.sentinelKey = L"Software\\Microsoft\\PowerToys\\FileConverter";
        spec.sentinelValue = L"ContextMenuRegisteredWin10";
        spec.dllFileCandidates = {
            L"WinUI3Apps\\PowerToys.FileConverterContextMenu.dll",
            L"PowerToys.FileConverterContextMenu.dll",
        };
        spec.friendlyName = L"File Converter Context Menu";
        spec.systemFileAssocHandlerName = L"FileConverterContextMenu";
        spec.representativeSystemExt = L".bmp";
        spec.systemFileAssocExtensions = {
            L".bmp",
            L".dib",
            L".gif",
            L".jfif",
            L".jpe",
            L".jpeg",
            L".jpg",
            L".jxr",
            L".png",
            L".tif",
            L".tiff",
            L".wdp",
            L".heic",
            L".heif",
            L".webp",
        };
        return spec;
    }

    std::optional<std::filesystem::path> FindLatestContextMenuPackage(const std::filesystem::path& context_menu_path)
    {
        const std::filesystem::path stable_package_path = context_menu_path / CONTEXT_MENU_PACKAGE_FILE_NAME;
        if (std::filesystem::exists(stable_package_path))
        {
            return stable_package_path;
        }

        std::vector<std::filesystem::path> candidate_packages;
        std::error_code ec;
        for (std::filesystem::directory_iterator it(context_menu_path, ec); !ec && it != std::filesystem::directory_iterator(); it.increment(ec))
        {
            if (!it->is_regular_file(ec))
            {
                continue;
            }

            const auto file_name = it->path().filename().wstring();
            const auto extension = it->path().extension().wstring();
            if (_wcsicmp(extension.c_str(), L".msix") != 0)
            {
                continue;
            }

            if (file_name.rfind(CONTEXT_MENU_PACKAGE_FILE_PREFIX, 0) == 0)
            {
                candidate_packages.push_back(it->path());
            }
        }

        if (candidate_packages.empty())
        {
            return std::nullopt;
        }

        std::sort(candidate_packages.begin(), candidate_packages.end(), [](const auto& lhs, const auto& rhs) {
            std::error_code lhs_ec;
            std::error_code rhs_ec;
            const auto lhs_time = std::filesystem::last_write_time(lhs, lhs_ec);
            const auto rhs_time = std::filesystem::last_write_time(rhs, rhs_ec);

            if (lhs_ec && rhs_ec)
            {
                return lhs.wstring() < rhs.wstring();
            }

            if (lhs_ec)
            {
                return true;
            }

            if (rhs_ec)
            {
                return false;
            }

            return lhs_time < rhs_time;
        });

        return candidate_packages.back();
    }

    std::wstring ToLower(std::wstring value)
    {
        std::transform(value.begin(), value.end(), value.begin(), [](wchar_t ch) {
            return static_cast<wchar_t>(towlower(ch));
        });

        return value;
    }

    std::optional<file_converter::ImageFormat> ParseFormat(const std::wstring& value)
    {
        const std::wstring lower = ToLower(value);

        if (lower == fc_constants::FormatPng)
        {
            return file_converter::ImageFormat::Png;
        }

        if (lower == fc_constants::FormatJpeg || lower == fc_constants::FormatJpg)
        {
            return file_converter::ImageFormat::Jpeg;
        }

        if (lower == fc_constants::FormatBmp)
        {
            return file_converter::ImageFormat::Bmp;
        }

        if (lower == fc_constants::FormatTiff || lower == fc_constants::FormatTif)
        {
            return file_converter::ImageFormat::Tiff;
        }

        if (lower == fc_constants::FormatHeic || lower == fc_constants::FormatHeif)
        {
            return file_converter::ImageFormat::Heif;
        }

        if (lower == fc_constants::FormatWebp)
        {
            return file_converter::ImageFormat::Webp;
        }

        return std::nullopt;
    }

    std::wstring ExtensionForFormat(file_converter::ImageFormat format)
    {
        switch (format)
        {
        case file_converter::ImageFormat::Jpeg:
            return fc_constants::ExtensionJpg;
        case file_converter::ImageFormat::Bmp:
            return fc_constants::ExtensionBmp;
        case file_converter::ImageFormat::Tiff:
            return fc_constants::ExtensionTiff;
        case file_converter::ImageFormat::Heif:
            return fc_constants::ExtensionHeic;
        case file_converter::ImageFormat::Webp:
            return fc_constants::ExtensionWebp;
        case file_converter::ImageFormat::Png:
        default:
            return fc_constants::ExtensionPng;
        }
    }

    std::wstring GetPipeNameForCurrentSession()
    {
        DWORD session_id = 0;
        if (!ProcessIdToSessionId(GetCurrentProcessId(), &session_id))
        {
            session_id = 0;
        }

        return std::wstring(fc_constants::PipeNamePrefix) + std::to_wstring(session_id);
    }

    DWORD CompletePendingPipeOperation(HANDLE pipe_handle, OVERLAPPED& operation, HANDLE stop_event, DWORD& bytes_transferred, DWORD timeout_ms)
    {
        const HANDLE events[]{ stop_event, operation.hEvent };
        const DWORD wait_result = WaitForMultipleObjects(ARRAYSIZE(events), events, FALSE, timeout_ms);
        if (wait_result == WAIT_OBJECT_0 + 1)
        {
            return GetOverlappedResult(pipe_handle, &operation, &bytes_transferred, FALSE) ? ERROR_SUCCESS : GetLastError();
        }

        DWORD error = ERROR_OPERATION_ABORTED;
        if (wait_result == WAIT_TIMEOUT)
        {
            error = ERROR_SEM_TIMEOUT;
        }
        else if (wait_result == WAIT_FAILED)
        {
            error = GetLastError();
        }
        CancelIoEx(pipe_handle, &operation);
        // Keep the operation and its buffer alive until cancellation or completion finishes.
        GetOverlappedResult(pipe_handle, &operation, &bytes_transferred, TRUE);
        return error;
    }

    std::string ReadPipeMessage(HANDLE pipe_handle, HANDLE io_event, HANDLE stop_event)
    {
        constexpr DWORD BUFFER_SIZE = 4096;
        char buffer[BUFFER_SIZE] = {};
        std::string payload;
        const auto deadline = GetTickCount64() + PIPE_READ_TIMEOUT_MS;

        while (WaitForSingleObject(stop_event, 0) == WAIT_TIMEOUT)
        {
            const auto now = GetTickCount64();
            if (now >= deadline)
            {
                break;
            }

            ResetEvent(io_event);
            OVERLAPPED operation{};
            operation.hEvent = io_event;
            DWORD bytes_read = 0;
            DWORD read_error = ReadFile(pipe_handle, buffer, BUFFER_SIZE, &bytes_read, &operation) ? ERROR_SUCCESS : GetLastError();
            if (read_error == ERROR_IO_PENDING)
            {
                read_error = CompletePendingPipeOperation(pipe_handle, operation, stop_event, bytes_read, static_cast<DWORD>(deadline - now));
            }

            if (bytes_read > 0)
            {
                payload.append(buffer, bytes_read);
            }

            if (read_error == ERROR_SUCCESS)
            {
                return payload;
            }

            if (read_error == ERROR_MORE_DATA)
            {
                continue;
            }

            if (read_error != ERROR_BROKEN_PIPE && read_error != ERROR_PIPE_NOT_CONNECTED && read_error != ERROR_OPERATION_ABORTED && read_error != ERROR_SEM_TIMEOUT)
            {
                Logger::warn(L"File Converter pipe read failed. Error={}", read_error);
            }

            break;
        }

        return {};
    }

    bool TryParseFormatConvertRequest(
        const std::string& payload,
        ConversionRequest& request,
        std::wstring& rejection_reason)
    {
        request = {};
        rejection_reason.clear();

        if (payload.empty())
        {
            rejection_reason = GET_RESOURCE_STRING_FALLBACK(IDS_FILECONVERTER_ERROR_EMPTYPAYLOAD, L"empty payload");
            return false;
        }

        winrt_json::JsonObject json_payload;
        if (!winrt_json::JsonObject::TryParse(winrt::to_hstring(payload), json_payload))
        {
            rejection_reason = GET_RESOURCE_STRING_FALLBACK(IDS_FILECONVERTER_ERROR_INVALIDJSON, L"invalid JSON");
            return false;
        }

        if (!json_payload.HasKey(fc_constants::JsonActionKey))
        {
            rejection_reason = GET_RESOURCE_STRING_FALLBACK(IDS_FILECONVERTER_ERROR_MISSINGACTION, L"missing action");
            return false;
        }

        const auto action_value = json_payload.GetNamedValue(fc_constants::JsonActionKey);
        if (action_value.ValueType() != winrt_json::JsonValueType::String)
        {
            rejection_reason = GET_RESOURCE_STRING_FALLBACK(IDS_FILECONVERTER_ERROR_ACTIONNOTSTRING, L"action is not a string");
            return false;
        }

        const auto action = json_payload.GetNamedString(fc_constants::JsonActionKey);
        if (_wcsicmp(action.c_str(), fc_constants::ActionFormatConvert) != 0)
        {
            rejection_reason = GET_RESOURCE_STRING_FALLBACK(IDS_FILECONVERTER_ERROR_UNSUPPORTEDACTION, L"unsupported action");
            return false;
        }

        std::wstring destination = fc_constants::FormatPng;
        if (json_payload.HasKey(fc_constants::JsonDestinationKey))
        {
            const auto destination_value = json_payload.GetNamedValue(fc_constants::JsonDestinationKey);
            if (destination_value.ValueType() == winrt_json::JsonValueType::String)
            {
                destination = json_payload.GetNamedString(fc_constants::JsonDestinationKey).c_str();
            }
        }

        if (!json_payload.HasKey(fc_constants::JsonFilesKey))
        {
            rejection_reason = GET_RESOURCE_STRING_FALLBACK(IDS_FILECONVERTER_ERROR_MISSINGFILESARRAY, L"missing files array");
            return false;
        }

        const auto files_value = json_payload.GetNamedValue(fc_constants::JsonFilesKey);
        if (files_value.ValueType() != winrt_json::JsonValueType::Array)
        {
            rejection_reason = GET_RESOURCE_STRING_FALLBACK(IDS_FILECONVERTER_ERROR_FILESNOTARRAY, L"files is not an array");
            return false;
        }

        const auto files_array = json_payload.GetNamedArray(fc_constants::JsonFilesKey);
        for (const auto& file_value : files_array)
        {
            if (file_value.ValueType() != winrt_json::JsonValueType::String)
            {
                ++request.skipped_entries;
                continue;
            }

            const auto file_path = file_value.GetString();
            if (file_path.empty())
            {
                ++request.skipped_entries;
                continue;
            }

            request.files.push_back(file_path.c_str());
        }

        if (request.files.empty())
        {
            rejection_reason = GET_RESOURCE_STRING_FALLBACK(IDS_FILECONVERTER_ERROR_NOVALIDPATHS, L"no valid file paths");
            return false;
        }

        const auto parsed_format = ParseFormat(destination);
        if (!parsed_format.has_value())
        {
            rejection_reason = GET_RESOURCE_STRING_FALLBACK(IDS_FILECONVERTER_ERROR_UNSUPPORTEDDESTINATION, L"unsupported destination format");
            return false;
        }

        const auto support = file_converter::IsOutputFormatSupported(parsed_format.value());
        if (FAILED(support.hr))
        {
            rejection_reason = support.error_message.empty() ? GET_RESOURCE_STRING_FALLBACK(IDS_FILECONVERTER_ERROR_DESTINATIONUNAVAILABLE, L"requested destination format is unavailable") : support.error_message;
            return false;
        }

        request.format = parsed_format.value();
        return true;
    }

    ConversionSummary ProcessFormatConvertRequest(const ConversionRequest& request)
    {
        ConversionSummary summary;
        const std::wstring output_extension = ExtensionForFormat(request.format);
        std::unordered_set<std::wstring> seen_files;

        for (const auto& file : request.files)
        {
            if (!seen_files.insert(file).second)
            {
                continue;
            }

            const std::filesystem::path input_path(file);
            std::error_code ec;
            if (input_path.empty() || !std::filesystem::exists(input_path, ec) || ec)
            {
                ++summary.missing_inputs;
                continue;
            }

            ec.clear();
            if (!std::filesystem::is_regular_file(input_path, ec) || ec)
            {
                ++summary.missing_inputs;
                continue;
            }

            std::filesystem::path output_path = input_path.parent_path() / input_path.stem();
            output_path += L"_converted";
            output_path += output_extension;

            const auto conversion = file_converter::ConvertImageFile(input_path.wstring(), output_path.wstring(), request.format);
            if (conversion.succeeded())
            {
                ++summary.succeeded;
                continue;
            }

            ++summary.failed;
            if (summary.first_failed_path.empty())
            {
                summary.first_failed_path = input_path.wstring();
                summary.first_failed_error = conversion.error_message;
            }
        }

        return summary;
    }

    void EnsureContextMenuPackageRegistered()
    {
        if (!package::IsWin11OrGreater())
        {
            return;
        }

        const std::filesystem::path module_path = get_module_folderpath(reinterpret_cast<HMODULE>(&__ImageBase));
        const std::filesystem::path context_menu_path = module_path / L"WinUI3Apps";
        if (!std::filesystem::exists(context_menu_path))
        {
            return;
        }

        const auto package_path = FindLatestContextMenuPackage(context_menu_path);
        if (!package_path.has_value())
        {
            return;
        }

        if (!package::IsPackageSatisfied(package_path->wstring()))
        {
            (void)package::RegisterSparsePackage(context_menu_path.wstring(), package_path->wstring());
        }
    }

    void EnsureContextMenuRuntimeRegistered()
    {
        if (package::IsWin11OrGreater())
        {
            return;
        }

        (void)runtime_shell_ext::EnsureRegistered(BuildWin10ContextMenuSpec(), reinterpret_cast<HMODULE>(&__ImageBase));
    }

    void UnregisterContextMenuRuntime()
    {
        if (package::IsWin11OrGreater())
        {
            return;
        }

        runtime_shell_ext::Unregister(BuildWin10ContextMenuSpec());
    }

    void UnregisterContextMenuPackage()
    {
        if (!package::IsWin11OrGreater())
        {
            return;
        }

        (void)package::UnRegisterPackage(CONTEXT_MENU_PACKAGE_DISPLAY_NAME);
    }

    class FileConverterPipeOrchestrator
    {
    public:
        void Start(const std::wstring& pipe_name)
        {
            std::scoped_lock lifecycle_lock(m_lifecycle_mutex);
            if (m_running.load())
            {
                return;
            }

            m_pipe_name = pipe_name;
            m_stop_event = winrt::handle{ CreateEventW(nullptr, TRUE, FALSE, nullptr) };
            winrt::check_bool(m_stop_event.get() != nullptr);
            m_io_event = winrt::handle{ CreateEventW(nullptr, TRUE, FALSE, nullptr) };
            winrt::check_bool(m_io_event.get() != nullptr);
            {
                std::scoped_lock lock(m_queue_mutex);
                m_running.store(true);
            }

            try
            {
                m_listener_thread = std::thread(&FileConverterPipeOrchestrator::ListenerLoop, this);
                m_worker_thread = std::thread(&FileConverterPipeOrchestrator::WorkerLoop, this);
            }
            catch (...)
            {
                StopThreads();
                throw;
            }
        }

        void Stop()
        {
            std::scoped_lock lifecycle_lock(m_lifecycle_mutex);
            StopThreads();
        }

        ~FileConverterPipeOrchestrator()
        {
            Stop();
        }

        void EnqueuePayload(std::string payload)
        {
            {
                std::scoped_lock lock(m_queue_mutex);
                if (!m_running.load())
                {
                    return;
                }

                m_pending_payloads.push(std::move(payload));
            }

            m_queue_cv.notify_one();
        }

    private:
        void StopThreads()
        {
            {
                std::scoped_lock lock(m_queue_mutex);
                if (!m_running.exchange(false))
                {
                    return;
                }
            }

            SetEvent(m_stop_event.get());
            m_queue_cv.notify_all();

            if (m_listener_thread.joinable())
            {
                m_listener_thread.join();
            }

            if (m_worker_thread.joinable())
            {
                m_worker_thread.join();
            }

            std::queue<std::string> empty;
            {
                std::scoped_lock lock(m_queue_mutex);
                std::swap(m_pending_payloads, empty);
            }
        }

        void ProcessPayload(const std::string& payload)
        {
            ConversionRequest request;
            std::wstring rejection_reason;
            if (!TryParseFormatConvertRequest(payload, request, rejection_reason))
            {
                if (!rejection_reason.empty())
                {
                    Logger::warn(L"File Converter ignored malformed request: {}", rejection_reason);
                }

                return;
            }

            const auto summary = ProcessFormatConvertRequest(request);

            if (request.skipped_entries > 0)
            {
                Logger::warn(L"File Converter request skipped {} invalid file entries.", request.skipped_entries);
            }

            if (summary.missing_inputs > 0)
            {
                Logger::warn(L"File Converter request skipped {} missing input files.", summary.missing_inputs);
            }

            if (summary.failed > 0)
            {
                Logger::warn(L"File Converter conversion failed for {} file(s).", summary.failed);
                if (!summary.first_failed_path.empty())
                {
                    Logger::warn(L"First conversion failure: path='{}' reason='{}'", summary.first_failed_path, summary.first_failed_error);
                }
            }
        }

        void WorkerLoop()
        {
            while (true)
            {
                std::string payload;
                {
                    std::unique_lock lock(m_queue_mutex);
                    m_queue_cv.wait(lock, [this] {
                        return !m_running.load() || !m_pending_payloads.empty();
                    });

                    if (m_pending_payloads.empty())
                    {
                        break;
                    }

                    payload = std::move(m_pending_payloads.front());
                    m_pending_payloads.pop();
                }

                ProcessPayload(payload);
            }
        }

        void ListenerLoop()
        {
            winrt::handle pipe;
            while (m_running.load())
            {
                if (!pipe.get())
                {
                    const HANDLE pipe_handle = CreateNamedPipeW(
                        m_pipe_name.c_str(),
                        PIPE_ACCESS_INBOUND | FILE_FLAG_OVERLAPPED,
                        PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
                        PIPE_UNLIMITED_INSTANCES,
                        0,
                        4096,
                        0,
                        nullptr);

                    if (pipe_handle == INVALID_HANDLE_VALUE)
                    {
                        WaitForSingleObject(m_stop_event.get(), 100);
                        continue;
                    }

                    // Reuse the server instance so waiting clients can connect between requests.
                    pipe = winrt::handle{ pipe_handle };
                }

                ResetEvent(m_io_event.get());
                OVERLAPPED operation{};
                operation.hEvent = m_io_event.get();
                DWORD connect_error = ConnectNamedPipe(pipe.get(), &operation) ? ERROR_SUCCESS : GetLastError();
                if (connect_error == ERROR_IO_PENDING)
                {
                    DWORD unused = 0;
                    connect_error = CompletePendingPipeOperation(pipe.get(), operation, m_stop_event.get(), unused, INFINITE);
                }

                if (connect_error != ERROR_SUCCESS && connect_error != ERROR_PIPE_CONNECTED)
                {
                    DisconnectNamedPipe(pipe.get());
                    WaitForSingleObject(m_stop_event.get(), 50);
                    continue;
                }

                auto payload = ReadPipeMessage(pipe.get(), m_io_event.get(), m_stop_event.get());

                // Inbound-only server pipes have no outbound data to flush.
                // Skipping FlushFileBuffers avoids reconnect stalls on malformed-request sequences.
                DisconnectNamedPipe(pipe.get());

                if (!payload.empty())
                {
                    EnqueuePayload(std::move(payload));
                }
            }
        }

        std::atomic<bool> m_running = false;
        std::mutex m_lifecycle_mutex;
        std::wstring m_pipe_name;
        winrt::handle m_stop_event;
        winrt::handle m_io_event;
        std::thread m_listener_thread;
        std::thread m_worker_thread;
        std::mutex m_queue_mutex;
        std::condition_variable m_queue_cv;
        std::queue<std::string> m_pending_payloads;
    };
}

class FileConverterModule : public PowertoyModuleIface
{
public:
    FileConverterModule()
    {
        // Avoid WinRT resource activation during module construction.
        // The runner loads modules very early, and constructor failures can terminate startup.
        m_name = MODULE_NAME_FALLBACK;
        LoggerHelpers::init_logger(m_key, L"ModuleInterface", "fileconverter");
    }

    ~FileConverterModule()
    {
        if (m_enabled)
        {
            m_pipe_orchestrator.Stop();
            UnregisterContextMenuRuntime();
        }
    }

    void destroy() override
    {
        delete this;
    }

    const wchar_t* get_name() override
    {
        return m_name.c_str();
    }

    const wchar_t* get_key() override
    {
        return m_key.c_str();
    }

    bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(GET_RESOURCE_STRING_FALLBACK(IDS_FILECONVERTER_SETTINGS_DESCRIPTION, L"Convert image files to common formats."));
        settings.set_overview_link(L"https://aka.ms/PowerToysOverview_FileConverter");
        settings.set_icon_key(L"pt-file-converter");
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    void set_config(const wchar_t* /*config*/) override
    {
    }

    void call_custom_action(const wchar_t* action) override
    {
        if (action == nullptr)
        {
            return;
        }

        m_pipe_orchestrator.EnqueuePayload(winrt::to_string(action));
    }

    void enable() override
    {
        if (m_enabled)
        {
            return;
        }

        EnsureContextMenuPackageRegistered();
        EnsureContextMenuRuntimeRegistered();
        m_pipe_orchestrator.Start(GetPipeNameForCurrentSession());
        m_enabled = true;
    }

    void disable() override
    {
        if (!m_enabled)
        {
            return;
        }

        m_pipe_orchestrator.Stop();
        UnregisterContextMenuRuntime();
        UnregisterContextMenuPackage();
        m_enabled = false;
    }

    bool is_enabled() override
    {
        return m_enabled;
    }

private:
    bool m_enabled = false;
    std::wstring m_name;
    std::wstring m_key = MODULE_KEY;
    FileConverterPipeOrchestrator m_pipe_orchestrator;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new FileConverterModule();
}
