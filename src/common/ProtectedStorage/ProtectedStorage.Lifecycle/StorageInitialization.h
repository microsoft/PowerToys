// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#pragma once
#include "..\ProtectedStorage.Setup\MaintenanceContract.h"
#include "..\ProtectedStorage.Common\Protocol.h"
#include <set>

namespace PowerToysProtectedStorage::Provisioning
{
    namespace native = PowerToys::ProtectedStorage;

    enum class IndexPlan
    {
        SeedNewDirectory,
        PreserveExisting,
    };

    inline IndexPlan PlanIndex(bool dataExists, bool retainedData, bool inventoryExists, bool explicitlyPurged)
    {
        if (dataExists) return IndexPlan::PreserveExisting;
        if (retainedData || (inventoryExists && !explicitlyPurged))
            throw failure("retained data directory is missing; recovery required", ERROR_INVALID_STATE);
        return IndexPlan::SeedNewDirectory;
    }

    inline std::string InitialIndex(const std::string& epoch)
    {
        const auto canonical = native::GuidText(native::GuidBytes(epoch));
        return native::Value(native::Value::Object{
            { "format", uint64_t{ 1 } },
            { "epoch", canonical },
            { "targets", native::Value::Object{
                { "workspaces.repository", native::Value::Object{
                    { "sequence", "0" }, { "generation", "" },
                    { "receipts", native::Value::Array{} }, { "cleanupAcknowledged", false } } } } } }).Stringify();
    }

    inline void RequireIndex(bool condition)
    {
        if (!condition) throw failure("storage index is corrupt; recovery required", ERROR_INVALID_STATE);
    }

    inline bool HasPurgeRecreationTicket(const std::filesystem::path& record, const std::wstring& owner)
    {
        const auto ticket = record / L"purge-recreation.ticket";
        if (!setup::exists(ticket)) return false;
        try
        {
            handle file(CreateFileW(ticket.c_str(), GENERIC_READ | READ_CONTROL, FILE_SHARE_READ, nullptr,
                OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
            RequireIndex(file.value != INVALID_HANDLE_VALUE);
            native::CheckAcl(file.value, L"", false, false);
            const auto bytes = Maintenance::ReadLocked(file.value, 4096);
            const auto values = native::ParseKeys(std::string(bytes.begin(), bytes.end()), { "format", "owner", "mode" });
            RequireIndex(values.at("format") == "1" && values.at("owner") == ToUtf8(owner) && values.at("mode") == "purge");
            return true;
        }
        catch (const std::exception&)
        {
            throw failure("purge recreation authorization is invalid; recovery required", ERROR_INVALID_STATE);
        }
    }

    inline void ValidateIndex(const std::string& text)
    {
        try
        {
            RequireIndex(text.size() <= native::MaxMetadata);
            const auto index = native::Value::Parse(text);
            const auto& epoch = index.At("epoch").Text();
            RequireIndex(index.At("format").Number() == 1 && native::GuidText(native::GuidBytes(epoch)) == epoch);
            const auto& targets = index.At("targets").Members();
            RequireIndex(!targets.empty() && targets.size() <= 64);
            for (const auto& [target, record] : targets)
            {
                RequireIndex(!target.empty() && target.size() <= 128 &&
                    target.find_first_not_of("abcdefghijklmnopqrstuvwxyz0123456789._-") == target.npos);
                const auto sequence = native::Decimal(record.At("sequence").Text());
                const auto& generation = record.At("generation").Text();
                RequireIndex(sequence ? native::GuidText(native::GuidBytes(generation)) == generation : generation.empty());
                const auto& receipts = record.At("receipts").Items();
                RequireIndex(receipts.size() <= 128);
                const bool acknowledged = record.At("cleanupAcknowledged").Boolean();
                uint64_t previous = 0;
                std::set<std::string> operations;
                for (const auto& receipt : receipts)
                {
                    const auto id = native::GuidText(native::GuidBytes(receipt.At("operationId").Text()));
                    RequireIndex(operations.insert(id).second && native::IsHash(receipt.At("digest").Text()));
                    const auto receiptSequence = native::Decimal(receipt.At("sequence").Text());
                    RequireIndex(receiptSequence > previous && receiptSequence <= sequence);
                    previous = receiptSequence;
                }
                RequireIndex(previous == sequence);
                if (sequence)
                    (void)native::GuidBytes(record.At("initializationOperationId").Text());
                else
                    RequireIndex(!record.Find("migrationSource") && !acknowledged);
                if (const auto source = record.Find("migrationSource"))
                {
                    source->Members();
                    RequireIndex(source->Stringify().size() <= 8192);
                }
            }
        }
        catch (const native::Error&)
        {
            throw failure("storage index is invalid; recovery required", ERROR_INVALID_STATE);
        }
    }

    inline void EnsureIndex(const std::filesystem::path& data, IndexPlan plan)
    {
        handle directory(CreateFileW(data.c_str(), FILE_READ_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE,
            nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        if (directory.value == INVALID_HANDLE_VALUE)
            throw failure("data directory is missing; recovery required", ERROR_INVALID_STATE);
        BY_HANDLE_FILE_INFORMATION information{};
        check(GetFileInformationByHandle(directory.value, &information) != FALSE, "data directory identity");
        RequireIndex((information.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) &&
            !(information.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT));
        const auto index = data / L"storage.index";
        if (plan == IndexPlan::PreserveExisting)
        {
            handle file(CreateFileW(index.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr,
                OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
            if (file.value == INVALID_HANDLE_VALUE)
                throw failure("existing storage index is unavailable; recovery required", ERROR_INVALID_STATE);
            try
            {
                const auto bytes = Maintenance::ReadLocked(file.value, native::MaxMetadata);
                ValidateIndex(std::string(bytes.begin(), bytes.end()));
            }
            catch (const failure&)
            {
                throw failure("existing storage index is unsafe or corrupt; recovery required", ERROR_INVALID_STATE);
            }
            return;
        }
        const auto pending = data / (L"storage.index." + setup::unique_id() + L".seed");
        const auto text = InitialIndex(native::Utf8(native::NewId()));
        product::write_new(pending, std::vector<BYTE>(text.begin(), text.end()));
        if (!MoveFileExW(pending.c_str(), index.c_str(), MOVEFILE_WRITE_THROUGH))
        {
            const auto error = GetLastError();
            DeleteFileW(pending.c_str());
            throw failure("publish new index without replacing existing state", error);
        }
    }
}
