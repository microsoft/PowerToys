#include "ProtectedStoreClient.h"
#include "ClientProtocol.h"
#include "../ProtectedStorage.Common/Authentication.h"

namespace PowerToys::ProtectedStorage
{
    namespace ClientProtocol
    {
        void ValidateResponse(const Frame& request, const Frame& response)
        {
            Check(response.command == request.command && response.requestId == request.requestId, ErrorCode::InvalidPayload);
            const auto& code = response.metadata.At("errorCode").Text();
            const auto requestOperation = request.metadata.Find("operationId");
            if (requestOperation)
                Check(GuidBytes(response.metadata.At("operationId").Text()) == GuidBytes(requestOperation->Text()),
                      ErrorCode::InvalidPayload);
            if (code != "None")
            {
                bool known = false;
                for (unsigned i = 0; i <= static_cast<unsigned>(ErrorCode::InternalError); ++i)
                    known = known || code == ErrorName(static_cast<ErrorCode>(i));
                Check(known && response.bytes.empty() && response.metadata.At("nativeCode").Number() <= MAXDWORD,
                      ErrorCode::InvalidPayload);
                const auto& retry = response.metadata.At("retryClass").Text();
                Check(retry == "None" || retry == "Retry" || retry == "QueryOutcome", ErrorCode::InvalidPayload);
                response.metadata.At("messageKey").Text();
                response.metadata.At("operationId").Text();
                return;
            }
            if (!IsMutatingCommand(request.command))
                return;
            Check(response.bytes.empty(), ErrorCode::InvalidPayload);
            if (request.command == DataCommand::PutBlob || request.command == DataCommand::UpdateTransient)
            {
                Check(response.metadata.At("outcome").Text() == "Committed", ErrorCode::InvalidPayload);
                const auto revision = ParseRevision(response.metadata.At("revision"));
                const auto epoch = GuidBytes(revision.epoch);
                Check(revision.sequence && std::any_of(epoch.begin(), epoch.end(), [](BYTE value) { return value != 0; }),
                      ErrorCode::InvalidPayload);
                if (request.command == DataCommand::UpdateTransient)
                    Check(epoch == GuidBytes(request.metadata.At("id").Text()), ErrorCode::InvalidPayload);
            }
            else if (request.command == DataCommand::AcknowledgeSourceCleanup)
                Check(response.metadata.At("cleanupAcknowledged").Boolean(), ErrorCode::InvalidPayload);
            if (request.command >= DataCommand::CreateTransient)
            {
                const auto id = GuidBytes(response.metadata.At("id").Text());
                Check(std::any_of(id.begin(), id.end(), [](BYTE value) { return value != 0; }), ErrorCode::InvalidPayload);
                if (request.command != DataCommand::CreateTransient)
                    Check(id == GuidBytes(request.metadata.At("id").Text()), ErrorCode::InvalidPayload);
            }
        }
        void ThrowResponseError(const Frame& request, const Frame& response)
        {
            if (response.metadata.At("errorCode").Text() == "None")
                return;
            if (response.metadata.At("retryClass").Text() == "QueryOutcome")
            {
                const auto operation = request.metadata.Find("operationId");
                throw StorageError(ErrorCode::OutcomeUnknown, static_cast<DWORD>(response.metadata.At("nativeCode").Number()), operation ? operation->Text() : response.metadata.At("operationId").Text());
            }
            ThrowIfError(response.metadata);
        }
        StorageError TransportFailure(const Frame& request, bool writeAttempted, const std::exception& failure)
        {
            const auto operation = request.metadata.Find("operationId");
            const auto id = operation ? operation->Text() : "";
            const auto native = dynamic_cast<const Error*>(&failure);
            const auto code = native ? native->code : ERROR_GEN_FAILURE;
            if (writeAttempted && IsMutatingCommand(request.command))
                return StorageError(ErrorCode::OutcomeUnknown, code, id);
            if (const auto typed = dynamic_cast<const StorageError*>(&failure))
                return StorageError(typed->errorCode, code, id);
            return StorageError(code == ERROR_TIMEOUT || code == ERROR_SEM_TIMEOUT        ? ErrorCode::Timeout :
                                code == ERROR_NONE_MAPPED || code == ERROR_FILE_NOT_FOUND ? ErrorCode::NotProvisioned :
                                code == ERROR_ACCESS_DENIED                               ? ErrorCode::Unauthorized :
                                                                                            ErrorCode::InternalError,
                                code,
                                id);
        }
    }
    Value ToValue(const Revision& revision)
    {
        return Value::Object{ { "epoch", revision.epoch }, { "sequence", std::to_string(revision.sequence) } };
    }
    Revision ParseRevision(const Value& value)
    {
        return { GuidText(GuidBytes(value.At("epoch").Text())), Decimal(value.At("sequence").Text()) };
    }
    ProtectedStoreClient::ProtectedStoreClient(DWORD timeout) : m_owner(TokenSid()), m_timeout(timeout)
    {
        Check(ValidOwner(m_owner), ErrorCode::Unauthorized);
        Check(timeout && timeout <= 60000, ErrorCode::InvalidPayload);
        auto context = OpenOwnerConnectionToken(m_owner);
    }
    void ProtectedStoreClient::Connect()
    {
        GetCapabilities();
    }
    Frame ProtectedStoreClient::Call(DataCommand command, Value::Object metadata, std::vector<BYTE> bytes)
    {
        std::lock_guard guard(m_mutex);
        Frame request{ command, GuidBytes(Utf8(NewId())), std::move(metadata), std::move(bytes) };
        Frame response;
        bool sent = false;
        try
        {
            if (const auto operation = request.metadata.Find("operationId"))
                GuidBytes(operation->Text());
            EncodeFrame(request);
            Check(TokenSid() == m_owner, ErrorCode::Unauthorized);
            Paths paths(m_owner);
            const auto endpoint = L"\\\\.\\pipe\\PowerToysProtectedStorage.Data." + m_owner;
            auto pipe = ConnectOwnerPipe(paths, endpoint, m_timeout);
            VerifyServer(pipe.get(), paths, false);
            sent = true;
            WriteFrame(pipe.get(), request, nullptr, m_timeout);
            response = ReadFrame(pipe.get(), nullptr, m_timeout);
            ClientProtocol::ValidateResponse(request, response);
            VerifyServer(pipe.get(), paths, false);
        }
        catch (const std::exception& error)
        {
            throw ClientProtocol::TransportFailure(request, sent, error);
        }
        ClientProtocol::ThrowResponseError(request, response);
        return response;
    }
    Value ProtectedStoreClient::GetCapabilities()
    {
        return Call(DataCommand::GetCapabilities, {}).metadata;
    }
    SignedClientCatalog ProtectedStoreClient::GetSignedClientCatalog()
    {
        auto response = Call(DataCommand::GetCapabilities, { { "includeClientCatalog", true } });
        const auto length = response.metadata.Find("catalogLength");
        Check(length != nullptr, ErrorCode::IncompatibleVersion);
        const auto count = length->Number();
        Check(count && count <= MaxMetadata && response.bytes.size() > count &&
                  response.bytes.size() - count <= 1024 * 1024,
              ErrorCode::InvalidPayload);
        const auto middle = response.bytes.begin() + static_cast<size_t>(count);
        return { { response.bytes.begin(), middle }, { middle, response.bytes.end() } };
    }
    TargetInfo ProtectedStoreClient::GetState(const std::string& target)
    {
        auto response = Call(DataCommand::GetState, { { "target", target } });
        const auto& metadata = response.metadata;
        TargetInfo result;
        const auto suppression = metadata.Find("autoImportSuppressed");
        Check(suppression != nullptr, ErrorCode::IncompatibleVersion);
        result.autoImportSuppressed = suppression->Boolean();
        const auto& state = metadata.At("state").Text();
        Check(state == "Initialized" || state == "Uninitialized" || state == "RecoveryRequired", ErrorCode::InvalidPayload);
        result.state = state == "Initialized"   ? TargetState::Initialized :
                       state == "Uninitialized" ? TargetState::Uninitialized :
                                                  TargetState::RecoveryRequired;
        if (auto revision = metadata.Find("revision"))
            result.revision = ParseRevision(*revision);
        if (auto receipt = metadata.Find("migrationSource"))
            result.migrationSource = *receipt;
        if (auto operation = metadata.Find("initializationOperationId"))
            result.initializationOperationId = operation->Text();
        if (auto acknowledged = metadata.Find("cleanupAcknowledged"))
            result.cleanupAcknowledged = acknowledged->Boolean();
        return result;
    }
    BlobValue ProtectedStoreClient::GetBlob(const std::string& target)
    {
        auto response = Call(DataCommand::GetBlob, { { "target", target } });
        return { target, ParseRevision(response.metadata.At("revision")), response.metadata.At("contentSchema").Text(), std::move(response.bytes) };
    }
    WriteResult ProtectedStoreClient::PutBlob(const WriteRequest& request)
    {
        Value::Object condition{ { "kind", request.condition == WriteConditionKind::IfUninitialized ? "IfUninitialized" : "IfRevision" } };
        if (request.condition == WriteConditionKind::IfRevision)
        {
            Check(request.expected.has_value(), ErrorCode::InvalidPayload);
            condition["expected"] = ToValue(*request.expected);
        }
        Value::Object metadata{ { "target", request.target }, { "operationId", request.operationId }, { "condition", std::move(condition) }, { "contentSchema", request.contentSchema } };
        if (request.migrationSource)
            metadata["migrationSource"] = *request.migrationSource;
        auto response = Call(DataCommand::PutBlob, std::move(metadata), request.bytes);
        return { response.metadata.At("operationId").Text(), ParseRevision(response.metadata.At("revision")), response.metadata.At("outcome").Text() };
    }
    WriteResult ProtectedStoreClient::QueryWrite(const std::string& target, const std::string& operationId)
    {
        auto response = Call(DataCommand::QueryWrite, { { "target", target }, { "operationId", operationId } });
        WriteResult result{ operationId, {}, response.metadata.At("outcome").Text() };
        if (auto revision = response.metadata.Find("revision"))
            result.revision = ParseRevision(*revision);
        return result;
    }
    void ProtectedStoreClient::AcknowledgeSourceCleanup(const std::string& target, const std::string& operationId)
    {
        Call(DataCommand::AcknowledgeSourceCleanup, { { "target", target }, { "operationId", operationId } });
    }
    std::string ProtectedStoreClient::CreateTransientBlob(const std::string& target, const std::string& operationId, const std::vector<BYTE>& bytes, const std::string& contentSchema)
    {
        return Call(DataCommand::CreateTransient, { { "target", target }, { "operationId", operationId }, { "contentSchema", contentSchema } }, bytes).metadata.At("id").Text();
    }
    BlobValue ProtectedStoreClient::GetTransientBlob(const std::string& target, const std::string& id)
    {
        auto response = Call(DataCommand::GetTransient, { { "target", target }, { "id", id } });
        return { target, ParseRevision(response.metadata.At("revision")), response.metadata.At("contentSchema").Text(), std::move(response.bytes) };
    }
    void ProtectedStoreClient::DeleteTransientBlob(const std::string& target, const std::string& id)
    {
        Call(DataCommand::DeleteTransient, { { "target", target }, { "id", id } });
    }
    WriteResult ProtectedStoreClient::UpdateTransientBlob(const std::string& target, const std::string& id, const std::string& operationId, const Revision& expected, const std::vector<BYTE>& bytes, const std::string& contentSchema)
    {
        const auto capabilities = GetCapabilities();
        const auto& features = capabilities.At("features").Items();
        Check(std::any_of(features.begin(), features.end(), [](const Value& feature) { return feature.Text() == "transient-cas"; }),
              ErrorCode::IncompatibleVersion);
        auto response = Call(DataCommand::UpdateTransient, { { "target", target }, { "id", id }, { "operationId", operationId }, { "contentSchema", contentSchema }, { "condition", Value::Object{ { "kind", "IfRevision" }, { "expected", ToValue(expected) } } } }, bytes);
        return { response.metadata.At("operationId").Text(), ParseRevision(response.metadata.At("revision")), response.metadata.At("outcome").Text() };
    }
    MaintenanceClient::MaintenanceClient() : m_paths(TokenSid()) {}
    std::string MaintenanceClient::GetStatus()
    {
        return PowerToys::ProtectedStorage::Call(m_paths, Request{});
    }
    std::map<std::string, std::string> MaintenanceClient::PrepareUpdate(const std::wstring& operation, const std::wstring& bundle)
    {
        return MsiCall(m_paths, Command::MsiPrepare, operation, bundle);
    }
    std::map<std::string, std::string> MaintenanceClient::QueryUpdate(const std::wstring& operation)
    {
        return MsiCall(m_paths, Command::MsiQuery, operation);
    }
    std::map<std::string, std::string> MaintenanceClient::CommitUpdate(const std::wstring& operation)
    {
        return MsiCall(m_paths, Command::MsiCommit, operation);
    }
    std::map<std::string, std::string> MaintenanceClient::RollbackUpdate(const std::wstring& operation)
    {
        return MsiCall(m_paths, Command::MsiRollback, operation);
    }
}
