#include "pch.h"
#include "JsonUtils.h"

#include <filesystem>

#include <common/logger/logger.h>

#include "../WorkspacesSettingsClient/PTSettingsClient.h"

namespace JsonUtils
{
    Result<WorkspacesData::WorkspacesProject, WorkspacesFileError> ReadSingleWorkspace(const std::wstring& fileName)
    {
        if (std::filesystem::exists(fileName))
        {
            try
            {
                auto tempWorkspacesJson = json::from_file(fileName);
                if (tempWorkspacesJson.has_value())
                {
                    auto tempWorkspace = WorkspacesData::WorkspacesProjectJSON::FromJson(tempWorkspacesJson.value());
                    if (tempWorkspace.has_value())
                    {
                        return Ok(tempWorkspace.value());
                    }
                    else
                    {
                        Logger::critical("Incorrect Workspaces file");
                        return Error(WorkspacesFileError::IncorrectFileError);
                    }
                }
                else
                {
                    Logger::critical("Incorrect Workspaces file");
                    return Error(WorkspacesFileError::IncorrectFileError);
                }
            }
            catch (std::exception ex)
            {
                Logger::critical("Exception on reading Workspaces file: {}", ex.what());
                return Error(WorkspacesFileError::FileReadingError);
            }
        }

        return Ok(WorkspacesData::WorkspacesProject{});
    }

    Result<std::vector<WorkspacesData::WorkspacesProject>, WorkspacesFileError> ReadWorkspaces(const std::wstring& fileName)
    {
        try
        {
            auto savedWorkspacesJson = json::from_file(fileName);
            if (savedWorkspacesJson.has_value())
            {
                auto savedWorkspaces = WorkspacesData::WorkspacesListJSON::FromJson(savedWorkspacesJson.value());
                if (savedWorkspaces.has_value())
                {
                    return Ok(savedWorkspaces.value());
                }
                else
                {
                    Logger::critical("Incorrect Workspaces file");
                    return Error(WorkspacesFileError::IncorrectFileError);
                }
            }
            else
            {
                Logger::critical("Incorrect Workspaces file");
                return Error(WorkspacesFileError::IncorrectFileError);
            }
        }
        catch (std::exception ex)
        {
            Logger::critical("Exception on reading Workspaces file: {}", ex.what());
            return Error(WorkspacesFileError::FileReadingError);
        }
    }

    bool Write(const std::wstring& fileName, const std::vector<WorkspacesData::WorkspacesProject>& projects)
    {
        try
        {
            json::to_file(fileName, WorkspacesData::WorkspacesListJSON::ToJson(projects));
        }
        catch (std::exception ex)
        {
            Logger::error("Error writing workspaces file. {}", ex.what());
            return false;
        }

        return true;
    }

    Result<std::vector<WorkspacesData::WorkspacesProject>, WorkspacesFileError> ReadWorkspacesFromService()
    {
        std::vector<uint8_t> bytes;
        auto rc = PTSettingsClient::GetBlob(bytes);
        switch (rc)
        {
        case PTSettingsClient::Result::Ok:
        {
            try
            {
                // The blob is the same UTF-8 JSON the Editor writes.
                std::string utf8(bytes.begin(), bytes.end());
                auto obj = json::JsonValue::Parse(winrt::to_hstring(utf8)).GetObjectW();
                auto parsed = WorkspacesData::WorkspacesListJSON::FromJson(obj);
                if (parsed.has_value())
                {
                    return Ok(parsed.value());
                }
                Logger::critical("Incorrect Workspaces blob from service");
                return Error(WorkspacesFileError::IncorrectFileError);
            }
            catch (std::exception ex)
            {
                Logger::critical("Exception parsing Workspaces blob: {}", ex.what());
                return Error(WorkspacesFileError::FileReadingError);
            }
        }

        case PTSettingsClient::Result::NotFound:
            // Service is up but this user has no blob yet (first run).
            return Ok(std::vector<WorkspacesData::WorkspacesProject>{});

        case PTSettingsClient::Result::ServiceUnavailable:
            // No service (no-admin / declined-UAC): legacy file fallback.
            return ReadWorkspaces(WorkspacesData::WorkspacesFile());

        default:
            // AuthRejected / Protocol / IoError: the protected settings EXIST but
            // this caller could not read them (e.g. the service rejected this
            // app's version/signature — common transiently right after a PowerToys
            // update, before re-provisioning).  Surface a distinct error so the
            // caller does NOT misreport this as an empty workspace list (which
            // would be both inaccurate and alarming) — Design §10 / UX.
            Logger::error("GetBlob failed ({}); reporting ServiceAccessError.", static_cast<int>(rc));
            return Error(WorkspacesFileError::ServiceAccessError);
        }
    }

    bool WriteWorkspacesToService(const std::vector<WorkspacesData::WorkspacesProject>& projects)
    {
        try
        {
            std::wstring str{ WorkspacesData::WorkspacesListJSON::ToJson(projects).Stringify().c_str() };
            std::string utf8 = winrt::to_string(winrt::hstring(str));
            std::vector<uint8_t> bytes(utf8.begin(), utf8.end());

            auto rc = PTSettingsClient::PutBlob(bytes);
            if (rc == PTSettingsClient::Result::Ok)
            {
                return true;
            }
            if (rc == PTSettingsClient::Result::ServiceUnavailable)
            {
                // No service: legacy file fallback (no-admin / declined-UAC).
                return Write(WorkspacesData::WorkspacesFile(), projects);
            }
            Logger::error("PutBlob failed ({}) writing workspaces.", static_cast<int>(rc));
            return false;
        }
        catch (std::exception ex)
        {
            Logger::error("Exception writing workspaces via service: {}", ex.what());
            return false;
        }
    }

    bool Write(const std::wstring& fileName, const WorkspacesData::WorkspacesProject& project)
    {
        try
        {
            json::to_file(fileName, WorkspacesData::WorkspacesProjectJSON::ToJson(project));
        }
        catch (std::exception ex)
        {
            Logger::error("Error writing workspaces file. {}", ex.what());
            return false;
        }

        return true;
    }
}
