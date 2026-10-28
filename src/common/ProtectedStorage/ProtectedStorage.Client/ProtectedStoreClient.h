#pragma once
#include "../ProtectedStorage.Common/Protocol.h"
#include "../ProtectedStorage.Common/Authentication.h"
#include <mutex>

namespace PowerToys::ProtectedStorage
{
    struct Revision
    {
        std::string epoch;
        uint64_t sequence = 0;
        bool operator==(const Revision&) const = default;
    };
    enum class TargetState
    {
        Uninitialized,
        Initialized,
        RecoveryRequired
    };
    struct TargetInfo
    {
        TargetState state = TargetState::Uninitialized;
        std::optional<Revision> revision;
        std::optional<Value> migrationSource;
        std::optional<std::string> initializationOperationId;
        bool cleanupAcknowledged = false;
        bool autoImportSuppressed = false;
    };
    struct BlobValue
    {
        std::string target;
        Revision revision;
        std::string contentSchema;
        std::vector<BYTE> bytes;
    };
    enum class WriteConditionKind
    {
        IfUninitialized,
        IfRevision
    };
    struct WriteRequest
    {
        std::string operationId;
        std::string target;
        WriteConditionKind condition = WriteConditionKind::IfUninitialized;
        std::optional<Revision> expected;
        std::string contentSchema;
        std::vector<BYTE> bytes;
        std::optional<Value> migrationSource;
    };
    struct WriteResult
    {
        std::string operationId;
        std::optional<Revision> revision;
        // Unknown includes operations outside the bounded durable receipt window.
        std::string outcome;
    };
    class ProtectedStoreClient
    {
    public:
        explicit ProtectedStoreClient(DWORD timeout = IoTimeout);
        void Connect();
        Value GetCapabilities();
        SignedClientCatalog GetSignedClientCatalog();
        TargetInfo GetState(const std::string& target);
        TargetInfo GetTargetState(const std::string& target) { return GetState(target); }
        BlobValue GetBlob(const std::string& target);
        WriteResult PutBlob(const WriteRequest& request);
        WriteResult QueryWrite(const std::string& target, const std::string& operationId);
        void AcknowledgeSourceCleanup(const std::string& target, const std::string& operationId);
        std::string CreateTransientBlob(const std::string& target, const std::string& operationId, const std::vector<BYTE>& bytes, const std::string& contentSchema);
        BlobValue GetTransientBlob(const std::string& target, const std::string& id);
        WriteResult UpdateTransientBlob(const std::string& target, const std::string& id, const std::string& operationId, const Revision& expected, const std::vector<BYTE>& bytes, const std::string& contentSchema);
        void DeleteTransientBlob(const std::string& target, const std::string& id);

    private:
        Frame Call(DataCommand command, Value::Object metadata, std::vector<BYTE> bytes = {});
        std::wstring m_owner;
        DWORD m_timeout;
        std::mutex m_mutex;
    };
    class MaintenanceClient
    {
    public:
        MaintenanceClient();
        std::string GetStatus();
        std::map<std::string, std::string> PrepareUpdate(const std::wstring& operation, const std::wstring& bundle);
        std::map<std::string, std::string> QueryUpdate(const std::wstring& operation);
        std::map<std::string, std::string> CommitUpdate(const std::wstring& operation);
        std::map<std::string, std::string> RollbackUpdate(const std::wstring& operation);

    private:
        Paths m_paths;
    };
    Value ToValue(const Revision& revision);
    Revision ParseRevision(const Value& value);
}
