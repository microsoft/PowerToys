// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#pragma once
#include "..\ProtectedStorage.Setup\SetupSupport.h"
#include "..\ProtectedStorage.Common\Common.h"
#include <algorithm>
#include <optional>
#include <taskschd.h>
#include <wrl/client.h>

namespace PowerToysProtectedStorage::ProfileCleanup
{
    struct Profile
    {
        std::wstring sid;
        std::wstring leaf;
        DWORD volume = 0;
        ULONGLONG file = 0;
    };

    inline bool IsVirtualSid(const std::wstring& sid)
    {
        PSID raw = nullptr;
        if (!ConvertStringSidToSidW(sid.c_str(), &raw))
            return false;
        const SID_IDENTIFIER_AUTHORITY nt = SECURITY_NT_AUTHORITY;
        const bool valid = IsValidSid(raw) && *GetSidSubAuthorityCount(raw) == 6 &&
                           memcmp(GetSidIdentifierAuthority(raw), &nt, sizeof(nt)) == 0 &&
                           *GetSidSubAuthority(raw, 0) == SECURITY_SERVICE_ID_BASE_RID && sid_string(raw) == sid;
        LocalFree(raw);
        return valid;
    }

    inline void ValidateLeaf(const std::wstring& leaf, const std::wstring& service)
    {
        if (leaf == service)
            return;
        if (leaf.size() <= service.size() + 1 || leaf.substr(0, service.size() + 1) != service + L"." ||
            leaf.find_first_not_of(L"0123456789", service.size() + 1) != std::wstring::npos)
            throw failure("profile is not the exact service's profile", ERROR_INVALID_OWNER);
    }

    inline std::filesystem::path WindowsDirectory()
    {
        wchar_t path[MAX_PATH]{};
        const auto length = GetWindowsDirectoryW(path, MAX_PATH);
        check(length && length < MAX_PATH, "Windows directory");
        return path;
    }

    inline std::filesystem::path ProfilePath(const Profile& profile)
    {
        return WindowsDirectory() / L"ServiceProfiles" / profile.leaf;
    }

    inline bool Exists(const std::filesystem::path& path)
    {
        if (GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES)
            return true;
        const auto error = GetLastError();
        if (error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND)
            return false;
        throw failure("inspect profile cleanup path", error);
    }

