#include "BlobStore.h"
#include <algorithm>
#include <set>

namespace PowerToys::ProtectedStorage
{
    namespace
    {
        constexpr size_t ReceiptLimit = 128;
        Value::Object& Object(Value& value)
        {
            value.Members();
            return std::get<Value::Object>(value.data);
        }
        std::string CanonicalId(const Value& value)
        {
            return GuidText(GuidBytes(value.Text()));
        }
        Value RevisionValue(const Value& index, uint64_t sequence)
        {
            return Value::Object{ { "epoch", index.At("epoch") }, { "sequence", std::to_string(sequence) } };
        }
        std::string Digest(const Frame& request)
        {
            Frame normalized = request;
            normalized.requestId = {};
            Object(normalized.metadata)["operationId"] = CanonicalId(request.metadata.At("operationId"));
            const auto bytes = EncodeFrame(normalized);
            return HashBytes(bytes.data(), static_cast<DWORD>(bytes.size()));
        }
        Frame Response(const Frame& request, Value::Object metadata, std::vector<BYTE> bytes = {})
        {
            metadata["errorCode"] = "None";
            return Frame{ request.command, request.requestId, std::move(metadata), std::move(bytes) };
        }
        std::string Target(const Frame& request)
        {
            const auto& target = request.metadata.At("target").Text();
            Check(!target.empty() && target.size() <= 128 &&
                      target.find_first_not_of("abcdefghijklmnopqrstuvwxyz0123456789._-") == target.npos,
                  ErrorCode::TargetDenied);
            return target;
        }
        Value& TargetRecord(Value& index, const std::string& target)
        {
            Check(index.At("targets").Find(target) != nullptr, ErrorCode::TargetDenied);
            return Object(Object(index).at("targets")).at(target);
        }
        const Value& TargetRecord(const Value& index, const std::string& target)
        {
            Check(index.At("targets").Find(target) != nullptr, ErrorCode::TargetDenied);
            return index.At("targets").At(target);
        }
        void ValidateReceipt(const Value& source)
        {
            // The service carries opaque source identity metadata; only the module interprets it.
            Check(source.Stringify().size() <= 8192, ErrorCode::QuotaExceeded);
            source.Members();
        }
    }
    BlobStore::BlobStore(std::wstring directory, std::function<void(HANDLE)> verifyFile) :
        m_directory(std::move(directory)), m_verifyFile(std::move(verifyFile))
    {
        Check(static_cast<bool>(m_verifyFile), ErrorCode::InvalidPayload);
    }
    Value BlobStore::InitialIndex(std::string epoch, const std::vector<std::string>& targets)
    {
        epoch = GuidText(GuidBytes(epoch));
        Value::Object entries;
        for (const auto& target : targets)
        {
            entries.emplace(target, Value::Object{ { "sequence", "0" }, { "generation", "" }, { "receipts", Value::Array{} }, { "cleanupAcknowledged", false } });
        }
        return Value::Object{ { "format", uint64_t{ 1 } }, { "epoch", epoch }, { "targets", std::move(entries) } };
    }
    Value BlobStore::ReadIndex() const
    {
        try
        {
            auto file = OpenRead(m_directory + L"\\storage.index", MaxMetadata);
            m_verifyFile(file.get());
            auto bytes = ReadAll(file.get(), MaxMetadata);
            auto index = Value::Parse(std::string_view(reinterpret_cast<const char*>(bytes.data()), bytes.size()));
            Check(index.At("format").Number() == 1 && CanonicalId(index.At("epoch")) == index.At("epoch").Text(), ErrorCode::RecoveryRequired);
            Check(!index.At("targets").Members().empty() && index.At("targets").Members().size() <= 64, ErrorCode::RecoveryRequired);
            for (const auto& [target, record] : index.At("targets").Members())
            {
                Check(!target.empty() && target.size() <= 128 &&
                          target.find_first_not_of("abcdefghijklmnopqrstuvwxyz0123456789._-") == target.npos,
                      ErrorCode::RecoveryRequired);
                auto sequence = Decimal(record.At("sequence").Text());
                const auto& generation = record.At("generation").Text();
                Check(sequence ? (GuidText(GuidBytes(generation)) == generation) : generation.empty(), ErrorCode::RecoveryRequired);
                Check(record.At("receipts").Items().size() <= ReceiptLimit, ErrorCode::RecoveryRequired);
                record.At("cleanupAcknowledged").Boolean();
                uint64_t previous = 0;
                std::set<std::string> operations;
                for (const auto& receipt : record.At("receipts").Items())
                {
                    auto id = CanonicalId(receipt.At("operationId"));
                    Check(operations.insert(id).second, ErrorCode::RecoveryRequired);
                    Check(IsHash(receipt.At("digest").Text()), ErrorCode::RecoveryRequired);
                    auto receiptSequence = Decimal(receipt.At("sequence").Text());
                    Check(receiptSequence > previous && receiptSequence <= sequence, ErrorCode::RecoveryRequired);
                    previous = receiptSequence;
                }
                Check(previous == sequence, ErrorCode::RecoveryRequired);
                if (sequence)
                    CanonicalId(record.At("initializationOperationId"));
                else
                    Check(!record.Find("migrationSource") && !record.At("cleanupAcknowledged").Boolean(), ErrorCode::RecoveryRequired);
                if (auto source = record.Find("migrationSource"))
                    ValidateReceipt(*source);
            }
            return index;
        }
        catch (const Error& error)
        {
            throw StorageError(ErrorCode::RecoveryRequired, error.code);
        }
    }
    void BlobStore::PublishIndex(const Value& index) const
    {
        const auto text = index.Stringify();
        Check(text.size() <= MaxMetadata, ErrorCode::QuotaExceeded);
        // This is the sole logical commit point. A flushed immutable generation always precedes it.
        ReplaceText(m_directory + L"\\storage.index", text);
        Handle committed(CreateFileW((m_directory + L"\\storage.index").c_str(), GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_WRITE_THROUGH, nullptr));
        if (!committed || !FlushFileBuffers(committed.get()))
            throw StorageError(ErrorCode::OutcomeUnknown, GetLastError());
    }
    Frame BlobStore::ReadGeneration(const Value& index, const std::string& target) const
    {
        const auto& record = TargetRecord(index, target);
        const auto sequence = Decimal(record.At("sequence").Text());
        Check(sequence != 0, ErrorCode::NotFound, ERROR_FILE_NOT_FOUND);
        try
        {
            auto file = OpenRead(m_directory + L"\\blob." + Wide(record.At("generation").Text()), MaxBody + HeaderLength);
            m_verifyFile(file.get());
            auto frame = DecodeFrame(ReadAll(file.get(), MaxBody + HeaderLength));
            Check(frame.command == DataCommand::PutBlob && frame.metadata.At("target").Text() == target &&
                      frame.metadata.At("revision").At("epoch").Text() == index.At("epoch").Text() &&
                      Decimal(frame.metadata.At("revision").At("sequence").Text()) == sequence &&
                      frame.metadata.At("contentHash").Text() == HashBytes(frame.bytes.data(), static_cast<DWORD>(frame.bytes.size())),
                  ErrorCode::RecoveryRequired);
            Check(!record.At("receipts").Items().empty(), ErrorCode::RecoveryRequired);
            const auto& receipt = record.At("receipts").Items().back();
            Check(receipt.At("operationId").Text() == frame.metadata.At("operationId").Text() &&
                      receipt.At("digest").Text() == frame.metadata.At("requestDigest").Text(),
                  ErrorCode::RecoveryRequired);
            return frame;
        }
        catch (const Error& error)
        {
            throw StorageError(ErrorCode::RecoveryRequired, error.code);
        }
    }
    void BlobStore::Recover()
    {
        std::lock_guard guard(m_mutex);
        auto index = ReadIndex();
        std::set<std::wstring> live;
        for (const auto& [target, record] : index.At("targets").Members())
        {
            if (!Decimal(record.At("sequence").Text()))
                continue;
            ReadGeneration(index, target);
            live.insert(L"blob." + Wide(record.At("generation").Text()));
        }
        // Orphans are safe to remove only after the complete durable index has validated.
        WIN32_FIND_DATAW data{};
        HANDLE raw = FindFirstFileW((m_directory + L"\\blob.*").c_str(), &data);
        if (raw == INVALID_HANDLE_VALUE)
        {
            Check(GetLastError() == ERROR_FILE_NOT_FOUND, ErrorCode::RecoveryRequired, GetLastError());
            return;
        }
        struct FindGuard
        {
            HANDLE value;
            ~FindGuard() { FindClose(value); }
        } find{ raw };
        do
        {
            std::wstring name(data.cFileName);
            if (live.contains(name))
                continue;
            Check(!(data.dwFileAttributes & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT)), ErrorCode::RecoveryRequired);
            Check(ValidId(std::wstring_view(name).substr(5)), ErrorCode::RecoveryRequired);
            auto path = m_directory + L"\\" + name;
            {
                auto held = OpenRead(path, MaxBody + HeaderLength);
            }
            if (!DeleteFileW(path.c_str()))
                Fail("reclaim uncommitted generation");
        } while (FindNextFileW(raw, &data));
        Check(GetLastError() == ERROR_NO_MORE_FILES, ErrorCode::RecoveryRequired, GetLastError());
    }
    Frame BlobStore::Inspect(const Frame& request)
    {
        auto target = Target(request);
        try
        {
            auto index = ReadIndex();
            const auto& record = TargetRecord(index, target);
            auto sequence = Decimal(record.At("sequence").Text());
            Value::Object metadata{ { "state", sequence ? "Initialized" : "Uninitialized" },
                                    { "cleanupAcknowledged", record.At("cleanupAcknowledged") } };
            if (sequence)
            {
                ReadGeneration(index, target);
                metadata["revision"] = RevisionValue(index, sequence);
            }
            if (auto source = record.Find("migrationSource"))
                metadata["migrationSource"] = *source;
            if (auto operation = record.Find("initializationOperationId"))
                metadata["initializationOperationId"] = *operation;
            return Response(request, std::move(metadata));
        }
        catch (const StorageError& error)
        {
            if (error.errorCode != ErrorCode::RecoveryRequired)
                throw;
            return Response(request, { { "state", "RecoveryRequired" } });
        }
    }
    Frame BlobStore::Read(const Frame& request)
    {
        const auto target = Target(request);
        auto index = ReadIndex();
        auto frame = ReadGeneration(index, target);
        auto metadata = frame.metadata.Members();
        metadata["state"] = "Initialized";
        return Response(request, std::move(metadata), std::move(frame.bytes));
    }
    Frame BlobStore::Write(const Frame& request)
    {
        const auto target = Target(request);
        const auto id = CanonicalId(request.metadata.At("operationId"));
        const auto& schema = request.metadata.At("contentSchema").Text();
        Check(!schema.empty() && schema.size() <= 256, ErrorCode::InvalidPayload);
        auto digest = Digest(request);
        auto index = ReadIndex();
        auto& record = TargetRecord(index, target);
        auto sequence = Decimal(record.At("sequence").Text());
        if (sequence)
            ReadGeneration(index, target);
        auto& receipts = std::get<Value::Array>(Object(record).at("receipts").data);
        for (const auto& receipt : receipts)
        {
            if (receipt.At("operationId").Text() != id)
                continue;
            Check(receipt.At("digest").Text() == digest, ErrorCode::OperationConflict);
            return Response(request, { { "operationId", id }, { "outcome", "Committed" }, { "revision", RevisionValue(index, Decimal(receipt.At("sequence").Text())) } });
        }
        const auto& condition = request.metadata.At("condition");
        const bool initialize = condition.At("kind").Text() == "IfUninitialized";
        if (initialize)
            Check(sequence == 0, ErrorCode::AlreadyInitialized);
        else
        {
            Check(condition.At("kind").Text() == "IfRevision", ErrorCode::InvalidPayload);
            const auto& expected = condition.At("expected");
            Check(sequence && expected.At("epoch").Text() == index.At("epoch").Text() &&
                      Decimal(expected.At("sequence").Text()) == sequence,
                  ErrorCode::RevisionConflict);
        }
        Check(sequence != UINT64_MAX, ErrorCode::QuotaExceeded);
        if (const auto* source = request.metadata.Find("migrationSource"))
        {
            Check(initialize, ErrorCode::InvalidPayload);
            ValidateReceipt(*source);
            Object(record)["migrationSource"] = *source;
        }
        if (initialize)
            Object(record)["initializationOperationId"] = id;
        ++sequence;
        const auto generation = GuidText(GuidBytes(Utf8(NewId())));
        Frame stored{ DataCommand::PutBlob, {}, Value::Object{ { "target", target }, { "revision", RevisionValue(index, sequence) }, { "contentSchema", schema }, { "operationId", id }, { "requestDigest", digest }, { "contentHash", HashBytes(request.bytes.data(), static_cast<DWORD>(request.bytes.size())) } }, request.bytes };
        const auto encoded = EncodeFrame(stored);
        const auto path = m_directory + L"\\blob." + Wide(generation);
        WriteNew(path, std::string_view(reinterpret_cast<const char*>(encoded.data()), encoded.size()));
        const auto oldGeneration = record.At("generation").Text();
        Object(record)["generation"] = generation;
        Object(record)["sequence"] = std::to_string(sequence);
        receipts.push_back(Value::Object{ { "operationId", id }, { "digest", digest }, { "sequence", std::to_string(sequence) } });
        if (receipts.size() > ReceiptLimit)
            receipts.erase(receipts.begin());
        PublishIndex(index);
        // Failed reclamation cannot turn an already committed save into a failed save.
        if (!oldGeneration.empty())
            DeleteFileW((m_directory + L"\\blob." + Wide(oldGeneration)).c_str());
        return Response(request, { { "operationId", id }, { "outcome", "Committed" }, { "revision", RevisionValue(index, sequence) } });
    }
    Frame BlobStore::Query(const Frame& request)
    {
        auto target = Target(request);
        auto id = CanonicalId(request.metadata.At("operationId"));
        auto index = ReadIndex();
        const auto& record = TargetRecord(index, target);
        auto sequence = Decimal(record.At("sequence").Text());
        if (sequence)
            ReadGeneration(index, target);
        for (const auto& receipt : record.At("receipts").Items())
        {
            if (receipt.At("operationId").Text() == id)
                return Response(request, { { "operationId", id }, { "outcome", "Committed" }, { "revision", RevisionValue(index, Decimal(receipt.At("sequence").Text())) } });
        }
        // Only a never-initialized target proves no operation committed. Expired receipts remain unknown.
        return Response(request, { { "operationId", id }, { "outcome", sequence ? "Unknown" : "NotCommitted" } });
    }
    Frame BlobStore::Acknowledge(const Frame& request)
    {
        auto target = Target(request);
        auto id = CanonicalId(request.metadata.At("operationId"));
        auto index = ReadIndex();
        auto& record = TargetRecord(index, target);
        ReadGeneration(index, target);
        Check(record.Find("initializationOperationId") && record.At("initializationOperationId").Text() == id &&
                  record.Find("migrationSource"),
              ErrorCode::OperationConflict);
        Object(record)["cleanupAcknowledged"] = true;
        PublishIndex(index);
        return Response(request, { { "operationId", id }, { "cleanupAcknowledged", true } });
    }
    Frame BlobStore::Dispatch(const Frame& request)
    {
        std::lock_guard guard(m_mutex);
        Check(request.command == DataCommand::PutBlob || request.bytes.empty(), ErrorCode::InvalidPayload);
        switch (request.command)
        {
        case DataCommand::GetState:
            return Inspect(request);
        case DataCommand::GetBlob:
            return Read(request);
        case DataCommand::PutBlob:
            return Write(request);
        case DataCommand::QueryWrite:
            return Query(request);
        case DataCommand::AcknowledgeSourceCleanup:
            return Acknowledge(request);
        default:
            throw StorageError(ErrorCode::InvalidPayload);
        }
    }
    void TransientBlobStore::Reclaim()
    {
        const auto now = GetTickCount64();
        for (auto it = m_entries.begin(); it != m_entries.end();)
        {
            if (it->second.expires > now)
            {
                ++it;
                continue;
            }
            m_size -= it->second.bytes.size();
            it = m_entries.erase(it);
        }
    }
    Frame TransientBlobStore::Dispatch(const Frame& request)
    {
        Reclaim();
        const auto target = Target(request);
        if (request.command == DataCommand::CreateTransient)
        {
            const auto id = CanonicalId(request.metadata.At("operationId"));
            const auto digest = Digest(request);
            for (const auto& [key, entry] : m_entries)
            {
                if (entry.operation != id)
                    continue;
                Check(entry.digest == digest, ErrorCode::OperationConflict);
                return Response(request, { { "id", key }, { "operationId", id } });
            }
            Check(m_entries.size() < 64 && m_size + request.bytes.size() <= 64ULL * 1024 * 1024, ErrorCode::QuotaExceeded);
            const auto schema = request.metadata.At("contentSchema").Text();
            Check(!schema.empty() && schema.size() <= 256, ErrorCode::InvalidPayload);
            const auto object = GuidText(GuidBytes(Utf8(NewId())));
            Check(m_entries.emplace(object, Entry{ object, id, digest, target, schema, GetTickCount64() + 15 * 60 * 1000, request.bytes, 1, {} }).second,
                  ErrorCode::InternalError);
            m_size += request.bytes.size();
            return Response(request, { { "id", object }, { "operationId", id } });
        }
        Check(request.command == DataCommand::UpdateTransient || request.bytes.empty(), ErrorCode::InvalidPayload);
        const auto id = CanonicalId(request.metadata.At("id"));
        auto found = m_entries.find(id);
        Check(found == m_entries.end() || found->second.target == target, ErrorCode::TargetDenied);
        if (request.command == DataCommand::UpdateTransient)
        {
            Check(found != m_entries.end(), ErrorCode::NotFound);
            auto& entry = found->second;
            const auto operation = CanonicalId(request.metadata.At("operationId"));
            const auto digest = Digest(request);
            Check(operation != entry.operation, ErrorCode::OperationConflict);
            const auto response = [&](uint64_t sequence) {
                return Response(request, { { "id", id }, { "operationId", operation }, { "outcome", "Committed" }, { "revision", Value::Object{ { "epoch", id }, { "sequence", std::to_string(sequence) } } } });
            };
            for (const auto& receipt : entry.receipts)
            {
                if (receipt.operation != operation)
                    continue;
                Check(receipt.digest == digest, ErrorCode::OperationConflict);
                return response(receipt.sequence);
            }
            const auto& condition = request.metadata.At("condition");
            Check(condition.At("kind").Text() == "IfRevision", ErrorCode::InvalidPayload);
            const auto& expected = condition.At("expected");
            Check(expected.At("epoch").Text() == id && Decimal(expected.At("sequence").Text()) == entry.sequence,
                  ErrorCode::RevisionConflict);
            Check(entry.sequence != UINT64_MAX && m_size - entry.bytes.size() + request.bytes.size() <= 64ULL * 1024 * 1024,
                  ErrorCode::QuotaExceeded);
            const auto schema = request.metadata.At("contentSchema").Text();
            Check(!schema.empty() && schema.size() <= 256, ErrorCode::InvalidPayload);
            Entry next = entry;
            next.bytes = request.bytes;
            next.schema = schema;
            ++next.sequence;
            next.receipts.push_back({ operation, digest, next.sequence });
            if (next.receipts.size() > ReceiptLimit)
                next.receipts.erase(next.receipts.begin());
            const auto size = m_size - entry.bytes.size() + next.bytes.size();
            entry = std::move(next);
            m_size = size;
            return response(entry.sequence);
        }
        if (request.command == DataCommand::DeleteTransient)
        {
            if (found != m_entries.end())
            {
                m_size -= found->second.bytes.size();
                m_entries.erase(found);
            }
            return Response(request, { { "id", id } });
        }
        Check(request.command == DataCommand::GetTransient && found != m_entries.end(), ErrorCode::NotFound);
        return Response(request, { { "id", id }, { "target", target }, { "contentSchema", found->second.schema }, { "revision", Value::Object{ { "epoch", id }, { "sequence", std::to_string(found->second.sequence) } } } }, found->second.bytes);
    }
}
