#pragma once
#include "Common.h"
#include <optional>
#include <variant>

namespace PowerToys::ProtectedStorage
{
    constexpr uint32_t HeaderLength = 36;
    constexpr uint32_t MaxMetadata = 64 * 1024;
    constexpr uint32_t MaxBlob = 16 * 1024 * 1024;
    constexpr uint32_t MaxBody = 4 + MaxMetadata + MaxBlob;

    enum class DataCommand : uint32_t
    {
        GetCapabilities = 1,
        GetState,
        GetBlob,
        PutBlob,
        QueryWrite,
        AcknowledgeSourceCleanup,
        CreateTransient,
        GetTransient,
        DeleteTransient,
        UpdateTransient
    };
    constexpr bool IsMutatingCommand(DataCommand command)
    {
        return command == DataCommand::PutBlob || command == DataCommand::AcknowledgeSourceCleanup ||
               command == DataCommand::CreateTransient || command == DataCommand::DeleteTransient ||
               command == DataCommand::UpdateTransient;
    }

    // Metadata deliberately supports integer JSON numbers only. Revisions use strings.
    struct Value
    {
        using Object = std::map<std::string, Value>;
        using Array = std::vector<Value>;
        std::variant<std::nullptr_t, bool, uint64_t, std::string, Object, Array> data = nullptr;
        Value() = default;
        Value(std::nullptr_t) {}
        Value(bool value) : data(value) {}
        Value(uint64_t value) : data(value) {}
        Value(std::string value) : data(std::move(value)) {}
        Value(const char* value) : data(std::string(value)) {}
        Value(Object value) : data(std::move(value)) {}
        Value(Array value) : data(std::move(value)) {}
        const Object& Members() const;
        const Array& Items() const;
        const std::string& Text() const;
        uint64_t Number() const;
        bool Boolean() const;
        const Value& At(std::string_view key) const;
        const Value* Find(std::string_view key) const;
        std::string Stringify() const;
        static Value Parse(std::string_view text);
    };

    struct Frame
    {
        DataCommand command = DataCommand::GetCapabilities;
        std::array<BYTE, 16> requestId{};
        Value metadata{ Value::Object{} };
        std::vector<BYTE> bytes;
    };
    std::array<BYTE, 16> GuidBytes(std::string_view id);
    std::string GuidText(const std::array<BYTE, 16>& id);
    uint64_t Decimal(std::string_view text);
    std::vector<BYTE> EncodeFrame(const Frame& frame);
    Frame DecodeFrame(const std::vector<BYTE>& bytes);
    void ValidateNetworkFrame(const Frame& frame);
    Frame ReadFrame(HANDLE pipe, HANDLE cancel = nullptr, DWORD timeout = IoTimeout);
    void WriteFrame(HANDLE pipe, const Frame& frame, HANDLE cancel = nullptr, DWORD timeout = IoTimeout);
    void ValidateImportIntent(const Frame& request, bool autoImportSuppressed);

    enum class ErrorCode
    {
        Unauthorized,
        TargetDenied,
        NotProvisioned,
        AuthorizationRequired,
        AlreadyInitialized,
        RevisionConflict,
        BusyMaintenance,
        InvalidPayload,
        Timeout,
        OutcomeUnknown,
        RecoveryRequired,
        IncompatibleVersion,
        QuotaExceeded,
        NotFound,
        OperationConflict,
        CleanupPending,
        OwnerContextRequired,
        InternalError
    };
    std::string_view ErrorName(ErrorCode code);
    struct StorageError : Error
    {
        ErrorCode errorCode;
        std::string operationId;
        explicit StorageError(ErrorCode value, DWORD nativeCode = ERROR_INVALID_DATA, std::string operation = {});
    };
    void Check(bool condition, ErrorCode error, DWORD nativeCode = ERROR_INVALID_DATA);
    Value ErrorMetadata(const std::exception& error, std::string_view operation = {});
    void ThrowIfError(const Value& metadata);
}
