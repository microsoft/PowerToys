#include "../Common/ProtoCommon.h"

#include <filesystem>

namespace
{
    std::wstring current_package_full_name()
    {
        UINT32 chars = 0;
        LONG result = GetCurrentPackageFullName(&chars, nullptr);
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptap::win32_error("GetCurrentPackageFullName(size)", result);
        }
        std::wstring value(chars, L'\0');
        result = GetCurrentPackageFullName(&chars, value.data());
        if (result != ERROR_SUCCESS)
        {
            throw ptap::win32_error("GetCurrentPackageFullName", result);
        }
        value.resize(chars - 1);
        return value;
    }
}

int wmain()
{
    try
    {
        const auto args = ptap::command_line_arguments();
        const auto stateArgument = ptap::argument_value(args, L"--config");
        const auto readyEventName = ptap::argument_value(args, L"--ready-event");
        const auto stopEventName = ptap::argument_value(args, L"--stop-event");
        if (stateArgument.empty() ||
            readyEventName.empty() ||
            stopEventName.empty() ||
            stateArgument.size() >= 1024 ||
            readyEventName.size() >= 192 ||
            stopEventName.size() >= 192)
        {
            return ERROR_INVALID_PARAMETER;
        }

        const std::filesystem::path statePath = std::filesystem::weakly_canonical(stateArgument);
        const auto state = ptap::read_state(statePath);
        const auto ownerSid = ptap::bounded_string(state.ownerSid, ARRAYSIZE(state.ownerSid));
        const auto accountSid = ptap::bounded_string(state.accountSid, ARRAYSIZE(state.accountSid));
        const auto expectedServiceSid = ptap::bounded_string(state.serviceSid, ARRAYSIZE(state.serviceSid));
        const auto names = ptap::instance_names(ownerSid);
        if (!std::filesystem::equivalent(statePath, names.statePath) ||
            accountSid != ptap::current_token_user_sid())
        {
            return ERROR_ACCESS_DENIED;
        }
        const std::wstring readyPrefix = L"PtAliasProtoReady_" + names.suffix + L"_";
        const std::wstring stopPrefix = L"PtAliasProtoStop_" + names.suffix + L"_";
        if (!readyEventName.starts_with(readyPrefix) || !stopEventName.starts_with(stopPrefix))
        {
            return ERROR_INVALID_NAME;
        }

        std::wstring fullName;
        try
        {
            fullName = current_package_full_name();
        }
        catch (const ptap::win32_error& error)
        {
            if (error.code() == APPMODEL_ERROR_NO_PACKAGE)
            {
                const auto marker = names.storeDirectory / L"tamper-code-executed.marker";
                ptap::unique_handle file(CreateFileW(
                    marker.c_str(),
                    GENERIC_WRITE,
                    FILE_SHARE_READ,
                    nullptr,
                    CREATE_ALWAYS,
                    FILE_ATTRIBUTE_NORMAL,
                    nullptr));
                return APPMODEL_ERROR_NO_PACKAGE;
            }
            throw;
        }
        const auto identity = ptap::validate_package_full_name(fullName);
        HANDLE rawToken = nullptr;
        ptap::check_bool(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &rawToken), "OpenProcessToken(worker)");
        ptap::unique_handle token(rawToken);
        const bool hasServiceSid = ptap::token_contains_sid(token.get(), expectedServiceSid);

        ptap::EvidenceRecord evidence;
        evidence.launchCount = ptap::increment_launch_count(names.storeDirectory);
        evidence.processId = GetCurrentProcessId();
        evidence.hasExpectedServiceSid = hasServiceSid ? 1u : 0u;
        ptap::copy_bounded(evidence.packageFullName, ARRAYSIZE(evidence.packageFullName), fullName);
        ptap::copy_bounded(evidence.packageFamilyName, ARRAYSIZE(evidence.packageFamilyName), identity.familyName);
        ptap::copy_bounded(evidence.userSid, ARRAYSIZE(evidence.userSid), accountSid);
        ptap::copy_bounded(evidence.serviceSid, ARRAYSIZE(evidence.serviceSid), expectedServiceSid);
        ptap::write_evidence_atomic(names.evidencePath, evidence);

        ptap::append_log(
            names.storeDirectory,
            L"worker",
            L"ready package=" + fullName +
                L", version=" + std::to_wstring(identity.version.major) + L"." +
                std::to_wstring(identity.version.minor) + L"." +
                std::to_wstring(identity.version.build) + L"." +
                std::to_wstring(identity.version.revision) +
                L", family=" + identity.familyName +
                L", user=" + accountSid +
                L", serviceSidPresent=" + (hasServiceSid ? L"true" : L"false") +
                L", launchCount=" + std::to_wstring(evidence.launchCount));

        ptap::unique_handle ready(OpenEventW(EVENT_MODIFY_STATE, FALSE, readyEventName.c_str()));
        ptap::unique_handle stop(OpenEventW(SYNCHRONIZE, FALSE, stopEventName.c_str()));
        if (!ready || !stop)
        {
            return GetLastError();
        }
        if (!hasServiceSid)
        {
            return ERROR_ACCESS_DENIED;
        }
        ptap::check_bool(SetEvent(ready.get()), "SetEvent(readiness)");
        const DWORD wait = WaitForSingleObject(stop.get(), INFINITE);
        return wait == WAIT_OBJECT_0 ? 0 : GetLastError();
    }
    catch (const ptap::win32_error& error)
    {
        return static_cast<int>(error.code());
    }
    catch (...)
    {
        return ERROR_UNHANDLED_EXCEPTION;
    }
}