    inline std::optional<Profile> Inspect(const std::wstring& sid, const std::wstring& service, const std::wstring& fallbackLeaf = L"")
    {
        if (!IsVirtualSid(sid))
            throw failure("only a virtual service profile may be removed", ERROR_INVALID_SID);
        Profile profile{ sid, fallbackLeaf.empty() ? service : fallbackLeaf };
        ValidateLeaf(profile.leaf, service);
        HKEY raw = nullptr;
        const auto key = L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList\\" + sid;
        const auto opened = RegOpenKeyExW(HKEY_LOCAL_MACHINE, key.c_str(), 0, KEY_READ | KEY_WOW64_64KEY, &raw);
        if (opened != ERROR_SUCCESS && opened != ERROR_FILE_NOT_FOUND)
            throw failure("inspect virtual profile registration", opened);
        if (raw)
        {
            struct Close
            {
                HKEY key;
                ~Close() { RegCloseKey(key); }
            } close{ raw };
            wchar_t value[32768]{}, expanded[32768]{};
            DWORD bytes = sizeof(value);
            result(RegGetValueW(raw, nullptr, L"ProfileImagePath", RRF_RT_REG_SZ | RRF_RT_REG_EXPAND_SZ | RRF_NOEXPAND, nullptr, value, &bytes), "read virtual profile path");
            const auto length = ExpandEnvironmentStringsW(value, expanded, static_cast<DWORD>(std::size(expanded)));
            check(length && length <= std::size(expanded), "expand virtual profile path");
            const std::filesystem::path path(expanded);
            if (_wcsicmp(path.parent_path().c_str(), (WindowsDirectory() / L"ServiceProfiles").c_str()))
                throw failure("profile outside the service-profile root", ERROR_INVALID_OWNER);
            profile.leaf = path.filename().wstring();
            ValidateLeaf(profile.leaf, service);
        }
        const auto path = ProfilePath(profile);
        for (const auto& ancestor : { WindowsDirectory(), WindowsDirectory() / L"ServiceProfiles" })
        {
            const auto flags = GetFileAttributesW(ancestor.c_str());
            if (flags == INVALID_FILE_ATTRIBUTES || !(flags & FILE_ATTRIBUTE_DIRECTORY) || (flags & FILE_ATTRIBUTE_REPARSE_POINT))
                throw failure("unsafe service-profile ancestor", ERROR_ACCESS_DENIED);
        }
        handle directory(CreateFileW(path.c_str(), FILE_READ_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        if (directory.value == INVALID_HANDLE_VALUE)
        {
            const auto error = GetLastError();
            if (error != ERROR_FILE_NOT_FOUND && error != ERROR_PATH_NOT_FOUND)
                throw failure("open virtual profile directory", error);
            return opened == ERROR_FILE_NOT_FOUND ? std::nullopt : std::optional<Profile>(profile);
        }
        BY_HANDLE_FILE_INFORMATION info{};
        check(GetFileInformationByHandle(directory.value, &info) != FALSE, "virtual profile directory identity");
        if (!(info.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) || (info.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT))
            throw failure("unsafe virtual profile directory", ERROR_ACCESS_DENIED);
        profile.volume = info.dwVolumeSerialNumber;
        profile.file = (static_cast<ULONGLONG>(info.nFileIndexHigh) << 32) | info.nFileIndexLow;
        return profile;
    }

    inline bool SameProfile(const Profile& expected, const Profile& current)
    {
        return expected.sid == current.sid && expected.leaf == current.leaf &&
               (!current.file || (expected.volume == current.volume && expected.file == current.file));
    }

    inline bool HasRegistryKey(HKEY root, const std::wstring& name)
    {
        HKEY key = nullptr;
        const auto opened = RegOpenKeyExW(root, name.c_str(), 0, KEY_READ | KEY_WOW64_64KEY, &key);
        if (key)
            RegCloseKey(key);
        if (opened == ERROR_SUCCESS)
            return true;
        if (opened == ERROR_FILE_NOT_FOUND)
            return false;
        throw failure("inspect remaining profile hive", opened);
    }

    inline void RemoveTree(const std::filesystem::path& path, unsigned depth = 0, const Profile* expected = nullptr)
    {
        if (!Exists(path))
            return;
        if (depth > 32)
            throw failure("unexpected private tree depth", ERROR_DIRECTORY);
        handle object(CreateFileW(path.c_str(), DELETE | FILE_READ_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_BACKUP_SEMANTICS, nullptr));
        check(object.value != INVALID_HANDLE_VALUE, "open exact removal object");
        BY_HANDLE_FILE_INFORMATION attributes{};
        check(GetFileInformationByHandle(object.value, &attributes) != FALSE, "removal identity");
        if (expected && (!(attributes.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) ||
                         (attributes.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) || attributes.dwVolumeSerialNumber != expected->volume ||
                         ((static_cast<ULONGLONG>(attributes.nFileIndexHigh) << 32) | attributes.nFileIndexLow) != expected->file))
            throw failure("orphan profile directory was replaced", ERROR_INVALID_OWNER);
        if ((attributes.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) && !(attributes.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT))
        {
            WIN32_FIND_DATAW found{};
            HANDLE find = FindFirstFileW((path / L"*").c_str(), &found);
            if (find == INVALID_HANDLE_VALUE)
            {
                if (GetLastError() != ERROR_FILE_NOT_FOUND)
                    throw failure("enumerate exact removal directory", GetLastError());
            }
            else
            {
                struct Close
                {
                    HANDLE value;
                    ~Close() { FindClose(value); }
                } close{ find };
                do
                {
                    if (wcscmp(found.cFileName, L".") && wcscmp(found.cFileName, L".."))
                        RemoveTree(path / found.cFileName, depth + 1);
                } while (FindNextFileW(find, &found));
                if (GetLastError() != ERROR_NO_MORE_FILES)
                    throw failure("FindNextFile", GetLastError());
            }
        }
        FILE_DISPOSITION_INFO disposition{ TRUE };
        check(SetFileInformationByHandle(object.value, FileDispositionInfo, &disposition, sizeof(disposition)) != FALSE,
              "delete exact object by handle");
    }

    template<typename Attempt, typename Clock, typename Wait>
    DWORD Retry(Attempt&& attempt, Clock&& clock, Wait&& wait, ULONGLONG budget)
    {
        const auto started = clock();
        for (;;)
        {
            const DWORD error = attempt();
            const auto elapsed = clock() - started;
            if ((error != ERROR_SHARING_VIOLATION && error != ERROR_BUSY) || elapsed >= budget)
                return error;
            wait(static_cast<DWORD>((std::min)(ULONGLONG{ 250 }, budget - elapsed)));
        }
    }

    inline DWORD Remove(const Profile& expected, const std::wstring& service, ULONGLONG budget = 5000)
    {
        return Retry([&] {
            const auto current = Inspect(expected.sid, service, expected.leaf);
            if (!current) return DWORD{ ERROR_SUCCESS };
            if (!SameProfile(expected, *current)) return DWORD{ ERROR_INVALID_OWNER };
            const BOOL removed = DeleteProfileW(expected.sid.c_str(), ProfilePath(expected).c_str(), nullptr);
            const auto error = removed ? ERROR_SUCCESS : GetLastError();
            // DeleteProfile can remove ProfileList before encountering a locked
            // file. A later call then returns FILE_NOT_FOUND and leaves the directory.
            if (!HasRegistryKey(HKEY_LOCAL_MACHINE, L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList\\" + expected.sid) &&
                !HasRegistryKey(HKEY_USERS, expected.sid) && !HasRegistryKey(HKEY_USERS, expected.sid + L"_Classes"))
            {
                try { RemoveTree(ProfilePath(expected), 0, &expected); }
                catch (const failure& failure) { return failure.code; }
            }
            if (!Inspect(expected.sid, service, expected.leaf)) return DWORD{ ERROR_SUCCESS };
            return error ? error : DWORD{ ERROR_DIR_NOT_EMPTY }; }, [] { return GetTickCount64(); }, [](DWORD delay) { Sleep(delay); }, budget);
    }

    inline bool OwnsRequest(const std::wstring& expected, const std::wstring& current, bool active, bool installing, bool serviceExists)
    {
        return setup::valid_id(expected) && expected == current && !active && !installing && !serviceExists;
    }

    inline std::wstring QuoteCommandPath(const std::filesystem::path& path)
    {
        const auto text = path.wstring();
        if (!path.is_absolute() || text.find_first_of(L"\"%!^&|<>\r\n") != std::wstring::npos)
            throw failure("unsafe fixed cleanup command path", ERROR_INVALID_NAME);
        return L"\"" + text + L"\"";
    }

    inline std::filesystem::path SystemExecutable(const wchar_t* leaf)
    {
        wchar_t path[MAX_PATH]{};
        const auto length = GetSystemDirectoryW(path, MAX_PATH);
        check(length && length < MAX_PATH, "system executable directory");
        return std::filesystem::path(path) / leaf;
    }

    inline std::wstring TaskName(const std::wstring& operation)
    {
        if (!setup::valid_id(operation))
            throw failure("profile cleanup operation", ERROR_INVALID_DATA);
        return L"PowerToys-ProtectedStorage-ProfileCleanup-" + operation;
    }

    inline std::wstring TaskCommand(const std::filesystem::path& stage, const std::wstring& owner, const std::wstring& operation)
    {
        canonical_owner(owner);
        const auto task = TaskName(operation);
        const auto script = QuoteCommandPath(stage.parent_path() / (operation + L".cmd"));
        const auto directory = QuoteCommandPath(stage);
        const auto scheduler = QuoteCommandPath(SystemExecutable(L"schtasks.exe"));
        return L"/d /v:off /s /c \"(if exist " + script + L" (call " + script + L" " + scheduler +
               L") else (if exist " + directory + L" (exit /b 1) else (" + scheduler + L" /Delete /TN \"" + task + L"\" /F)))\"";
    }

    inline std::wstring TaskScript(const std::wstring& owner, const std::wstring& operation)
    {
        canonical_owner(owner);
        const auto task = TaskName(operation);
        // Fixed ASCII script generated by the signed SYSTEM lifecycle, not user
        // input. Relative paths preserve Unicode roots and stay in this operation.
        return L"@echo off\r\nsetlocal DisableDelayedExpansion\r\n"
               L"if not exist \"%~dpn0\" goto retire\r\n"
               L"if exist \"%~dpn0.done\" goto remove\r\n"
               L"start \"\" /b /wait \"%~dpn0\\Cleanup.exe\" retry-profile " +
               owner + L" " + operation + L"\r\n"
                                          L"if errorlevel 1 exit /b 1\r\n"
                                          L"if not exist \"%~dpn0.done\" exit /b 1\r\n"
                                          L":remove\r\n"
                                          L"if exist \"%~dpn0\\Cleanup.exe\" del /q \"%~dpn0\\Cleanup.exe\"\r\n"
                                          L"if exist \"%~dpn0\\Cleanup.exe\" exit /b 1\r\n"
                                          L"if exist \"%~dpn0\\request\" del /q \"%~dpn0\\request\"\r\n"
                                          L"if exist \"%~dpn0\\request\" exit /b 1\r\n"
                                          L"rmdir \"%~dpn0\"\r\n"
                                          L"if exist \"%~dpn0\" exit /b 1\r\n"
                                          L":retire\r\n"
                                          L"if exist \"%~dpn0.done\" del /q \"%~dpn0.done\"\r\n"
                                          L"if exist \"%~dpn0.done\" exit /b 1\r\n"
                                          L"(\r\n"
                                          L"del /q \"%~f0\"\r\n"
                                          L"if exist \"%~f0\" exit /b 1\r\n"
                                          L"rmdir \"%~dp0\" 2>nul\r\n"
                                          L"\"%~1\" /Delete /TN \"" +
               task + L"\" /F\r\n"
                      L")\r\n";
    }

    inline std::wstring XmlText(const std::wstring& text)
    {
        std::wstring result;
        for (const auto ch : text)
        {
            if (ch == L'&')
                result += L"&amp;";
            else if (ch == L'<')
                result += L"&lt;";
            else if (ch == L'>')
                result += L"&gt;";
            else if (ch == L'\"')
                result += L"&quot;";
            else
                result += ch;
        }
        return result;
    }

    inline void Schedule(const std::filesystem::path& stage, const std::wstring& owner, const std::wstring& operation)
    {
        require_system();
        const auto command = TaskCommand(stage, owner, operation);
        product::assert_system_directory(stage.parent_path());
        product::write_new_text(stage.parent_path() / (operation + L".cmd"), TaskScript(owner, operation));
        const auto xml = L"<?xml version=\"1.0\" encoding=\"UTF-16\"?>"
                         L"<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">"
                         L"<Triggers><BootTrigger><Enabled>true</Enabled><Delay>PT30S</Delay></BootTrigger>"
                         L"<RegistrationTrigger><Repetition><Interval>PT5M</Interval><Duration>P1D</Duration>"
                         L"<StopAtDurationEnd>false</StopAtDurationEnd></Repetition><Enabled>true</Enabled><Delay>PT30S</Delay></RegistrationTrigger></Triggers>"
                         L"<Principals><Principal id=\"System\"><UserId>S-1-5-18</UserId>"
                         L"<RunLevel>HighestAvailable</RunLevel></Principal></Principals>"
                         L"<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>"
                         L"<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><StartWhenAvailable>true</StartWhenAvailable>"
                         L"<ExecutionTimeLimit>PT5M</ExecutionTimeLimit><RestartOnFailure><Interval>PT1M</Interval><Count>3</Count></RestartOnFailure></Settings>"
                         L"<Actions Context=\"System\"><Exec><Command>" +
                         XmlText(SystemExecutable(L"cmd.exe").wstring()) +
                         L"</Command><Arguments>" + XmlText(command) + L"</Arguments><WorkingDirectory>" +
                         XmlText(SystemExecutable(L"cmd.exe").parent_path().wstring()) + L"</WorkingDirectory></Exec></Actions></Task>";
        const HRESULT initialized = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
        if (FAILED(initialized) && initialized != RPC_E_CHANGED_MODE)
            throw failure("initialize profile cleanup scheduler", static_cast<DWORD>(initialized));
        struct Uninitialize
        {
            bool owned;
            ~Uninitialize()
            {
                if (owned)
                    CoUninitialize();
            }
        } uninitialize{ SUCCEEDED(initialized) };
        using Microsoft::WRL::ComPtr;
        const auto hr = [](HRESULT value, const char* message) {
            if (FAILED(value))
                throw failure(message, static_cast<DWORD>(value));
        };
        ComPtr<ITaskService> scheduler;
        hr(CoCreateInstance(CLSID_TaskScheduler, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&scheduler)), "create cleanup scheduler");
        VARIANT empty{};
        hr(scheduler->Connect(empty, empty, empty, empty), "connect cleanup scheduler");
        struct Text
        {
            BSTR value;
            explicit Text(const std::wstring& value) : value(SysAllocString(value.c_str())) { check(this->value != nullptr, "scheduler string"); }
            ~Text() { SysFreeString(value); }
        };
        Text root(L"\\"), name(TaskName(operation)), definition(xml), descriptor(L"D:P(A;;GA;;;SY)(A;;GR;;;BA)");
        ComPtr<ITaskFolder> folder;
        hr(scheduler->GetFolder(root.value, &folder), "cleanup task folder");
        VARIANT security{};
        security.vt = VT_BSTR;
        security.bstrVal = descriptor.value;
        ComPtr<IRegisteredTask> task;
        hr(folder->RegisterTask(name.value, definition.value, TASK_CREATE, empty, empty, TASK_LOGON_SERVICE_ACCOUNT, security, &task), "register protected profile cleanup task");
    }

    struct Context
    {
        std::filesystem::path record;
        std::wstring owner;
        std::wstring service;

        std::filesystem::path Stage(const std::wstring& operation) const
        {
            TaskName(operation);
            return record / L"ProfileCleanup" / operation;
        }
    };

    inline std::wstring ReadText(const std::filesystem::path& path)
    {
        handle file(CreateFileW(path.c_str(), GENERIC_READ | READ_CONTROL, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        check(file.value != INVALID_HANDLE_VALUE, "open protected profile cleanup metadata");
        PowerToys::ProtectedStorage::CheckAcl(file.value, L"", false, false);
        const auto bytes = Maintenance::ReadLocked(file.value, 4096);
        if (std::any_of(bytes.begin(), bytes.end(), [](BYTE ch) { return ch > 127; }))
            throw failure("profile cleanup metadata must be ASCII", ERROR_INVALID_DATA);
        return { bytes.begin(), bytes.end() };
    }

    inline void WriteText(const std::filesystem::path& path, const std::wstring& text)
    {
        const auto temporary = path.parent_path() / (L"profile-write-" + setup::unique_id());
        product::write_new_text(temporary, text);
        if (!MoveFileExW(temporary.c_str(), path.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
        {
            const auto error = GetLastError();
            DeleteFileW(temporary.c_str());
            throw failure("publish profile cleanup metadata", error);
        }
    }

    inline void DeleteMetadata(const std::filesystem::path& path)
    {
        if (!DeleteFileW(path.c_str()))
        {
            const auto error = GetLastError();
            if (error != ERROR_FILE_NOT_FOUND)
                throw failure("delete profile cleanup metadata", error);
        }
    }

    inline void Lock(const Context& context, handle& lock)
    {
        require_system();
        product::assert_system_directory(context.record);
        const auto path = context.record / L"profile-maintenance.lock";
        if (!Exists(path))
        {
            try
            {
                product::write_new(path, {});
            }
            catch (const failure& error)
            {
                if (error.code != ERROR_FILE_EXISTS)
                    throw;
            }
        }
        const auto error = Retry([&] {
            lock.value = CreateFileW(path.c_str(), GENERIC_READ | READ_CONTROL, 0, nullptr, OPEN_EXISTING,
                FILE_FLAG_OPEN_REPARSE_POINT, nullptr);
            return lock.value == INVALID_HANDLE_VALUE ? GetLastError() : DWORD{ ERROR_SUCCESS }; }, [] { return GetTickCount64(); }, [](DWORD delay) { Sleep(delay); }, 30000);
        result(error, "acquire profile lifecycle lease");
        PowerToys::ProtectedStorage::CheckAcl(lock.value, L"", false, false);
        if (!Maintenance::ReadLocked(lock.value, 1).empty())
            throw failure("profile lease must be empty", ERROR_INVALID_DATA);
    }

    inline std::wstring CurrentRequest(const Context& context)
    {
        const auto path = context.record / L"profile-cleanup-id";
        if (!Exists(path))
            return L"";
        const auto value = ReadText(path);
        if (!setup::valid_id(value))
            throw failure("invalid current profile cleanup operation", ERROR_INVALID_DATA);
        return value;
    }

    inline bool ServiceExists(const Context& context)
    {
        service scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
        check(scm.value != nullptr, "profile cleanup SCM");
        service instance(OpenServiceW(scm.value, context.service.c_str(), SERVICE_QUERY_STATUS));
        if (instance.value)
            return true;
        const auto error = GetLastError();
        if (error != ERROR_SERVICE_DOES_NOT_EXIST)
            throw failure("profile cleanup service absence", error);
        return false;
    }

    inline void Cancel(const Context& context)
    {
        DeleteMetadata(context.record / L"profile-cleanup-id");
        const auto pending = context.record / L"cleanup-pending";
        if (Exists(pending))
        {
            const auto text = ReadText(pending);
            if (text == L"va-profile-review\n" || text.rfind(L"profile-cleanup\n", 0) == 0)
                DeleteMetadata(pending);
        }
    }

    inline void Pending(const Context& context, const std::wstring& operation, DWORD error)
    {
        WriteText(context.record / L"cleanup-pending", L"profile-cleanup\noperation=" + operation + L"\nnativeError=" + std::to_wstring(error) + L"\n");
    }

    inline void WriteRequest(const Context& context, const std::wstring& operation, const Profile& profile)
    {
        if (!IsVirtualSid(profile.sid))
            throw failure("invalid profile cleanup identity", ERROR_INVALID_SID);
        ValidateLeaf(profile.leaf, context.service);
        product::write_new_text(context.Stage(operation) / L"request", L"format=1\nowner=" + context.owner + L"\noperation=" + operation + L"\nservice=" + context.service + L"\nsid=" + profile.sid + L"\nleaf=" + profile.leaf + L"\nvolume=" + std::to_wstring(profile.volume) + L"\nfile=" + std::to_wstring(profile.file) + L"\n");
        WriteText(context.record / L"profile-cleanup-id", operation);
    }

    inline Profile ReadRequest(const Context& context, const std::wstring& operation)
    {
        const auto text = ReadText(context.Stage(operation) / L"request");
        const auto fields = PowerToys::ProtectedStorage::ParseKeys(ToUtf8(text),
                                                                   { "format", "owner", "operation", "service", "sid", "leaf", "volume", "file" });
        const auto wide = [&](const char* key) { const auto& value = fields.at(key); return std::wstring(value.begin(), value.end()); };
        if (fields.at("format") != "1" || wide("owner") != context.owner || wide("operation") != operation ||
            wide("service") != context.service)
            throw failure("profile cleanup request binding", ERROR_INVALID_OWNER);
        const auto volume = PowerToys::ProtectedStorage::Decimal(fields.at("volume"));
        if (volume > MAXDWORD)
            throw failure("profile cleanup volume", ERROR_INVALID_DATA);
        Profile profile{ wide("sid"), wide("leaf"), static_cast<DWORD>(volume), PowerToys::ProtectedStorage::Decimal(fields.at("file")) };
        if (!IsVirtualSid(profile.sid))
            throw failure("profile cleanup SID", ERROR_INVALID_SID);
        ValidateLeaf(profile.leaf, context.service);
        return profile;
    }

    inline void Queue(const Context& context, const Profile& profile, DWORD error)
    {
        require_system();
        const auto operation = setup::unique_id();
        const auto stage = context.Stage(operation);
        directory(stage.parent_path(), system_acl());
        product::assert_system_directory(stage.parent_path());
        setup::new_directory(stage, system_acl());
        handle source(CreateFileW(product::module_path().c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        check(source.value != INVALID_HANDLE_VALUE, "hold signed profile cleanup worker");
        Maintenance::VerifySignedFile(product::module_path());
        product::write_new(stage / L"Cleanup.exe", Maintenance::ReadLocked(source.value));
        Maintenance::VerifySignedFile(stage / L"Cleanup.exe");
        WriteRequest(context, operation, profile);
        Pending(context, operation, error);
        try
        {
            Schedule(stage, context.owner, operation);
        }
        catch (const failure& failure)
        {
            Pending(context, operation, failure.code);
            throw;
        }
    }

    inline DWORD Run(const Context& context, const std::wstring& operation)
    {
        require_system();
        canonical_owner(context.owner);
        const auto stage = context.Stage(operation);
        if (_wcsicmp(product::module_path().c_str(), (stage / L"Cleanup.exe").c_str()))
            throw failure("profile retry must use its protected staged image", ERROR_ACCESS_DENIED);
        product::assert_system_directory(context.record.parent_path());
        product::assert_system_directory(context.record);
        product::assert_system_directory(stage.parent_path());
        product::assert_system_directory(stage);
        const auto expected = ReadRequest(context, operation);
        handle lock;
        Lock(context, lock);
        const auto current = CurrentRequest(context);
        if (current == operation)
        {
            if (!OwnsRequest(operation, current, Exists(context.record / L"active"), Exists(context.record / L"pending-install"), ServiceExists(context)))
                return ERROR_BUSY;
            const auto profile = Inspect(expected.sid, context.service, expected.leaf);
            if (profile && !SameProfile(expected, *profile))
            {
                Pending(context, operation, ERROR_INVALID_OWNER);
                return ERROR_INVALID_OWNER;
            }
            const auto error = Remove(expected, context.service);
            if (error != ERROR_SUCCESS)
            {
                Pending(context, operation, error);
                return error;
            }
            Cancel(context);
        }
        const auto done = stage.parent_path() / (operation + L".done");
        if (!Exists(done))
            product::write_new(done, {});
        return ERROR_SUCCESS;
    }
}
