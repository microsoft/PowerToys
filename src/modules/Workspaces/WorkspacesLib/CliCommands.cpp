// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include "CliCommands.h"

#include <algorithm>
#include <cwctype>
#include <set>
#include <shlobj.h>
#include <wil/resource.h>
#include <common/utils/gpo.h>

namespace WorkspacesCli
{
    Error::Error(int exitCode, std::wstring code, const char* message) :
        std::runtime_error(message), exitCode(exitCode), code(std::move(code))
    {
    }

    std::wstring NormalizeId(const std::wstring& id)
    {
        std::wstring value = id;
        if (value.size() == 38 && value.front() == L'{' && value.back() == L'}')
        {
            value = value.substr(1, 36);
        }
        if (value.size() != 36)
        {
            throw Error(2, L"invalidArguments", "Expected a workspace GUID.");
        }
        for (size_t i = 0; i < value.size(); ++i)
        {
            if (i == 8 || i == 13 || i == 18 || i == 23)
            {
                if (value[i] != L'-')
                    throw Error(2, L"invalidArguments", "Invalid GUID separator.");
            }
            else if (!((value[i] >= L'0' && value[i] <= L'9') ||
                       (value[i] >= L'a' && value[i] <= L'f') ||
                       (value[i] >= L'A' && value[i] <= L'F')))
            {
                throw Error(2, L"invalidArguments", "Invalid GUID digit.");
            }
        }
        std::transform(value.begin(), value.end(), value.begin(), [](wchar_t c) { return static_cast<wchar_t>(std::towlower(c)); });
        return value;
    }

    bool WantsJson(const std::vector<std::wstring>& arguments)
    {
        for (size_t i = 1; i < arguments.size(); ++i)
        {
            if (arguments[i] == L"--json")
                return true;
            if (arguments[i] == L"--id" || arguments[i] == L"--name" || arguments[i] == L"--timeout")
                ++i;
        }
        return false;
    }

    Options Parse(const std::vector<std::wstring>& arguments)
    {
        Options result;
        if (arguments.empty())
            throw Error(2, L"invalidArguments", "Expected list or launch. Use --help.");
        result.command = arguments.front();
        if (arguments.size() == 2 && (result.command == L"list" || result.command == L"launch") && arguments[1] == L"--help")
        {
            result.command = L"--help";
            return result;
        }
        if (result.command != L"list" && result.command != L"launch" &&
            result.command != L"--help" && result.command != L"--version")
            throw Error(2, L"invalidArguments", "Unknown command.");

        std::set<std::wstring> seen;
        for (size_t i = 1; i < arguments.size(); ++i)
        {
            const auto& option = arguments[i];
            if (!seen.insert(option).second)
                throw Error(2, L"invalidArguments", "Duplicate option.");
            if (option == L"--json")
                result.json = true;
            else if (option == L"--details")
                result.details = true;
            else if (option == L"--id" || option == L"--name" || option == L"--timeout")
            {
                if (++i == arguments.size() || arguments[i].empty())
                    throw Error(2, L"invalidArguments", "Missing option value.");
                if (option == L"--id")
                {
                    NormalizeId(arguments[i]);
                    result.id = arguments[i];
                }
                else if (option == L"--name")
                {
                    if (std::all_of(arguments[i].begin(), arguments[i].end(), [](wchar_t c) { return std::iswspace(c) != 0; }))
                        throw Error(2, L"invalidArguments", "Workspace name must not be blank.");
                    result.name = arguments[i];
                }
                else
                {
                    const auto& value = arguments[i];
                    if (value.size() > 3 || !std::all_of(value.begin(), value.end(), [](wchar_t c) { return c >= L'0' && c <= L'9'; }))
                        throw Error(2, L"invalidArguments", "Timeout must be an integer from 1 to 600 seconds.");
                    result.timeoutSeconds = static_cast<DWORD>(std::stoul(value));
                    if (!result.timeoutSeconds || result.timeoutSeconds > 600)
                        throw Error(2, L"invalidArguments", "Timeout must be from 1 to 600 seconds.");
                }
            }
            else
                throw Error(2, L"invalidArguments", "Unknown option.");
        }
        const bool selected = !result.id.empty() || !result.name.empty();
        if ((!result.id.empty() && !result.name.empty()) ||
            (result.command == L"launch" && (!selected || result.details)) ||
            (result.command == L"list" && (seen.contains(L"--timeout") || (result.details && !selected))) ||
            (result.command.starts_with(L"--") && arguments.size() != 1))
            throw Error(2, L"invalidArguments", "Invalid combination of command options.");
        return result;
    }

