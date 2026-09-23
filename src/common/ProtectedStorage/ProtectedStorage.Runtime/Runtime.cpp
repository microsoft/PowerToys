#include "BlobStore.h"
#include "../ProtectedStorage.Common/Authentication.h"
#include <iostream>
#include <memory>

using namespace PowerToys::ProtectedStorage;

namespace
{
    bool Maintenance(const Paths& paths)
    {
        if (paths.Draining())
            return true;
        auto current = CurrentTransaction(paths);
        return !current.empty() && !IsTerminal(Journal(current));
    }
    int Run(int argc, wchar_t** argv)
    {
        Require(argc == 6 && std::wstring_view(argv[1]) == L"--worker", "Runtime requires inherited readiness/stop/recovery handles");
        Paths paths(argv[2]);
        CheckOwnVa(paths);
        paths.Check();
        Check(_wcsicmp(ModulePath().c_str(), (paths.code + L"\\Runtime.exe").c_str()) == 0, ErrorCode::Unauthorized);
        VerifyLiveRelease(paths);
        auto pinnedCode = HoldDirectories(paths.code);
        auto pinnedData = HoldDirectories(paths.data);
        Handle ready(ParseHandle(argv[3])), stop(ParseHandle(argv[4])), recoveryEvent(ParseHandle(argv[5]));
        AllowIdentityQuery(paths);
        BlobStore store(paths.data, [&](HANDLE file) { CheckAcl(file, paths.va, true, false); });
        bool recovery = false;
        try
        {
            store.Recover();
            paths.AutoImportSuppressed();
        }
        catch (const StorageError& error)
        {
            if (error.errorCode != ErrorCode::RecoveryRequired)
                throw;
            recovery = true;
        }
        if (recovery && !SetEvent(recoveryEvent.get()))
            Fail("signal worker recovery");
        TransientBlobStore transient;
        PSECURITY_DESCRIPTOR raw = nullptr;
        auto sddl = PipeSddl(paths.owner, paths.va);
        Check(ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1, &raw, nullptr) != FALSE,
              ErrorCode::InternalError,
              GetLastError());
        std::unique_ptr<void, decltype(&LocalFree)> descriptor(raw, LocalFree);
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), raw, FALSE };
        auto endpoint = L"\\\\.\\pipe\\PowerToysProtectedStorage.Data." + paths.owner;
        Handle pipe(CreateNamedPipeW(endpoint.c_str(), PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED | FILE_FLAG_FIRST_PIPE_INSTANCE, PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS, 1, 65536, 65536, 0, &attributes));
        Check(static_cast<bool>(pipe), ErrorCode::InternalError, GetLastError());
        // Ready means release, index recovery, identity ACLs and the unique data listener were all checked.
        Check(SetEvent(ready.get()) != FALSE, ErrorCode::InternalError, GetLastError());
        while (WaitForSingleObject(stop.get(), 0) == WAIT_TIMEOUT)
        {
            Handle connected(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            if (!connected)
                Fail("data connect event");
            OVERLAPPED operation{};
            operation.hEvent = connected.get();
            BOOL connectedNow = ConnectNamedPipe(pipe.get(), &operation);
            DWORD error = connectedNow ? ERROR_SUCCESS : GetLastError();
            if (!connectedNow && error == ERROR_IO_PENDING)
            {
                HANDLE waits[]{ connected.get(), stop.get() };
                DWORD result = WaitForMultipleObjects(2, waits, FALSE, INFINITE);
                if (result != WAIT_OBJECT_0)
                {
                    CancelIoEx(pipe.get(), &operation);
                    DWORD ignored = 0;
                    GetOverlappedResult(pipe.get(), &operation, &ignored, TRUE);
                    Check(result == WAIT_OBJECT_0 + 1, ErrorCode::InternalError);
                    break;
                }
                DWORD ignored = 0;
                Check(GetOverlappedResult(pipe.get(), &operation, &ignored, FALSE) != FALSE, ErrorCode::InternalError, GetLastError());
            }
            else
                Check(connectedNow || error == ERROR_PIPE_CONNECTED, ErrorCode::InternalError, error);
            Frame request;
            bool parsed = false;
            bool mutationDispatched = false;
            bool dispatchCompleted = false;
            try
            {
                request = ReadFrame(pipe.get(), stop.get());
                parsed = true;
                auto caller = request.command == DataCommand::GetCapabilities ? AuthenticateOwner(pipe.get(), paths) :
                                                                                AuthenticateCaller(pipe.get(), paths);
                AuthorizeRequest(caller, request);
                Frame response;
                if (request.command == DataCommand::GetCapabilities)
                {
                    Check(request.bytes.empty(), ErrorCode::InvalidPayload);
                    response = { request.command, request.requestId, Value::Object{ { "errorCode", "None" }, { "protocolMajor", uint64_t{ 1 } }, { "protocolMinor", uint64_t{ 0 } }, { "maxBlobBytes", uint64_t{ MaxBlob } }, { "maxMetadataBytes", uint64_t{ MaxMetadata } }, { "release", VersionText(OwnVersion()) }, { "maintenance", Maintenance(paths) }, { "recoveryRequired", recovery }, { "features", Value::Array{ "cas", "write-query", "source-receipt", "transient", "transient-cas", "signed-peer-catalog", "purge-suppression" } } }, {} };
                    if (const auto include = request.metadata.Find("includeClientCatalog"); include && include->Boolean())
                    {
                        auto proof = ReadSignedClientCatalog(paths.code);
                        VerifySignedClientCatalog(proof, LoadPolicy(paths), OwnVersion());
                        std::get<Value::Object>(response.metadata.data)["catalogLength"] = static_cast<uint64_t>(proof.document.size());
                        response.bytes = std::move(proof.document);
                        response.bytes.insert(response.bytes.end(), proof.signature.begin(), proof.signature.end());
                    }
                }
                else
                {
                    Check(!Maintenance(paths), ErrorCode::BusyMaintenance, ERROR_BUSY);
                    Handle lease;
                    const bool write = request.command == DataCommand::PutBlob || request.command == DataCommand::AcknowledgeSourceCleanup ||
                                       request.command == DataCommand::CreateTransient || request.command == DataCommand::DeleteTransient ||
                                       request.command == DataCommand::UpdateTransient;
                    if (write)
                    {
                        try
                        {
                            lease = paths.Lock();
                        }
                        catch (const Error& failure)
                        {
                            throw StorageError(ErrorCode::BusyMaintenance, failure.code);
                        }
                        Check(!Maintenance(paths), ErrorCode::BusyMaintenance, ERROR_BUSY);
                    }
                    if (request.command == DataCommand::PutBlob)
                        ValidateImportIntent(request, paths.AutoImportSuppressed());
                    mutationDispatched = IsMutatingCommand(request.command);
                    response = request.command >= DataCommand::CreateTransient ? transient.Dispatch(request) : store.Dispatch(request);
                    dispatchCompleted = true;
                    if (request.command == DataCommand::GetState)
                    {
                        std::get<Value::Object>(response.metadata.data)["autoImportSuppressed"] = paths.AutoImportSuppressed();
                        if (response.metadata.At("state").Text() == "RecoveryRequired")
                        {
                            recovery = true;
                            if (!SetEvent(recoveryEvent.get()))
                                Fail("signal worker recovery");
                        }
                    }
                }
                WriteFrame(pipe.get(), response, stop.get());
            }
            catch (const std::exception& failure)
            {
                if (const auto typed = dynamic_cast<const StorageError*>(&failure);
                    typed && typed->errorCode == ErrorCode::RecoveryRequired)
                {
                    recovery = true;
                    if (!SetEvent(recoveryEvent.get()))
                        Fail("signal worker recovery");
                }
                if (parsed)
                {
                    try
                    {
                        auto id = request.metadata.Find("operationId");
                        const auto native = dynamic_cast<const Error*>(&failure);
                        const auto typed = dynamic_cast<const StorageError*>(&failure);
                        const auto operationId = id ? id->Text() : "";
                        // I/O/allocation failures or failed responses after a mutation may follow its commit point.
                        StorageError unknown(ErrorCode::OutcomeUnknown, native ? native->code : ERROR_GEN_FAILURE, operationId);
                        Frame response{ request.command, request.requestId, ErrorMetadata(mutationDispatched && (dispatchCompleted || !typed) ? unknown : failure, operationId), {} };
                        WriteFrame(pipe.get(), response, stop.get());
                    }
                    catch (const std::exception&)
                    {
                    }
                }
            }
            DisconnectNamedPipe(pipe.get());
        }
        // Writes are synchronous under the maintenance lease: reaching here is a completed drain.
        return 0;
    }
}
int wmain(int argc, wchar_t** argv)
{
    try
    {
        if (argc == 2 && std::wstring_view(argv[1]) == L"--version")
        {
            std::cout << "{\"component\":\"Runtime\",\"version\":" << Json(VersionText(OwnVersion())) << "}\n";
            return 0;
        }
        return Run(argc, argv);
    }
    catch (const std::exception& failure)
    {
        std::cerr << ErrorMetadata(failure).Stringify() << '\n';
        return 1;
    }
}
