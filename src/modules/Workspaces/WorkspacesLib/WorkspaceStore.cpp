// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include "WorkspaceStore.h"
#include "CliCommands.h"

#include <wil/resource.h>
#include <objbase.h>
#include <common/logger/logger.h>

namespace WorkspaceStore
{
    namespace
    {
        struct VerificationError : std::runtime_error
        {
            VerificationError() : std::runtime_error("Workspace replacement could not be verified") {}
        };

        wil::unique_handle Lock(const std::filesystem::path& path)
        {
            const auto lockPath = path.wstring() + L".lock";
            const auto deadline = GetTickCount64() + 2000;
            for (;;)
            {
                wil::unique_handle handle(CreateFileW(lockPath.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr));
                if (handle)
                    return handle;
                if (GetLastError() != ERROR_SHARING_VIOLATION || GetTickCount64() >= deadline)
                    throw std::runtime_error("Workspace store lock unavailable");
                Sleep(20);
            }
        }

        void Commit(const std::filesystem::path& path, const json::JsonObject& data)
        {
            GUID guid{};
            winrt::check_hresult(CoCreateGuid(&guid));
            wchar_t id[40]{};
            StringFromGUID2(guid, id, ARRAYSIZE(id));
            const auto temporary = path.wstring() + id + L".tmp";
            wil::unique_handle file(CreateFileW(temporary.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_WRITE_THROUGH, nullptr));
            if (!file)
                throw std::runtime_error("Workspace temporary file unavailable");
            auto cleanup = wil::scope_exit([&] {
                file.reset();
                DeleteFileW(temporary.c_str());
            });
            const auto bytes = winrt::to_string(data.Stringify());
            DWORD written = 0;
            if (bytes.size() > 16 * 1024 * 1024 ||
                !WriteFile(file.get(), bytes.data(), static_cast<DWORD>(bytes.size()), &written, nullptr) ||
                written != bytes.size() || !FlushFileBuffers(file.get()))
                throw std::runtime_error("Workspace write failed");
            file.reset();
            if (!MoveFileExW(temporary.c_str(), path.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
                throw std::runtime_error("Workspace replacement failed");
            try
            {
                const auto actual = WorkspacesCli::ReadJson(path);
                if (!actual || actual->Stringify() != data.Stringify())
                    throw VerificationError();
            }
            catch (const std::exception&)
            {
                throw VerificationError();
            }
            catch (const winrt::hresult_error&)
            {
                throw VerificationError();
            }
        }

        void PreserveHistory(const json::JsonObject& current, const json::JsonObject& incoming)
        {
            const auto existing = current.GetNamedArray(L"workspaces");
            for (const auto& value : incoming.GetNamedArray(L"workspaces"))
            {
                const auto item = value.GetObjectW();
                const auto id = item.GetNamedString(L"id");
                for (const auto& previous : existing)
                {
                    const auto old = previous.GetObjectW();
                    if (CompareStringOrdinal(id.c_str(), -1, old.GetNamedString(L"id").c_str(), -1, TRUE) == CSTR_EQUAL &&
                        old.HasKey(L"last-launched-time"))
                    {
                        const auto oldTime = old.GetNamedNumber(L"last-launched-time");
                        if (!item.HasKey(L"last-launched-time") || oldTime > item.GetNamedNumber(L"last-launched-time"))
                            item.SetNamedValue(L"last-launched-time", old.GetNamedValue(L"last-launched-time"));
                        break;
                    }
                }
            }
        }
    }

    bool Write(const std::filesystem::path& fileName, const json::JsonObject& data)
    {
        try
        {
            wil::unique_handle lock;
            const auto incoming = json::JsonObject::Parse(data.Stringify());
            if (fileName.filename() == L"workspaces.json")
            {
                lock = Lock(fileName);
                const auto current = WorkspacesCli::ReadJson(fileName);
                if (current)
                    PreserveHistory(*current, incoming);
            }
            Commit(fileName, incoming);
            return true;
        }
        catch (const winrt::hresult_error&)
        {
            Logger::error("Workspace JSON could not be saved");
        }
        catch (const std::exception&)
        {
            Logger::error("Workspace file could not be saved");
        }
        return false;
    }

    UpdateResult UpdateLastLaunched(const std::filesystem::path& fileName, const std::wstring& workspaceId, time_t timestamp)
    {
        try
        {
            auto lock = Lock(fileName);
            const auto current = WorkspacesCli::ReadJson(fileName);
            if (!current)
                return UpdateResult::Conflict;
            json::JsonObject selected{ nullptr };
            for (const auto& value : current->GetNamedArray(L"workspaces"))
            {
                const auto item = value.GetObjectW();
                if (WorkspacesCli::NormalizeId(item.GetNamedString(L"id").c_str()) ==
                    WorkspacesCli::NormalizeId(workspaceId))
                {
                    if (selected)
                        return UpdateResult::Conflict;
                    selected = item;
                }
            }
            if (!selected)
                return UpdateResult::Conflict;
            selected.SetNamedValue(L"last-launched-time", json::value(static_cast<int64_t>(timestamp)));
            Commit(fileName, *current);
            return UpdateResult::Updated;
        }
        catch (const VerificationError&)
        {
            Logger::error("Workspace replacement completed but verification failed");
            return UpdateResult::Unverified;
        }
        catch (const winrt::hresult_error&)
        {
            Logger::warn("Workspace launch metadata could not be saved");
        }
        catch (const std::exception&)
        {
            Logger::warn("Workspace launch metadata could not be saved");
        }
        return UpdateResult::Failed;
    }
}