    std::filesystem::path SettingsRoot()
    {
        wil::unique_cotaskmem_string local;
        winrt::check_hresult(SHGetKnownFolderPath(FOLDERID_LocalAppData, KF_FLAG_DONT_VERIFY, nullptr, &local));
        return std::filesystem::path(local.get()) / L"Microsoft" / L"PowerToys";
    }

    std::optional<json::JsonObject> ReadJson(const std::filesystem::path& path)
    {
        wil::unique_handle file(CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
        if (!file)
        {
            const auto error = GetLastError();
            if (error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND)
                return std::nullopt;
            throw Error(8, L"storageError", "Cannot read the configuration file.");
        }
        LARGE_INTEGER size{};
        if (!GetFileSizeEx(file.get(), &size) || size.QuadPart <= 0 || size.QuadPart > 16 * 1024 * 1024)
            throw Error(8, L"invalidData", "Configuration is empty or exceeds the supported size.");
        std::string bytes(static_cast<size_t>(size.QuadPart), '\0');
        DWORD read{};
        if (!ReadFile(file.get(), bytes.data(), static_cast<DWORD>(bytes.size()), &read, nullptr) || read != bytes.size())
            throw Error(8, L"storageError", "Configuration changed or could not be read completely.");
        try
        {
            if (bytes.starts_with("\xEF\xBB\xBF"))
                bytes.erase(0, 3);
            return json::JsonObject::Parse(winrt::to_hstring(bytes));
        }
        catch (const winrt::hresult_error&)
        {
            throw Error(8, L"invalidData", "Configuration contains invalid JSON.");
        }
    }

    void CheckUserSetting(const std::optional<json::JsonObject>& settings)
    {
        bool enabled = false;
        try
        {
            if (settings && settings->HasKey(L"enabled"))
                enabled = settings->GetNamedObject(L"enabled").GetNamedBoolean(L"Workspaces", false);
        }
        catch (const winrt::hresult_error&)
        {
            throw Error(8, L"invalidData", "Invalid module enablement configuration.");
        }
        if (!enabled)
            throw Error(6, L"disabledByUser", "Workspaces is disabled in PowerToys Settings.");
    }

    void CheckEnabled()
    {
        const auto policy = powertoys_gpo::getConfiguredWorkspacesEnabledValue();
        if (policy == powertoys_gpo::gpo_rule_configured_disabled)
            throw Error(6, L"disabledByPolicy", "Workspaces is disabled by policy.");
        if (policy == powertoys_gpo::gpo_rule_configured_enabled)
            return;
        if (policy != powertoys_gpo::gpo_rule_configured_not_configured)
            throw Error(6, L"policyError", "Cannot determine the Workspaces policy.");
        CheckUserSetting(ReadJson(SettingsRoot() / L"settings.json"));
    }

    std::vector<WorkspacesData::WorkspacesProject> LoadWorkspaces()
    {
        const auto data = ReadJson(SettingsRoot() / L"Workspaces" / L"workspaces.json");
        if (!data)
            return {};
        auto parsed = WorkspacesData::WorkspacesListJSON::FromJson(*data);
        if (!parsed)
            throw Error(8, L"invalidData", "Invalid workspace configuration.");
        return *parsed;
    }

    std::vector<WorkspacesData::WorkspacesProject> Select(
        const std::vector<WorkspacesData::WorkspacesProject>& workspaces,
        const Options& options)
    {
        std::set<std::wstring> ids;
        std::vector<WorkspacesData::WorkspacesProject> result;
        for (const auto& project : workspaces)
        {
            std::wstring id;
            try
            {
                id = NormalizeId(project.id);
            }
            catch (const Error&)
            {
                throw Error(8, L"invalidData", "A saved workspace has an invalid ID.");
            }
            if (!ids.insert(id).second || project.name.empty())
                throw Error(8, L"invalidData", "Workspace IDs must be unique and names must be present.");
            if ((!options.id.empty() && id == NormalizeId(options.id)) ||
                (!options.name.empty() && CompareStringOrdinal(project.name.c_str(), static_cast<int>(project.name.size()), options.name.c_str(), static_cast<int>(options.name.size()), TRUE) == CSTR_EQUAL) ||
                (options.id.empty() && options.name.empty()))
                result.push_back(project);
        }
        if ((!options.id.empty() || !options.name.empty()) && result.empty())
            throw Error(3, L"workspaceNotFound", "No workspace matches the selector.");
        if (!options.name.empty() && result.size() > 1)
            throw Error(4, L"ambiguousName", "More than one workspace has this name. Select by ID.");
        std::sort(result.begin(), result.end(), [](const auto& a, const auto& b) { return NormalizeId(a.id) < NormalizeId(b.id); });
        return result;
    }

    json::JsonObject Envelope(const std::wstring& command, const std::wstring& state, const json::JsonObject& result)
    {
        json::JsonObject output;
        output.SetNamedValue(L"schemaVersion", json::value(1));
        output.SetNamedValue(L"command", json::value(command));
        output.SetNamedValue(L"state", json::value(state));
        output.SetNamedValue(L"result", result);
        return output;
    }

    json::JsonObject Failure(const std::wstring& command, const Error& error)
    {
        json::JsonObject detail;
        detail.SetNamedValue(L"code", json::value(error.code));
        detail.SetNamedValue(L"message", json::value(winrt::to_hstring(error.what())));
        detail.SetNamedValue(L"exitCode", json::value(error.exitCode));
        json::JsonObject output;
        output.SetNamedValue(L"schemaVersion", json::value(1));
        output.SetNamedValue(L"command", json::value(command));
        output.SetNamedValue(L"state", json::value(L"failed"));
        output.SetNamedValue(L"error", detail);
        return output;
    }

    json::JsonObject ListResult(const std::vector<WorkspacesData::WorkspacesProject>& workspaces, bool details)
    {
        json::JsonArray items;
        for (const auto& project : workspaces)
        {
            auto item = WorkspacesData::WorkspacesProjectJSON::ToJson(project);
            if (!details)
            {
                json::JsonObject summary;
                summary.SetNamedValue(L"id", item.GetNamedValue(L"id"));
                summary.SetNamedValue(L"name", item.GetNamedValue(L"name"));
                json::JsonArray apps;
                for (const auto& value : item.GetNamedArray(L"applications"))
                {
                    json::JsonObject app;
                    app.SetNamedValue(L"application", value.GetObjectW().GetNamedValue(L"application"));
                    apps.Append(app);
                }
                summary.SetNamedValue(L"applications", apps);
                item = summary;
            }
            items.Append(item);
        }
        json::JsonObject result;
        result.SetNamedValue(L"view", json::value(details ? L"detail" : L"summary"));
        result.SetNamedValue(L"workspaces", items);
        return result;
    }

    json::JsonObject LaunchResult(const WorkspacesData::WorkspacesProject& project,
                                  const WorkspacesData::LaunchingAppStateMap& states,
                                  const std::wstring& operationId,
                                  int& exitCode,
                                  const std::map<std::wstring, DWORD>& errors,
                                  const std::map<std::wstring, LaunchDecision>& approvals)
    {
        json::JsonArray apps;
        size_t arranged = 0;
        for (const auto& app : project.apps)
        {
            const auto entry = states.find(app);
            const auto state = entry == states.end() ? LaunchingState::Waiting : entry->second.state;
            const wchar_t* name = L"unknown";
            if (state == LaunchingState::LaunchedAndMoved)
            {
                name = L"arranged";
                ++arranged;
            }
            else if (state == LaunchingState::Failed)
                name = errors.contains(app.id) ? L"launchFailed" : L"arrangeFailed";
            else if (state == LaunchingState::Canceled)
                name = L"canceled";
            else if (state == LaunchingState::Skipped)
                name = L"skipped";
            else if (state == LaunchingState::Launched)
                name = L"arrangeFailed";
            json::JsonObject item;
            item.SetNamedValue(L"id", json::value(app.id));
            item.SetNamedValue(L"state", json::value(name));
            const auto approval = approvals.find(app.id);
            if (state == LaunchingState::Skipped)
            {
                item.SetNamedValue(L"code", json::value(L"skipped"));
            }
            else if (approval != approvals.end() && approval->second != LaunchDecision::Approved)
            {
                const wchar_t* code = L"confirmationFailed";
                if (approval->second == LaunchDecision::UiUnavailable)
                    code = L"confirmationRequired";
                else if (approval->second == LaunchDecision::TimedOut)
                    code = L"confirmationTimedOut";
                else if (approval->second == LaunchDecision::Canceled)
                    code = L"canceled";
                item.SetNamedValue(L"code", json::value(code));
            }
            else if (const auto error = errors.find(app.id); error != errors.end())
            {
                item.SetNamedValue(L"code", json::value(error->second == ERROR_CANCELLED ? L"consentDenied" : L"launchFailed"));
                if (error->second != ERROR_SUCCESS)
                    item.SetNamedValue(L"nativeError", json::value(error->second));
            }
            apps.Append(item);
        }
        exitCode = arranged == project.apps.size() ? 0 : 10;
        json::JsonObject result;
        result.SetNamedValue(L"workspaceId", json::value(project.id));
        result.SetNamedValue(L"operationId", json::value(operationId));
        result.SetNamedValue(L"applications", apps);
        result.SetNamedValue(L"persistenceStatus", json::value(L"unchanged"));
        auto output = Envelope(L"launch", exitCode == 0 ? L"completed" : (arranged == 0 ? L"failed" : L"partial"), result);
        output.SetNamedValue(L"warnings", json::JsonArray{});
        return output;
    }
}
