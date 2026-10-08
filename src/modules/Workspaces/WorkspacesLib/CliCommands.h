// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#pragma once

#include <filesystem>
#include <map>
#include <stdexcept>
#include <string>
#include <vector>

#include "WorkspacesData.h"
#include "LaunchDecision.h"

namespace WorkspacesCli
{
    inline constexpr DWORD MaxPayload = 1024 * 1024;

    struct Error : std::runtime_error
    {
        int exitCode;
        std::wstring code;

        Error(int exitCode, std::wstring code, const char* message);
    };

    struct Options
    {
        std::wstring command;
        std::wstring id;
        std::wstring name;
        bool json = false;
        bool details = false;
        DWORD timeoutSeconds = 120;
    };

    Options Parse(const std::vector<std::wstring>& arguments);
    bool WantsJson(const std::vector<std::wstring>& arguments);
    std::wstring NormalizeId(const std::wstring& id);
    std::filesystem::path SettingsRoot();
    std::optional<json::JsonObject> ReadJson(const std::filesystem::path& path);
    void CheckEnabled();
    void CheckUserSetting(const std::optional<json::JsonObject>& settings);
    std::vector<WorkspacesData::WorkspacesProject> LoadWorkspaces();
    std::vector<WorkspacesData::WorkspacesProject> Select(
        const std::vector<WorkspacesData::WorkspacesProject>& workspaces,
        const Options& options);
    json::JsonObject ListResult(const std::vector<WorkspacesData::WorkspacesProject>& workspaces, bool details);
    json::JsonObject Envelope(const std::wstring& command, const std::wstring& state, const json::JsonObject& result);
    json::JsonObject Failure(const std::wstring& command, const Error& error);
    json::JsonObject LaunchResult(
        const WorkspacesData::WorkspacesProject& project,
        const WorkspacesData::LaunchingAppStateMap& states,
        const std::wstring& operationId,
        int& exitCode,
        const std::map<std::wstring, DWORD>& errors = {},
        const std::map<std::wstring, LaunchDecision>& approvals = {});
}
